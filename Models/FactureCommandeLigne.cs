using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Models
{
    public class FactureCommandeLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Facture")]
        public int FactureId { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [StringLength(200)]
        public string? Modele { get; set; }

        public int Quantite { get; set; }

        [Precision(18, 4)]
        public decimal PrixUnitaireFacon { get; set; }

        [Precision(18, 4)]
        public decimal MontantLigne { get; set; }

        public virtual Facture Facture { get; set; } = null!;
        public virtual CommandeClient Commande { get; set; } = null!;
    }
}