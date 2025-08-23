using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Fournisseur
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(150)]
        public string NomEntreprise { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? PersonneContact { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string? Telephone { get; set; }
        
        [StringLength(500)]
        public string? Adresse { get; set; }
        
        [StringLength(100)]
        public string? Ville { get; set; }
        
        [StringLength(20)]
        public string? CodePostal { get; set; }
        
        [StringLength(100)]
        public string? Pays { get; set; }
        
        [StringLength(1000)]
        public string? SpecialitesProduits { get; set; }
        
        [StringLength(500)]
        public string? ConditionsPaiement { get; set; }
        
        public int DelaiLivraisonJours { get; set; } = 0;
        
        [StringLength(1000)]
        public string? NotesContrat { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual ICollection<Achat> Achats { get; set; } = new List<Achat>();
        public virtual ICollection<Importation> Importations { get; set; } = new List<Importation>();
    }
}

