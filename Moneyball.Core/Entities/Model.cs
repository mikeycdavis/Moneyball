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

    /// <summary>
    /// Version number for model tracking
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Description of the model
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Sport id this model is trained for (foreign key to Sports table)
    /// </summary>
    public int SportId { get; set; }

    /// <summary>
    /// File path to the trained model file
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// Whether this model is currently active for predictions
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Feature names expected by this model (JSON serialized array)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Metadata { get; set; }

    /// <summary>
    /// When the model was trained
    /// </summary>
    public DateTime TrainedAt { get; set; }

    /// <summary>
    /// Who trained the model (user or system)
    /// </summary>
    public string TrainedBy { get; set; } = "System";

    /// <summary>
    /// Audit fields
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(SportId))]
    public virtual Sport Sport { get; set; } = null!;

    public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public virtual ICollection<ModelPerformance> PerformanceRecords { get; set; } = new List<ModelPerformance>();
}