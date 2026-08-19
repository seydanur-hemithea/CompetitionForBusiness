using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompetitionForBusiness.Models
{
    [Table("rooms")]
    public class Room
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("current_question_id")]
        public int CurrentQuestionId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
