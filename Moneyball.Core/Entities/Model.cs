using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Moneyball.Core.Enums;

namespace Moneyball.Core.Entities;

/// <summary>
/// Represents a trained ML model stored in the system.
/// Contains metadata and file location for model execution.
/// </summary>
[Table("Models", Schema = "dbo")]
public class Model
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ModelId { get; set; }
    
    /// <summary>
    /// Unique identifier for the model (e.g., "NBA_LogisticRegression_v2")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of model (e.g., "Python", "ML.NET", "ONNX", "TensorFlow")
    /// </summary>
    public ModelType Type { get; set; }

    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public int SportId { get; set; }

    [MaxLength(500)]
    public string? FilePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(max)")]
    public string? Metadata { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    // Navigation properties
    [ForeignKey(nameof(SportId))]
    public virtual Sport Sport { get; set; } = null!;

    public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public virtual ICollection<ModelPerformance> PerformanceRecords { get; set; } = new List<ModelPerformance>();
}