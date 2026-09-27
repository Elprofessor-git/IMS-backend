using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum StatutTache
    {
        NonCommence,
        EnCours,
        Bloque,
        Termine,
        Annule
    }
    
    public enum PrioriteTache
    {
        Basse,
        Normale,
        Haute,
        Urgente
    }
    
    public class TacheProduction
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }
        
        [StringLength(100)]
        public string? EquipeAssignee { get; set; }

        // ── Propriété utilisateur (source de vérité de l'ownership) ──────────────
        // AssignedToUserId : le responsable de l'exécution. CreatedByUserId : l'utilisateur
        // IMS qui a créé/validé la tâche. Les deux sont des FK vers AspNetUsers.
        // NULL n'est possible que pour les tâches historiques antérieures à cette colonne :
        // voir la migration « AddTacheOwnershipUsers » et la stratégie de backfill documentée.
        // Toute tâche créée par l'API est enregistrée avec CurrentUserId, jamais avec
        // une valeur fournie par le client.
        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(450)]
        public string? AssignedToUserId { get; set; }

        // Champs legacy conservés pour la compatibilité d'affichage (et l'export) :
        // ils ne sont plus la source de vérité. Toute écriture de la FK utilisateur
        // réécrit le libellé legacy correspondant (voir TacheOwnershipService).
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }

        public StatutTache Statut { get; set; } = StatutTache.NonCommence;
        
        public PrioriteTache Priorite { get; set; } = PrioriteTache.Normale;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public DateTime? DateDebutPrevue { get; set; }
        
        public DateTime? DateFinPrevue { get; set; }
        
        public DateTime? DateDebutReelle { get; set; }
        
        public DateTime? DateFinReelle { get; set; }
        
        public int DureeEstimeeHeures { get; set; } = 0;
        
        public int DureeReelleHeures { get; set; } = 0;
        
        public decimal PourcentageAvancement { get; set; } = 0;
        
        [StringLength(1000)]
        public string? NotesProgression { get; set; }
        
        [StringLength(1000)]
        public string? ProblemesBloques { get; set; }
        
        [StringLength(100)]
        public string? CreePar { get; set; }
        
        public DateTime? DateMiseAJour { get; set; }
        
        [StringLength(100)]
        public string? ModifiePar { get; set; }

        // Groupe d'origine si la tâche a été générée par l'application d'un modèle
        // (module « Tâches »). SetNull : la génération se sépare du groupe supprimé.
        [ForeignKey("GroupeTache")]
        public int? GroupeTacheId { get; set; }

        // Relations
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual GroupeTache? GroupeTache { get; set; }
        public virtual ICollection<MouvementStock> MouvementsStock { get; set; } = new List<MouvementStock>();

        // Relations utilisateur (ownership). Configurées côté TacheProduction dans
        // ApplicationDbContext (deux FK vers le même type : navigations opposées nommées
        // pour éviter toute ambiguïté).
        public virtual ApplicationUser? CreatedBy { get; set; }
        public virtual ApplicationUser? AssignedTo { get; set; }
    }
}

