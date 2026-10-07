using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Plan (matelas) d'un ordre de coupe : jusqu'à 9 lignes, chacune = nombre de
    /// plis + occurrences par taille. La quantité d'une cellule vaut Plis ×
    /// Occurrences ; le RESTE par taille affiché sous chaque plan n'est PAS stocké,
    /// il est recalculé à la lecture par <see cref="Services.Coupe.MoteurCoupe"/>.
    ///
    /// <see cref="MatelasId"/> rattache optionnellement la ligne à un matelas suivi
    /// par l'atelier : c'est par ce lien que s'applique le verrou existant — un plan
    /// est figé (409) dès que des coupes réelles sont enregistrées sur le matelas.
    /// </summary>
    public class OrdreCoupePlan
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreCoupe")]
        public int OrdreCoupeId { get; set; }

        /// <summary>Position de la ligne (0..8).</summary>
        public int Index { get; set; }

        /// <summary>Repère affiché (ex. « M1 », « nappe 2 »). Libre.</summary>
        [StringLength(100)]
        public string? Libelle { get; set; }

        /// <summary>Nombre de plis de la passe.</summary>
        public int Plis { get; set; }

        /// <summary>Matelas suivi rattaché à ce plan (optionnel, SetNull).</summary>
        [ForeignKey("Matelas")]
        public int? MatelasId { get; set; }

        public virtual OrdreCoupe OrdreCoupe { get; set; } = null!;
        public virtual Matelas? Matelas { get; set; }
        public virtual ICollection<OrdreCoupePlanOccurrence> Occurrences { get; set; } = new List<OrdreCoupePlanOccurrence>();
    }

    /// <summary>
    /// Occurrence d'une taille sur un plan : une ligne par gabarit dessiné.
    /// Quantité théorique de la cellule = Occurrences × Plis du plan parent.
    /// </summary>
    public class OrdreCoupePlanOccurrence
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreCoupePlan")]
        public int OrdreCoupePlanId { get; set; }

        /// <summary>Libellé de la taille, identique à celui de la colonne.</summary>
        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int Occurrences { get; set; }

        public virtual OrdreCoupePlan OrdreCoupePlan { get; set; } = null!;
    }
}
