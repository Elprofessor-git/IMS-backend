namespace Backend_Gestion_Magasin_API.Dtos
{
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateRoleDto
    {
        public string Name { get; set; } = string.Empty;
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
