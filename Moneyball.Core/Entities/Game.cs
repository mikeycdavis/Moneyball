using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Moneyball.Core.Enums;

namespace Moneyball.Core.Entities
{
    [Table("Games", Schema = "dbo")]
    public class Game
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GameId { get; set; }

        public int SportId { get; set; }

        [MaxLength(50)]
        public string? ExternalGameId { get; set; }

        public int HomeTeamId { get; set; }

        public int AwayTeamId { get; set; }

        [Required]
        public DateTime GameDate { get; set; }

        public int? HomeScore { get; set; }

        public int? AwayScore { get; set; }

        public GameStatus Status { get; set; } = GameStatus.Scheduled;

        public bool IsComplete { get; set; }

        [MaxLength(50)]
        public string? Season { get; set; }

        public int? Week { get; set; } // For NFL

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(SportId))]
        public virtual Sport Sport { get; set; } = null!;

        [ForeignKey(nameof(HomeTeamId))]
        public virtual Team HomeTeam { get; set; } = null!;

        [ForeignKey(nameof(AwayTeamId))]
        public virtual Team AwayTeam { get; set; } = null!;

        public virtual ICollection<Odds> Odds { get; set; } = new List<Odds>();
        public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public virtual ICollection<TeamStatistic> TeamStatistics { get; set; } = new List<TeamStatistic>();
    }
}
