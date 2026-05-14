using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Marque
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [ForeignKey("Plateforme")]
        public int PlateformeId { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool EstActive { get; set; } = true;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public virtual Plateforme Plateforme { get; set; } = null!;
        public virtual ICollection<CommandeClient> Commandes { get; set; } = new List<CommandeClient>();
    }
}
