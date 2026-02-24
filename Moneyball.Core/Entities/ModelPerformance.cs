using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Moneyball.Core.Entities;

[Table("ModelPerformances", Schema = "dbo")]
public class ModelPerformance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PerformanceId { get; set; }

    public int ModelId { get; set; }

    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;

    // Metrics from 0.0 to 1.0, 4 decimal precision
    [Column(TypeName = "decimal(5,4)")]
    public decimal Accuracy { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal Precision { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal Recall { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal F1Score { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal AUC { get; set; }

    // LogLoss can be small fractional values, more precision
    [Column(TypeName = "decimal(8,6)")]
    public decimal LogLoss { get; set; }

    // Number of samples
    public int TrainingSamples { get; set; }
    public int ValidationSamples { get; set; }

    // Store feature importance as JSON in the DB
    [Column(TypeName = "nvarchar(max)")]
    public string? FeatureImportanceJson { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal ROI { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ModelId))]
    public virtual Model Model { get; set; } = null!;

    [NotMapped]
    public Dictionary<string, decimal>? FeatureImportance
    {
        get => FeatureImportanceJson == null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>(FeatureImportanceJson);
        set => FeatureImportanceJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }
}