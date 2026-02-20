using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Core.Entities;

/// <summary>
/// Stores team-level statistics for a specific game
/// </summary>
[Table("TeamStatistics", Schema = "dbo")]
public class TeamStatistic
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TeamStatisticId { get; set; }

    public int GameId { get; set; }

    public int TeamId { get; set; }

    public bool IsHomeTeam { get; set; }

    // Common stats across sports
    public int? Points { get; set; }

    // Basketball-specific stats
    public int? FieldGoalsMade { get; set; }
    public int? FieldGoalsAttempted { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? FieldGoalPercentage { get; set; }
    public int? ThreePointsMade { get; set; }
    public int? ThreePointsAttempted { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? ThreePointPercentage { get; set; }
    public int? FreeThrowsMade { get; set; }
    public int? FreeThrowsAttempted { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? FreeThrowPercentage { get; set; }
    public int? Rebounds { get; set; }
    public int? OffensiveRebounds { get; set; }
    public int? DefensiveRebounds { get; set; }
    public int? Assists { get; set; }
    public int? Steals { get; set; }
    public int? Blocks { get; set; }
    public int? Turnovers { get; set; }
    public int? PersonalFouls { get; set; }

    // Football-specific stats
    public int? PassingYards { get; set; }
    public int? RushingYards { get; set; }
    public int? TotalYards { get; set; }
    public int? Touchdowns { get; set; }
    public int? Interceptions { get; set; }
    public int? Fumbles { get; set; }
    public int? Sacks { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TimeOfPossession { get; set; }

    // Additional flexible stats stored as JSON
    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalStats { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;
}