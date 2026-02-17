using Moneyball.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Core.Entities;

public class BettingRecommendation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RecommendationId { get; set; }

    public int PredictionId { get; set; }

    public BetType RecommendedBetType { get; set; }

    public int? RecommendedTeamId { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal Edge { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? KellyFraction { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal RecommendedStakePercentage { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MinBankroll { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(PredictionId))]
    public virtual Prediction Prediction { get; set; } = null!;

    [ForeignKey(nameof(RecommendedTeamId))]
    public virtual Team? RecommendedTeam { get; set; }
}