using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum StatutAchat
    {
        Brouillon,
        Soumis,
        Confirme,
        Livre,
        Annule
    }
    
    public class Achat
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string NumeroAchat { get; set; } = string.Empty;
        
        [ForeignKey("Fournisseur")]
        public int FournisseurId { get; set; }
        
        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }
        
        public StatutAchat Statut { get; set; } = StatutAchat.Brouillon;
        
        public DateTime DateAchat { get; set; } = DateTime.Now;
        
        public DateTime? DateLivraisonPrevue { get; set; }
        
        public DateTime? DateLivraisonReelle { get; set; }
        
        public decimal MontantTotal { get; set; } = 0;

        public decimal MontantTotalTND { get; set; } = 0; // Converti figé à l'écriture (devise de référence)
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        [StringLength(500)]
        public string? ConditionsPaiement { get; set; }
        
        [StringLength(1000)]
        public string? NotesAchat { get; set; }
        
        [StringLength(500)]
        public string? CheminPDF { get; set; }
        
        [StringLength(1000)]
        public string? HistoriqueModifications { get; set; }
        
        [StringLength(1000)]
        public string? JustificatifAnnulation { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public DateTime? DateMiseAJour { get; set; }
        
        [StringLength(100)]
        public string? CreePar { get; set; }
        
        [StringLength(100)]
        public string? ModifiePar { get; set; }
        
        // Relations
        public virtual Fournisseur? Fournisseur { get; set; }
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual ICollection<LigneAchat> LignesAchat { get; set; } = new List<LigneAchat>();
    }
}

