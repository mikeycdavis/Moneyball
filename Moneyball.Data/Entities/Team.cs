using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Data.Entities
{
    public class Team
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TeamId { get; set; }

        public int SportId { get; set; }

        [MaxLength(50)]
        public string? ExternalId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Abbreviation { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Conference { get; set; }

        [MaxLength(100)]
        public string? Division { get; set; }

        // Navigation properties
        [ForeignKey(nameof(SportId))]
        public virtual Sport Sport { get; set; } = null!;

        public virtual ICollection<Game> HomeGames { get; set; } = new List<Game>();
        public virtual ICollection<Game> AwayGames { get; set; } = new List<Game>();
    }
}
