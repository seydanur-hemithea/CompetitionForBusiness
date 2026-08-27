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
                Console.WriteLine($"[AI SERVICE LOG] Analiz süreci başladı. ParticipantID: {participantId}");

                // 1. Adayın verdiği cevapları çekiyoruz
                var userAnswers = await _context.UserAnswers
                    .AsNoTracking()
                    .Where(u => u.ParticipantId == participantId)
                    .ToListAsync();

                Console.WriteLine($"[AI SERVICE LOG] Veritabanından çekilen cevap sayısı: {userAnswers?.Count ?? 0}");

                if (userAnswers == null || !userAnswers.Any())
                {
                    Console.WriteLine("[AI SERVICE LOG] Katılımcıya ait cevap bulunamadı.");
                    return new AiAnalysisResult
                    {
                        PrimarySkill = "Genel",
                        FeedbackSummary = "Henüz analiz edilecek yanıt verisi bulunamadı.",
                        IsEligibleForInterview = false,
                        OverallScore = 0
                    };
                }

                // 2. İlgili soruları çekiyoruz
                var questionIds = userAnswers.Select(u => u.QuestionId).Distinct().ToList();
                Console.WriteLine($"[AI SERVICE LOG] Aranacak benzersiz soru sayısı: {questionIds.Count}");

                var questions = await _context.Questions
                    .AsNoTracking()
                    .Where(q => questionIds.Contains(q.Id))
                    .ToDictionaryAsync(q => q.Id);

                Console.WriteLine($"[AI SERVICE LOG] Eşleşen soru sayısı: {questions.Count}");

                // 3. Kategori bazlı başarı ve süre analizi
                int totalCorrect = 0;
                double totalResponseTime = 0;
                var categoryStats = new Dictionary<string, (int Correct, int Total)>();

                foreach (var answer in userAnswers)
                {
                    totalResponseTime += answer.ResponseTimeMs;

                    if (questions.TryGetValue(answer.QuestionId, out var question))
                    {
                        // Null-safe doğru cevap kontrolü
                        string targetCorrectOption = question.CorrectOption ?? string.Empty;
                        string userSelectedOption = answer.SelectedOption ?? string.Empty;

                        bool isCorrect = !string.IsNullOrEmpty(userSelectedOption) &&
                                         userSelectedOption.Trim().Equals(targetCorrectOption.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isCorrect) totalCorrect++;

                        string category = string.IsNullOrWhiteSpace(question.Category) ? "Genel" : question.Category;

                        if (!categoryStats.ContainsKey(category))
                        {
                            categoryStats[category] = (0, 0);
                        }

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

                Console.WriteLine($"[AI SERVICE LOG] Analiz tamamlandı -> Skor: %{scorePercentage}, Baskın Alan: {topCategory}");

                return new AiAnalysisResult
                {
                    PrimarySkill = topCategory,
                    FeedbackSummary = isEligible
                        ? $"Tebrikler! Özellikle {topCategory} alanındaki sorulara verdiğiniz ortalama {avgResponseTimeSec} saniyelik hızlı ve doğru yanıtlarla öne çıktınız."
                        : $"Katılımınız için teşekkürler. {topCategory} alanındaki sorulara ilgi gösterdiniz, ancak mülakat barajı için biraz daha pratiğe ihtiyacınız var.",
                    IsEligibleForInterview = isEligible,
                    OverallScore = scorePercentage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI SERVICE EXCEPTION] Analiz Hatalı: {ex.Message}\n{ex.StackTrace}");

                // Servis patlasa bile uygulamanın kilitlenmemesi için fallback nesne dönüyoruz
                return new AiAnalysisResult
                {
                    PrimarySkill = "Genel",
                    FeedbackSummary = "Analiz oluşturulurken bir hata meydana geldi.",
                    IsEligibleForInterview = false,
                    OverallScore = 0
                };
            }
        }
    }
}
