using Microsoft.EntityFrameworkCore;
using Moneyball.Data.Entities;

namespace Moneyball.Data;

public class MoneyballDbContext(DbContextOptions<MoneyballDbContext> options) : DbContext(options)
{
    public DbSet<Sport> Sports { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<GameOdds> GameOdds { get; set; }
    public DbSet<TeamStatistic> TeamStatistics { get; set; }
    public DbSet<Model> Models { get; set; }
    public DbSet<ModelPerformance> ModelPerformances { get; set; }
    public DbSet<Prediction> Predictions { get; set; }
    public DbSet<BettingRecommendation> BettingRecommendations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sport configuration
        modelBuilder.Entity<Sport>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Team configuration
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasIndex(e => new { e.SportId, e.ExternalId });
            entity.HasIndex(e => e.Name);
        });

        // Game configuration
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(e => e.GameDate);
            entity.HasIndex(e => new { e.SportId, e.GameDate });
            entity.HasIndex(e => e.ExternalGameId);
            entity.HasIndex(e => new { e.Status, e.GameDate });

            entity.HasOne(e => e.HomeTeam)
                .WithMany(t => t.HomeGames)
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AwayTeam)
                .WithMany(t => t.AwayGames)
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // GameOdds configuration
        modelBuilder.Entity<GameOdds>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.RecordedAt });
            entity.HasIndex(e => e.BookmakerName);
        });

        // TeamStatistic configuration
        modelBuilder.Entity<TeamStatistic>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.TeamId });
        });

        // Model configuration
        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.SportId, e.IsActive });
        });

        // Prediction configuration
        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.ModelId });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.ModelId, e.CreatedAt });
        });

        // BettingRecommendation configuration
        modelBuilder.Entity<BettingRecommendation>(entity =>
        {
            entity.HasIndex(e => new { e.Edge, e.CreatedAt });
        });

        // Seed initial sports
        modelBuilder.Entity<Sport>().HasData(
            new Sport { SportId = 1, Name = "NBA", IsActive = true },
            new Sport { SportId = 2, Name = "NFL", IsActive = true },
            new Sport { SportId = 3, Name = "NHL", IsActive = true },
            new Sport { SportId = 4, Name = "MLB", IsActive = true }
        );
    }
}