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

        /// <summary>
        /// Matelas dont est issue cette coupe (enveloppe de coupe — module Fournitures).
        /// Nullable et additif : les coupes existantes restent valides sans matelas.
        /// La suppression d'un matelas met la référence à NULL (SetNull) — l'historique
        /// des coupes n'est jamais supprimé avec le matelas.
        /// </summary>
        [ForeignKey("Matelas")]
        public int? MatelasId { get; set; }

        /// <summary>
        /// Ordre de fabrication dont relève cette coupe (module OF — Phase 1).
        /// Nullable et additif : les coupes existantes restent valides sans OF.
        /// La suppression d'un OF met la référence à NULL (SetNull) — l'historique
        /// des coupes n'est jamais supprimé avec l'OF.
        /// </summary>
        [ForeignKey("OrdreFabrication")]
        public int? OrdreFabricationId { get; set; }

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
        public virtual Matelas? Matelas { get; set; }
        public virtual OrdreFabrication? OrdreFabrication { get; set; }
    }
}