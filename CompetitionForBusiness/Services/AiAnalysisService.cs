using CompetitionForBusiness.Data;
using CompetitionForBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompetitionForBusiness.Services
{
    public class AiAnalysisService
    {
        private readonly AppDbContext _context;

        public AiAnalysisService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AiAnalysisResult> AnalyzeParticipantPerformanceAsync(Guid participantId)
        {
            try
            {
                Console.WriteLine($"[AI SERVICE LOG] Analiz başladı. ID: {participantId}");

                // 1. Kullanıcının cevaplarını ve soru verilerini TEK SORGUMUZA AsNoTracking ile çekiyoruz.
                // Bu yöntem veritabanına sadece 1 hafif istek atar ve bağlantıyı anında kapatır.
                var userAnswers = await _context.UserAnswers
                    .AsNoTracking()
                    .Include(u => u.Question)
                    .Where(u => u.ParticipantId == participantId)
                    .ToListAsync();

                Console.WriteLine($"[AI SERVICE LOG] Çekilen cevap sayısı: {userAnswers.Count}");

                if (userAnswers == null || !userAnswers.Any())
                {
                    return new AiAnalysisResult
                    {
                        PrimarySkill = "Genel",
                        FeedbackSummary = "Henüz cevaplanmış soru bulunmuyor.",
                        IsEligibleForInterview = false,
                        OverallScore = 0
                    };
                }

                // 2. Doğruluk ve Kategori Hesaplamaları
                int totalCorrect = 0;
                double totalResponseTime = 0;
                var categoryStats = new Dictionary<string, (int Correct, int Total)>();

                foreach (var answer in userAnswers)
                {
                    totalResponseTime += answer.ResponseTimeMs;

                    if (answer.Question != null)
                    {
                        string targetOption = answer.Question.CorrectOption ?? string.Empty;
                        string selectedOption = answer.SelectedOption ?? string.Empty;

                        bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                                         selectedOption.Trim().Equals(targetOption.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isCorrect) totalCorrect++;

                        string category = string.IsNullOrWhiteSpace(answer.Question.Category) ? "Genel" : answer.Question.Category;

                        if (!categoryStats.ContainsKey(category))
                            categoryStats[category] = (0, 0);

                        var current = categoryStats[category];
                        categoryStats[category] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                    }
                }

                int totalAnswered = userAnswers.Count;
                int scorePercentage = totalAnswered > 0 ? (int)((double)totalCorrect / totalAnswered * 100) : 0;
                double avgResponseTimeSec = totalAnswered > 0 ? Math.Round((totalResponseTime / totalAnswered) / 1000.0, 2) : 0;

                // En başarılı olunan kategoriyi bulma
                string topCategory = categoryStats.Any()
                    ? categoryStats.OrderByDescending(c => c.Value.Total > 0 ? (double)c.Value.Correct / c.Value.Total : 0)
                                   .FirstOrDefault().Key
                    : "Genel";

                bool isEligible = scorePercentage >= 70;

                Console.WriteLine($"[AI SERVICE LOG] Analiz Başarıyla Tamamlandı. Skor: %{scorePercentage}");

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
                Console.WriteLine($"[AI SERVICE EXCEPTION] {ex.Message}\n{ex.StackTrace}");
                return new AiAnalysisResult
                {
                    PrimarySkill = "Genel",
                    FeedbackSummary = "Analiz esnasında bir hata oluştu.",
                    IsEligibleForInterview = false,
                    OverallScore = 0
                };
            }
        }
    }
}
