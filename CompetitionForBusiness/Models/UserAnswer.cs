using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompetitionForBusiness.Models
{
    [Table("user_answers")]
    public class UserAnswer
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("participant_id")]
        public Guid ParticipantId { get; set; }

        [Column("question_id")]
        public int QuestionId { get; set; }

        [Column("selected_option")]
        public string SelectedOption { get; set; } = string.Empty;

        [Column("response_time_ms")]
        public int ResponseTimeMs { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
