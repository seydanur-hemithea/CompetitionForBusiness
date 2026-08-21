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
            // 1. Adayın verdiği cevapları AsNoTracking ile hızlıca çekiyoruz
            var userAnswers = await _context.UserAnswers
                .AsNoTracking()
                .Where(u => u.ParticipantId == participantId)
                .ToListAsync();

            // Cevap verisi yoksa veritabanına ikinci sorguyu atmadan direkt dönüyoruz
            if (userAnswers == null || !userAnswers.Any())
            {
                return new AiAnalysisResult
                {
                    PrimarySkill = "Genel",
                    FeedbackSummary = "Yeterli cevap verisi bulunamadı.",
                    IsEligibleForInterview = false,
                    OverallScore = 0
                };
            }

            // 2. İlgili soruları çekiyoruz (AsNoTracking eklendi)
            var questionIds = userAnswers.Select(u => u.QuestionId).Distinct().ToList();
            
            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            // 3. Kategori bazlı başarı ve süre analizini hesaplıyoruz
            int totalCorrect = 0;
            double totalResponseTime = 0;
            var categoryStats = new Dictionary<string, (int Correct, int Total)>();

            foreach (var answer in userAnswers)
            {
                totalResponseTime += answer.ResponseTimeMs;

                if (questions.TryGetValue(answer.QuestionId, out var question))
                {
                    bool isCorrect = !string.IsNullOrEmpty(answer.SelectedOption) &&
                                     answer.SelectedOption.Equals(question.CorrectOption, StringComparison.OrdinalIgnoreCase);
                    
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

            int scorePercentage = (int)((double)totalCorrect / userAnswers.Count * 100);
            double avgResponseTimeSec = Math.Round((totalResponseTime / userAnswers.Count) / 1000.0, 2);

            // 4. Kategori özeti
            var categorySummary = string.Join(", ", categoryStats.Select(c => $"{c.Key}: %{(double)c.Value.Correct / c.Value.Total * 100:F0}"));

            // Dinamik sonuç üretimi
            string topCategory = categoryStats
                .OrderByDescending(c => (double)c.Value.Correct / c.Value.Total)
                .FirstOrDefault().Key ?? "Genel";

            bool isEligible = scorePercentage >= 75;

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
    }
}
