using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class LotCoupe
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int QuantiteCoupee { get; set; }

        public DateTime DateCoupe { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        public bool ForcerDepassement { get; set; } = false;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public virtual CommandeClient Commande { get; set; } = null!;
    }
}