using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    public class CreateBesoinCommandeDto
    {
        [Required]
        public int ArticleId { get; set; }

        public TypeBesoin TypeBesoin { get; set; } = TypeBesoin.MatierePremiere;

        [Range(0.001, double.MaxValue)]
        public decimal QuantiteUnitaire { get; set; }

        [Range(1, int.MaxValue)]
        public int NombrePieces { get; set; }

        [StringLength(50)]
        public string? Couleur { get; set; }

        [StringLength(50)]
        public string? Taille { get; set; }

        [StringLength(100)]
        public string? Dimension { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
