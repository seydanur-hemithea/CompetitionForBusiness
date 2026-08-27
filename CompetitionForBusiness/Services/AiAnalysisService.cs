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

        if (!userAnswers.Any())
        {
            return new AiAnalysisResult
            {
                PrimarySkill = "Genel",
                FeedbackSummary = "Henüz cevaplanmış soru bulunmuyor.",
                IsEligibleForInterview = false,
                OverallScore = 0
            };
        }

        // 2. Kilitlenmeyi önlemek için ToListAsync() alıp hafızada Dictionary yapıyoruz
        var questionIds = userAnswers.Select(u => u.QuestionId).Distinct().ToList();
        
        var questionsList = await _context.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .ToListAsync(); // ToDictionaryAsync yerine ToListAsync kullanıyoruz

        var questions = questionsList.ToDictionary(q => q.Id);

        Console.WriteLine($"[AI SERVICE LOG] Sorular çekildi: {questions.Count} adet.");

        // 3. Hesaplamalar
        int totalCorrect = 0;
        double totalResponseTime = 0;
        var categoryStats = new Dictionary<string, (int Correct, int Total)>();

        foreach (var answer in userAnswers)
        {
            totalResponseTime += answer.ResponseTimeMs;

            if (questions.TryGetValue(answer.QuestionId, out var question))
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

        int scorePercentage = (int)((double)totalCorrect / userAnswers.Count * 100);
        double avgResponseTimeSec = Math.Round((totalResponseTime / userAnswers.Count) / 1000.0, 2);

        string topCategory = categoryStats.Any()
            ? categoryStats.OrderByDescending(c => c.Value.Total > 0 ? (double)c.Value.Correct / c.Value.Total : 0)
                           .FirstOrDefault().Key
            : "Genel";

        bool isEligible = scorePercentage >= 75;

        Console.WriteLine($"[AI SERVICE LOG] Analiz Bitti. Skor: {scorePercentage}");

        return new AiAnalysisResult
        {
            PrimarySkill = topCategory,
            FeedbackSummary = isEligible
                ? $"Tebrikler! {topCategory} alanında başarılı bir performans sergilediniz."
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
            FeedbackSummary = "Analiz hesaplanırken bir zaman aşımı oluştu.",
            IsEligibleForInterview = false,
            OverallScore = 0
        };
    }
}
    }
}
