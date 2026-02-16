using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Data.Entities;

public class ModelPerformance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PerformanceId { get; set; }

    public int ModelId { get; set; }

    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(5,4)")]
    public decimal? Accuracy { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal? ROI { get; set; }

    public int? SampleSize { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Metrics { get; set; } // JSON: precision, recall, F1, AUC, etc.

    // Navigation properties
    [ForeignKey(nameof(ModelId))]
    public virtual Model Model { get; set; } = null!;
}