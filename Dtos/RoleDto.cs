using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool EstAdministrateur { get; set; }
        public bool PeutGererStock { get; set; }
        public bool PeutGererCommandes { get; set; }
        public bool PeutGererTaches { get; set; }
        public bool PeutGererClients { get; set; }
        public bool PeutGererFournisseurs { get; set; }
        public bool PeutGererAchats { get; set; }
        public bool PeutGererImportations { get; set; }
        public bool PeutGererUtilisateurs { get; set; }
        public bool PeutGererMouvements { get; set; }
        public bool PeutValiderStock { get; set; }
        public bool PeutConfirmerAchats { get; set; }
        public bool PeutValiderImportations { get; set; }
        public bool EstActif { get; set; }
    }

    public class CreateRoleDto
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool EstAdministrateur { get; set; }
        public bool PeutGererStock { get; set; }
        public bool PeutGererCommandes { get; set; }
        public bool PeutGererTaches { get; set; }
        public bool PeutGererClients { get; set; }
        public bool PeutGererFournisseurs { get; set; }
        public bool PeutGererAchats { get; set; }
        public bool PeutGererImportations { get; set; }
        public bool PeutGererUtilisateurs { get; set; }
        public bool PeutGererMouvements { get; set; }
        public bool PeutValiderStock { get; set; }
        public bool PeutConfirmerAchats { get; set; }
        public bool PeutValiderImportations { get; set; }
    }

    public class UpdateStatutDto
    {
        public string Statut { get; set; } = string.Empty;
    }

    public class AssignerEquipeDto
    {
        public string EquipeId { get; set; } = string.Empty;
    }

    public class AssignerTacheDto
    {
        public string AssigneA { get; set; } = string.Empty;
    }

    public class ModifierPrioriteDto
    {
        public string Priorite { get; set; } = string.Empty;
    }

    public class ModifierEcheanceDto
    {
        public DateTime DateFinPrevue { get; set; }
    }
}
