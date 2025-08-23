using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum StatutImportation
    {
        Brouillon,
        Soumise,
        Validee,
        Recue,
        Annulee
    }
    
    public enum ModeExpedition
    {
        Maritime,
        Aerien,
        Terrestre,
        Express,
        Autre
    }
    
    public class Importation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string ReferenceImportation { get; set; } = string.Empty;
        
        [ForeignKey("Fournisseur")]
        public int FournisseurId { get; set; }
        
        public StatutImportation Statut { get; set; } = StatutImportation.Brouillon;
        
        public DateTime DateImportation { get; set; } = DateTime.Now;
        
        public DateTime? DateReceptionPrevue { get; set; }
        
        public DateTime? DateReceptionReelle { get; set; }
        
        public ModeExpedition ModeExpedition { get; set; } = ModeExpedition.Maritime;
        
        public decimal MontantTotal { get; set; } = 0;
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        [StringLength(500)]
        public string? CheminFacture { get; set; }
        
        [StringLength(500)]
        public string? CheminBonLivraison { get; set; }
        
        [StringLength(500)]
        public string? CheminCertificatDouane { get; set; }
        
        [StringLength(1000)]
        public string? NotesImportation { get; set; }
        
        [StringLength(1000)]
        public string? HistoriqueModifications { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public DateTime? DateMiseAJour { get; set; }
        
        [StringLength(100)]
        public string? CreePar { get; set; }
        
        [StringLength(100)]
        public string? ModifiePar { get; set; }
        
        // Relations
        public virtual Fournisseur Fournisseur { get; set; } = null!;
        public virtual ICollection<LigneImportation> LignesImportation { get; set; } = new List<LigneImportation>();
    }
}

