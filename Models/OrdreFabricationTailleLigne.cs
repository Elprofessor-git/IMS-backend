using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>Ligne de répartition par taille d'un OF (indépendante des autres OF de la commande).</summary>
    public class OrdreFabricationTailleLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreFabrication")]
        public int OrdreFabricationId { get; set; }

        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int Quantite { get; set; }

        public virtual OrdreFabrication OrdreFabrication { get; set; } = null!;
    }
}