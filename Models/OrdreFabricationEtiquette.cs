using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Étiquette / fourniture étiquetée portant le numéro d'OF (codes-barres,
    /// vignettes de composition...). Préoccupation distincte de l'envoi vers une
    /// chaîne (EnvoiFourniture) : c'est le lot d'impression d'un OF, d'où l'ancrage
    /// OrdreFabricationId en tête. FournitureCommandeLigneId/nomenclature reste
    /// optionnel : une étiquette peut être rattachée à une fourniture de la commande.
    /// </summary>
    public class OrdreFabricationEtiquette
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreFabrication")]
        public int OrdreFabricationId { get; set; }

        /// <summary>Ligne de fourniture d'origine, quand l'étiquette matérialise une fourniture
        /// existante de la commande (nullable : étiquette d'OF « autonome »).</summary>
        [ForeignKey("FournitureLigne")]
        public int? FournitureCommandeLigneId { get; set; }

        [StringLength(50)]
        public string? Taille { get; set; }

        public int QuantiteEtiquettes { get; set; }

        public DateTime DateImpression { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual OrdreFabrication OrdreFabrication { get; set; } = null!;
        public virtual FournitureCommandeLigne? FournitureLigne { get; set; }
    }
}