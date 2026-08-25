using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Stock
{
    public class CreateStockDto
    {
        [Required]
        public int ArticleId { get; set; }

        [StringLength(50)]
        public string? Couleur { get; set; }

        [StringLength(50)]
        public string? CodeCouleur { get; set; }

        [StringLength(50)]
        public string? Taille { get; set; }

        [StringLength(100)]
        public string? Dimension { get; set; }

        [StringLength(100)]
        public string? EmplacementPhysique { get; set; }

        [StringLength(50)]
        public string? NumeroLot { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Quantite { get; set; }

        public TypeStock TypeStock { get; set; } = TypeStock.Libre;

        public int? CommandeClientId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PrixUnitaire { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";

        public DateTime? DatePeremption { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? ClientId { get; set; }

        public int? PlateformeId { get; set; }

        public int? GroupeCommandeId { get; set; }
    }
}
