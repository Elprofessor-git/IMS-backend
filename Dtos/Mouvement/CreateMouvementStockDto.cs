using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Mouvement
{
    public class CreateMouvementStockDto
    {
        [Required]
        public int StockId { get; set; }

        public TypeMouvement TypeMouvement { get; set; }

        public OrigineMouvement OrigineMouvement { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Quantite { get; set; }

        [StringLength(100)]
        public string? EmplacementSource { get; set; }

        [StringLength(100)]
        public string? EmplacementDestination { get; set; }

        [StringLength(100)]
        public string? NumeroReference { get; set; }

        [StringLength(1000)]
        public string? Motif { get; set; }

        [StringLength(100)]
        public string? EffectuePar { get; set; }
    }
}
