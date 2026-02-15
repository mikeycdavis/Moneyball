using Microsoft.EntityFrameworkCore;
using Moneyball.Domain.NBA;

namespace Moneyball.Infrastructure.NBA
{
    public class NbaEdgeDbContext : DbContext
    {
        public DbSet<NbaGame> Games => Set<NbaGame>();
        public DbSet<NbaTeam> Teams => Set<NbaTeam>();
        public DbSet<NbaFeatureSet> Features => Set<NbaFeatureSet>();
        public DbSet<NbaPrediction> Predictions => Set<NbaPrediction>();

        public NbaEdgeDbContext(DbContextOptions<NbaEdgeDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NbaFeatureSet>()
                .HasKey(f => f.GameId);
        }
    }
}
