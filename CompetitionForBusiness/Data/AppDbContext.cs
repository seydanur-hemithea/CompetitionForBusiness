using CompetitionForBusiness.Models;

using Microsoft.EntityFrameworkCore;

namespace CompetitionForBusiness.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Room> Rooms { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }
    }


}
