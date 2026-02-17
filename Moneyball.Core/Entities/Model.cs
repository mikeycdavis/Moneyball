using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Moneyball.Core.Enums;

namespace Moneyball.Core.Entities;

public class Model
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ModelId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public int SportId { get; set; }

    public ModelType ModelType { get; set; }

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