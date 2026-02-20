using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Moneyball.Core.Entities
{
    [Table("Sports", Schema = "dbo")]
    public class Sport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SportId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
        public virtual ICollection<Game> Games { get; set; } = new List<Game>();
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
    }
}
