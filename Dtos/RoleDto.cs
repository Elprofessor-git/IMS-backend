using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    // DTO pour l'affichage et la gestion des rôles personnalisés
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool PeutGererStock { get; set; }
        public bool EstAdministrateur { get; set; }
    }

    public class CreateRoleDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool PeutGererStock { get; set; }
        public bool EstAdministrateur { get; set; }
    }

    // DTOs indispensables pour les tâches de production
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
