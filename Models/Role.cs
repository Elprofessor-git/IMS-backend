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
        
        // Permissions par module — écriture
        public bool PeutGererStock { get; set; } = false;
        public bool PeutGererCommandes { get; set; } = false;
        public bool PeutGererTaches { get; set; } = false;
        public bool PeutGererClients { get; set; } = false;
        public bool PeutGererFournisseurs { get; set; } = false;
        public bool PeutGererAchats { get; set; } = false;
        public bool PeutGererImportations { get; set; } = false;
        public bool PeutGererUtilisateurs { get; set; } = false;
        public bool PeutGererMouvements { get; set; } = false;
        public bool PeutGererPlateformes { get; set; } = false;

        // Permissions par module — lecture seule
        public bool PeutVoirMouvements { get; set; } = false;
        public bool PeutVoirCommandes { get; set; } = false;
        public bool PeutVoirClients { get; set; } = false;
        public bool PeutVoirFournisseurs { get; set; } = false;
        public bool PeutVoirPlateformes { get; set; } = false;
        public bool PeutVoirTaches { get; set; } = false;
        public bool PeutVoirUtilisateurs { get; set; } = false;
        public bool PeutVoirRoles { get; set; } = false;

        // Permissions spéciales
        public bool PeutValiderStock { get; set; } = false;
        public bool PeutConfirmerAchats { get; set; } = false;
        public bool PeutValiderImportations { get; set; } = false;
        public bool EstAdministrateur { get; set; } = false;

        // Dashboard & Rapports (lecture seule)
        public bool PeutVoirDashboard { get; set; } = true;
        public bool PeutVoirRapports { get; set; } = true;

        // Facturation
        public bool PeutVoirFactures { get; set; } = false;
        public bool PeutGererFactures { get; set; } = false;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual ICollection<ApplicationUser> Utilisateurs { get; set; } = new List<ApplicationUser>();
    }
}

