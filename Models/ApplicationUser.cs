using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Prenom { get; set; }

        [StringLength(100)]
        public string? Poste { get; set; }
        
        [ForeignKey("Role")]
        public int? RoleId { get; set; }
        
        [StringLength(100)]
        public string? Equipe { get; set; }
        
        [StringLength(100)]
        public string? Departement { get; set; }
        
        public DateTime? DerniereConnexion { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual Role? Role { get; set; }
    }
}