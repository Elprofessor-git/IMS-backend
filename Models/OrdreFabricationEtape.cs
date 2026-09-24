using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Type d'étape de la gamme opératoire d'un OF (document maître 5.3).
    /// </summary>
    public enum TypeEtapeProduction
    {
        Coupe,
        Production,
        ControleQualite,
        Expedition
    }

    /// <summary>
    /// Étape de la gamme opératoire d'un Ordre de Fabrication (5.3) — upgrade de
    /// TacheProduction : chaque jalon d'un OF est suivi individuellement
    /// (statut, chaîne, dates prévues/réelles, temps théorique/réel).
    /// Le vocabulaire de statut réutilise StatutTache (déjà défini pour
    /// TacheProduction) — aucune table SQL renommée.
    /// Relation A SENS UNIQUE avec PlanningEntry : l'étape référence une cellule
    /// de planning optionnelle (PlanningEntryId), jamais l'inverse — il n'existe
    /// donc aucun second modèle concurrent du planning.
    /// </summary>
    public class OrdreFabricationEtape
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreFabrication")]
        public int OrdreFabricationId { get; set; }

        public TypeEtapeProduction TypeEtape { get; set; } = TypeEtapeProduction.Production;

        public StatutTache Statut { get; set; } = StatutTache.NonCommence;

        /// <summary>Chaîne affectée à cette étape (pertinente pour Production).</summary>
        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        /// <summary>Cellule de planning rattachée (relation à sens unique).</summary>
        [ForeignKey("PlanningEntry")]
        public int? PlanningEntryId { get; set; }

        public DateTime? DateDebutPrevue { get; set; }

        public DateTime? DateFinPrevue { get; set; }

        public DateTime? DateDebutReelle { get; set; }

        public DateTime? DateFinReelle { get; set; }

        public decimal TempsTheoriqueHeures { get; set; } = 0;

        public decimal TempsReelHeures { get; set; } = 0;

        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime? DateMiseAJour { get; set; }

        [StringLength(100)]
        public string? CreePar { get; set; }

        // Relations
        public virtual OrdreFabrication OrdreFabrication { get; set; } = null!;
        public virtual ChaineProduction? ChaineProduction { get; set; }
        public virtual PlanningEntry? PlanningEntry { get; set; }
    }
}