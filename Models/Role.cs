using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string NomRole { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        // Permissions par module
        public bool PeutGererStock { get; set; } = false;
        public bool PeutGererCommandes { get; set; } = false;
        public bool PeutGererTaches { get; set; } = false;
        public bool PeutGererClients { get; set; } = false;
        public bool PeutGererFournisseurs { get; set; } = false;
        public bool PeutGererAchats { get; set; } = false;
        public bool PeutGererImportations { get; set; } = false;
        public bool PeutGererUtilisateurs { get; set; } = false;
        public bool PeutGererMouvements { get; set; } = false;
        
        // Permissions spéciales
        public bool PeutValiderStock { get; set; } = false;
        public bool PeutConfirmerAchats { get; set; } = false;
        public bool PeutValiderImportations { get; set; } = false;
        public bool EstAdministrateur { get; set; } = false;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual ICollection<ApplicationUser> Utilisateurs { get; set; } = new List<ApplicationUser>();
    }
}

