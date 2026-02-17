using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Core.Entities;

public class Prediction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PredictionId { get; set; }

    public int ModelId { get; set; }

    public int GameId { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal PredictedHomeWinProbability { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal PredictedAwayWinProbability { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal? Edge { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? Confidence { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PredictedHomeScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PredictedAwayScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PredictedTotal { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? FeatureValues { get; set; } // JSON of features used

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(ModelId))]
    public virtual Model Model { get; set; } = null!;

    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<BettingRecommendation> Recommendations { get; set; } = new List<BettingRecommendation>();
}