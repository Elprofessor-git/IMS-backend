using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ordre de coupe d'une commande, au grain (commande, modèle, couleur) : un ordre
    /// par couple, exactement comme une feuille du classeur « ORDRE DE COUPE-2026 ».
    ///
    /// Ce que l'entité porte, volontairement :
    ///   • l'en-tête saisi par l'atelier (modèle, couleur, chaîne, marge, OF de référence) ;
    ///   • ses colonnes de tailles (jusqu'à 8), SES quantités et SES plans : tout est
    ///     stocké tel que saisi. Une cellule modifiée à la main n'est jamais réécrite par
    ///     un recalcul — les restes et le résumé sont calculés à la lecture.
    ///
    /// Ce que l'entité ne porte PAS :
    ///   • les restes par taille (calculs de <see cref="Services.Coupe.MoteurCoupe"/>) ;
    ///   • le résumé (commandé / planifié / surplus / manque) ;
    ///   • les grandeurs dérivées des matières (utilisés, retrait, stock restant).
    ///
    /// Le préremplissage est un acte explicite (POST /{id}/Preremplir) : aucune
    /// migration, aucune lecture automatique ne remplit une cellule déjà saisie.
    /// </summary>
    public class OrdreCoupe
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("CommandeClient")]
        public int CommandeClientId { get; set; }

        /// <summary>Modèle (libellé libre, ex. « PDJ22 D676 »).</summary>
        [Required]
        [StringLength(100)]
        public string Modele { get; set; } = string.Empty;

        /// <summary>Couleur (libellé libre, ex. « 6001-ECRU rosé »).</summary>
        [Required]
        [StringLength(50)]
        public string Couleur { get; set; } = string.Empty;

        /// <summary>Chaîne de production assignée (interne ou sous-traitant).</summary>
        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        /// <summary>
        /// Marge de sécurité de l'ordre, en %. NULL = la marge par défaut de la
        /// commande fait foi ; renseignée, elle remplace la valeur de la commande.
        /// </summary>
        public decimal? MargeSecurite { get; set; }

        /// <summary>Numéros d'OF en référence (liste libre, ex. « OF-12, OF-18 »).</summary>
        [StringLength(500)]
        public string? ReferenceOF { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime? DateMiseAJour { get; set; }

        [StringLength(100)]
        public string? CreePar { get; set; }

        [StringLength(100)]
        public string? ModifiePar { get; set; }

        public virtual CommandeClient CommandeClient { get; set; } = null!;
        public virtual ChaineProduction? ChaineProduction { get; set; }
        public virtual ICollection<OrdreCoupeTaille> Tailles { get; set; } = new List<OrdreCoupeTaille>();
        public virtual ICollection<OrdreCoupePlan> Plans { get; set; } = new List<OrdreCoupePlan>();
        public virtual ICollection<OrdreCoupeMatiere> Matieres { get; set; } = new List<OrdreCoupeMatiere>();
    }
}
