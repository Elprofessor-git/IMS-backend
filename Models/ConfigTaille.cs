using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class ConfigTaille
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int Quantite { get; set; }

        public virtual CommandeClient Commande { get; set; } = null!;
    }
}
