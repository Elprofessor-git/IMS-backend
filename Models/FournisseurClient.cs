using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class FournisseurClient
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? Prenom { get; set; }
        
        [StringLength(150)]
        public string? NomEntreprise { get; set; }
        
        [Required]
        [EmailAddress]
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
        
        public bool EstFournisseur { get; set; } = false;
        
        public bool EstClient { get; set; } = false;
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
    }
}
