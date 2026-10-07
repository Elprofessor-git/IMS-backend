using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Colonne de taille d'un ordre de coupe : jusqu'à 8 colonnes, libellé libre
    /// (« 0 »..« 4 », « 34 »..« 46 », « TU »…), donc détaché de ConfigTaille — qui
    /// reste inchangée et reste la configuration de la commande.
    ///
    /// Les deux quantités sont STOCKÉES, jamais dérivées à la lecture :
    ///   • <see cref="QuantiteDemandee"/> : la demande, graine de l'échelle de restes ;
    ///   • <see cref="QuantiteAvecMarge"/> : la demande augmentée de la marge, colonne
    ///     d'accompagnement affichée en tête de colonne.
    /// Une cellule saisie à la main n'est donc jamais écrasée par un recalcul ni par
    /// un second préremplissage : Preremplir ne remplit que les cellules NULL, et la
    /// taille à 0 (libellé comme quantité) est admise.
    /// </summary>
    public class OrdreCoupeTaille
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreCoupe")]
        public int OrdreCoupeId { get; set; }

        /// <summary>Position de la colonne (0..7).</summary>
        public int Index { get; set; }

        /// <summary>Libellé de la colonne, libre.</summary>
        [Required]
        [StringLength(50)]
        public string Libelle { get; set; } = string.Empty;

        /// <summary>Quantité demandée. NULL = cellule non remplie (jamais devinée).</summary>
        public int? QuantiteDemandee { get; set; }

        /// <summary>Quantité avec marge. NULL = cellule non remplie.</summary>
        public decimal? QuantiteAvecMarge { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        public virtual OrdreCoupe OrdreCoupe { get; set; } = null!;
    }
}
