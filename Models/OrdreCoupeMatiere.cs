using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Matière d'un ordre de coupe : liste libre (tissu, thermocollant, doublure…).
    /// Seules les entrées sont stockées ; tout ce qui en découle est calculé à la
    /// lecture et n'est donc jamais périmé en base :
    ///
    ///   utilisés    = total planifié de l'ordre × <see cref="ConsoClient"/>
    ///   retrait     = <see cref="RetraitPourcentage"/> % × <see cref="MetresRecus"/>
    ///   stock       = <see cref="MetresRecus"/> − utilisés − retrait
    ///   alerte      = stock &lt; 0 (affichée en rouge)
    ///
    /// <see cref="ConsoReelle"/> est informative : elle affiche l'écart constaté avec
    /// la consommation client SANS entrer dans le calcul. À ne pas confondre avec
    /// <c>RapportCoupeTissuDto.ConsoNominale</c> (nomenclature BOM), renommé pour
    /// cette raison.
    /// </summary>
    public class OrdreCoupeMatiere
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreCoupe")]
        public int OrdreCoupeId { get; set; }

        [Required]
        [StringLength(200)]
        public string Designation { get; set; } = string.Empty;

        /// <summary>Laize (cm). NULL pour une matière sans laize.</summary>
        public decimal? Laize { get; set; }

        /// <summary>Consommation client par pièce (m/pièce) — celle du calcul.</summary>
        public decimal ConsoClient { get; set; }

        /// <summary>Consommation réelle constatée (m/pièce) — informative, avec écart.</summary>
        public decimal? ConsoReelle { get; set; }

        /// <summary>Retrait en % des mètres reçus.</summary>
        public decimal RetraitPourcentage { get; set; }

        /// <summary>Mètres reçus (m).</summary>
        public decimal MetresRecus { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public virtual OrdreCoupe OrdreCoupe { get; set; } = null!;
    }
}
