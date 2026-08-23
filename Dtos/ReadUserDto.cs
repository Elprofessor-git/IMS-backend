namespace Backend_Gestion_Magasin_API.Dtos
{
    public class ReadUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Prenom { get; set; }
        public string Email { get; set; } = string.Empty;
        public int? RoleId { get; set; }
        public string? NomRole { get; set; }
        public bool EstActif { get; set; }
        public DateTime DateCreation { get; set; }
    }
}
