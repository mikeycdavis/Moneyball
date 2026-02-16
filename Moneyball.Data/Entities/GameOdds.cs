using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Data.Entities;

public class GameOdds
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OddsId { get; set; }

    public int GameId { get; set; }

    [MaxLength(100)]
    public string BookmakerName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? HomeMoneyline { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? AwayMoneyline { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? HomeSpread { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? AwaySpread { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? HomeSpreadOdds { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? AwaySpreadOdds { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverUnder { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? OverOdds { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? UnderOdds { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;
}