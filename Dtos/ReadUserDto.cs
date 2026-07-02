namespace Backend_Gestion_Magasin_API.Dtos
{
    public class ReadUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Prenom { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }       // rôle Identity string (ex: "Admin")
        public int? RoleId { get; set; }        // ID du rôle personnalisé (AppRoles)
        public string? NomRole { get; set; }    // nom du rôle personnalisé
        public bool EstActif { get; set; }
        public DateTime DateCreation { get; set; }
    }
}
