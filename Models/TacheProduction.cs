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
        
        // Relations
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual ICollection<MouvementStock> MouvementsStock { get; set; } = new List<MouvementStock>();
    }
}

