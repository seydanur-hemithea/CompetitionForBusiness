namespace CompetitionForBusiness.Models
{
    public class AiAnalysisResult
    {
        public string PrimarySkill { get; set; } = string.Empty; // Örn: Veri Bilimi
        public string FeedbackSummary { get; set; } = string.Empty; // Adaya gösterilecek metin
        public bool IsEligibleForInterview { get; set; } // Mülakata davet edilsin mi?
        public int OverallScore { get; set; } // Toplam Başarı Yüzdesi
    }
}
