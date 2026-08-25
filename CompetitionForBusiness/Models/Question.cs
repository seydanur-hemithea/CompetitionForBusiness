using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompetitionForBusiness.Models
{
    [Table("questions")] // Supabase tablo adı (küçük harf)
    public class Question
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("question_text")]
        public string QuestionText { get; set; } = string.Empty;

        [Column("option_a")]
        public string OptionA { get; set; } = string.Empty;

        [Column("option_b")]
        public string OptionB { get; set; } = string.Empty;

        [Column("option_c")]
        public string OptionC { get; set; } = string.Empty;

        [Column("option_d")]
        public string OptionD { get; set; } = string.Empty;

        [Column("correct_option")]
        public string CorrectOption { get; set; } = string.Empty;

        [Column("category")]
        public string Category { get; set; } = string.Empty;
    }
}
