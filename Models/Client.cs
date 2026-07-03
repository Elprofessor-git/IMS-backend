using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Client
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
        public string? PreferencesTissus { get; set; }
        
        [StringLength(1000)]
        public string? NotesHistorique { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Clé étrangère
        [ForeignKey("Plateforme")]
        public int PlateformeId { get; set; }
        
        // Relations
        public virtual Plateforme? Plateforme { get; set; }
        public virtual ICollection<CommandeClient> Commandes { get; set; } = new List<CommandeClient>();
    }
}

