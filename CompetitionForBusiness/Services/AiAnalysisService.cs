using CompetitionForBusiness.Data;
using CompetitionForBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CompetitionForBusiness.Services
{
    public class AiAnalysisService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;

        public AiAnalysisService(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        public async Task<AiAnalysisResult> AnalyzeParticipantPerformanceAsync(Guid participantId)
        {
            try
            {
                Console.WriteLine($"[AI SERVICE LOG] Analiz başladı. ID: {participantId}");

                // 1. Cevapları çek
                var userAnswers = await _context.UserAnswers
                    .AsNoTracking()
                    .Where(u => u.ParticipantId == participantId)
                    .ToListAsync();

                Console.WriteLine($"[AI SERVICE LOG] Cevap sayısı: {userAnswers.Count}");

                if (userAnswers == null || !userAnswers.Any())
                {
                    Console.WriteLine("[AI SERVICE LOG] Cevap bulunamadı.");
                    return new AiAnalysisResult
                    {
                        PrimarySkill = "Genel",
                        FeedbackSummary = "Henüz cevaplanmış soru bulunmuyor.",
                        IsEligibleForInterview = false,
                        OverallScore = 0
                    };
                }

                // 2. KİLİTLENMEYİ ÇÖZEN KISIM:
                // Tüm nesneyi çekmek yerine SADECE gerekli kolonları anonim tip olarak çekiyoruz.
                Console.WriteLine("[AI SERVICE LOG] Sorular hafif (Select) sorguyla çekiliyor...");

                var questionsList = await _context.Questions
                    .AsNoTracking()
                    .Select(q => new 
                    {
                        Id = q.Id,
                        CorrectOption = q.CorrectOption,
                        Category = q.Category
                    })
                    .ToListAsync();

                Console.WriteLine($"[AI SERVICE LOG] Çekilen soru sayısı: {questionsList.Count}");

                var questionsDict = questionsList.ToDictionary(q => q.Id);

                // 3. Hesaplamalar
                int totalCorrect = 0;
                double totalResponseTime = 0;
                var categoryStats = new Dictionary<string, (int Correct, int Total)>();

                foreach (var answer in userAnswers)
                {
                    totalResponseTime += answer.ResponseTimeMs;

                    if (questionsDict.TryGetValue(answer.QuestionId, out var question))
                    {
                        string targetOption = question.CorrectOption ?? string.Empty;
                        string selectedOption = answer.SelectedOption ?? string.Empty;

                        bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                                         selectedOption.Trim().Equals(targetOption.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isCorrect) totalCorrect++;

                        string category = string.IsNullOrWhiteSpace(question.Category) ? "Genel" : question.Category;

                        if (!categoryStats.ContainsKey(category))
                            categoryStats[category] = (0, 0);

                        var current = categoryStats[category];
                        categoryStats[category] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                    }
                }

                int totalAnswered = userAnswers.Count;
                int scorePercentage = totalAnswered > 0 ? (int)((double)totalCorrect / totalAnswered * 100) : 0;
                double avgResponseTimeSec = totalAnswered > 0 ? Math.Round((totalResponseTime / totalAnswered) / 1000.0, 2) : 0;

                string topCategory = categoryStats.Any()
                    ? categoryStats.OrderByDescending(c => c.Value.Total > 0 ? (double)c.Value.Correct / c.Value.Total : 0)
                                   .FirstOrDefault().Key
                    : "Genel";

                bool isEligible = scorePercentage >= 75;

                Console.WriteLine($"[AI SERVICE LOG] Analiz Başarıyla Bitti. Skor: %{scorePercentage}, Alan: {topCategory}");

                return new AiAnalysisResult
                {
                    PrimarySkill = topCategory,
                    FeedbackSummary = isEligible
                        ? $"Tebrikler! {topCategory} alanında ortalama {avgResponseTimeSec} saniye yanıt süresi ile başarılı oldunuz."
                        : $"Katılımınız için teşekkürler. {topCategory} alanında biraz daha pratik yapabilirsiniz.",
                    IsEligibleForInterview = isEligible,
                    OverallScore = scorePercentage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI SERVICE EXCEPTION] Hata Oluştu: {ex.Message}\n{ex.StackTrace}");
                return new AiAnalysisResult
                {
                    PrimarySkill = "Genel",
                    FeedbackSummary = "Analiz hesaplanırken beklenmeyen bir durum oluştu.",
                    IsEligibleForInterview = false,
                    OverallScore = 0
                };
            }
        }
    }
}
