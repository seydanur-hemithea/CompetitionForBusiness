using CompetitionForBusiness.Data;
using CompetitionForBusiness.Models;

using Microsoft.EntityFrameworkCore;

using System;

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
            // 1. Adayın verdiği tüm cevapları ve soru bilgilerini çekiyoruz
            var userAnswers = await _context.UserAnswers
                .Where(u => u.ParticipantId == participantId)
                .ToListAsync();

            var questionIds = userAnswers.Select(u => u.QuestionId).ToList();
            var questions = await _context.Questions
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            if (!userAnswers.Any())
            {
                return new AiAnalysisResult
                {
                    PrimarySkill = "Genel",
                    FeedbackSummary = "Yeterli cevap verisi bulunamadı.",
                    IsEligibleForInterview = false,
                    OverallScore = 0
                };
            }

            // 2. Kategori bazlı başarı ve süre analizini hesaplıyoruz
            int totalCorrect = 0;
            double totalResponseTime = 0;
            var categoryStats = new Dictionary<string, (int Correct, int Total)>();

            foreach (var answer in userAnswers)
            {
                totalResponseTime += answer.ResponseTimeMs;

                if (questions.TryGetValue(answer.QuestionId, out var question))
                {
                    bool isCorrect = answer.SelectedOption.Equals(question.CorrectOption, StringComparison.OrdinalIgnoreCase);
                    if (isCorrect) totalCorrect++;

                    if (!categoryStats.ContainsKey(question.Category))
                    {
                        categoryStats[question.Category] = (0, 0);
                    }

                    var current = categoryStats[question.Category];
                    categoryStats[question.Category] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                }
            }

            int scorePercentage = (int)((double)totalCorrect / userAnswers.Count * 100);
            double avgResponseTimeSec = Math.Round((totalResponseTime / userAnswers.Count) / 1000.0, 2);

            // 3. Yapay Zekaya (LLM) Gönderilecek Prompt Hazırlığı
            var categorySummary = string.Join(", ", categoryStats.Select(c => $"{c.Key}: %{(double)c.Value.Correct / c.Value.Total * 100:F0}"));

            string prompt = $@"
        Aşağıda bir yarışmacının teknik test performansı verilmiştir:
        - Toplam Doğru Yüzdesi: %{scorePercentage}
        - Ortalama Yanıt Süresi: {avgResponseTimeSec} saniye
        - Kategori Başarıları: {categorySummary}

        GÖREV:
        1. Adayın en yetkin/baskın olduğu uzmanlık alanını belirle.
        2. Adaya sınav sonunda gösterilmek üzere 2 cümlelik motivasyonel ve yapıcı bir değerlendirme özeti yaz.
        3. Eğer genel başarı %75 üzerindeyse ve yanıt süresi hızlıysa mülakat durumunu true yap.

        Lütfen cevabı SADECE şu JSON formatında dön:
        {{
          ""primarySkill"": ""Baskın Alan Adı"",
          ""feedbackSummary"": ""Adaya gösterilecek özet metin..."",
          ""isEligibleForInterview"": true/false
        }}";

            // NOT: Buradan itibaren isteği OpenAI / Gemini API'sine gönderebilirsiniz.
            // Şimdilik sistemin sorunsuz çalışması için kural tabanlı dinamik yanıt üretiyoruz:

            string topCategory = categoryStats.OrderByDescending(c => (double)c.Value.Correct / c.Value.Total).FirstOrDefault().Key ?? "Yazılım";
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
