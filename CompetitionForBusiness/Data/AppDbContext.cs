using CompetitionForBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace CompetitionForBusiness.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Room> Rooms { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Supabase (PostgreSQL) tablo isimleri ile C# DbSet'lerini eşleştiriyoruz:
            modelBuilder.Entity<Question>().ToTable("questions");
            modelBuilder.Entity<Participant>().ToTable("participants");
            modelBuilder.Entity<UserAnswer>().ToTable("user_answers");
            modelBuilder.Entity<Room>().ToTable("rooms");

            // UserAnswer tablosunda Id Identity (Auto Increment) ise EF Core'a bildiriyoruz
            modelBuilder.Entity<UserAnswer>()
                .Property(ua => ua.Id)
                .ValueGeneratedOnAdd();
        }
    }
}
