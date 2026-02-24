using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;

namespace Moneyball.Infrastructure.Repositories;

public class MoneyballDbContext(DbContextOptions<MoneyballDbContext> options) : DbContext(options)
{
    public DbSet<Sport> Sports { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<Odds> Odds { get; set; }
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

            entity.Property(e => e.Name)
                .HasConversion<string>();
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

            entity.Property(e => e.Status)
                .HasConversion<string>();

            // Preserve Team records when a Game is deleted
            entity.HasOne(e => e.HomeTeam)
                .WithMany(t => t.HomeGames)
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // Preserve Team records when a Game is deleted
            entity.HasOne(e => e.AwayTeam)
                .WithMany(t => t.AwayGames)
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // GameOdds configuration
        modelBuilder.Entity<Odds>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.RecordedAt });
            entity.HasIndex(e => e.BookmakerName);
        });

        // TeamStatistic configuration
        modelBuilder.Entity<TeamStatistic>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.TeamId });

            // Preserve Team records when a TeamStatistic is deleted
            entity.HasOne(p => p.Team)
                .WithMany()
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Model configuration
        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.SportId, e.IsActive });

            entity.Property(e => e.Type)
                .HasConversion<string>();
        });

        // Prediction configuration
        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.ModelId });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.ModelId, e.CreatedAt });

            // Preserve Model records when a Prediction is deleted
            entity.HasOne(p => p.Model)
                .WithMany(p => p.Predictions)
                .HasForeignKey(p => p.ModelId)
                .OnDelete(DeleteBehavior.NoAction);

            // Preserve Game records when a Prediction is deleted
            entity.HasOne(p => p.Game)
                .WithMany(p => p.Predictions)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // BettingRecommendation configuration
        modelBuilder.Entity<BettingRecommendation>(entity =>
        {
            entity.HasIndex(e => new { e.Edge, e.CreatedAt });

            // Preserve Team records when a BettingRecommendation is deleted
            entity.HasOne(p => p.RecommendedTeam)
                .WithMany()
                .HasForeignKey(p => p.RecommendedTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cascade delete BettingRecommendations when a Prediction is deleted
            entity.HasOne(br => br.Prediction)
                .WithMany(p => p.Recommendations)
                .HasForeignKey(br => br.PredictionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial sports
        modelBuilder.Entity<Sport>().HasData(
            new Sport { SportId = 1, Name = SportType.NBA, IsActive = true },
            new Sport { SportId = 2, Name = SportType.NFL, IsActive = true },
            new Sport { SportId = 3, Name = SportType.NHL, IsActive = true },
            new Sport { SportId = 4, Name = SportType.MLB, IsActive = true }
        );
    }
}