using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    public class CreateLigneImportationDto
    {
        [Required]
        public int ArticleId { get; set; }

        public TypeDestinationImportation TypeDestination { get; set; } = TypeDestinationImportation.StockLibre;

        public int? CommandeClientId { get; set; }

        public int? ClientId { get; set; }

        public int? PlateformeId { get; set; }

        [StringLength(200)]
        public string? Designation { get; set; }

        [StringLength(50)]
        public string? Couleur { get; set; }

        [StringLength(50)]
        public string? CodeCouleur { get; set; }

        [StringLength(100)]
        public string? Dimension { get; set; }

        [StringLength(100)]
        public string? Nature { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Quantite { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PrixUnitaire { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; }

        [StringLength(50)]
        public string? Unite { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
