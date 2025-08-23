using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    public class UpdateUserDto
    {
        [StringLength(100)]
        public string? Nom { get; set; }

        [StringLength(100)]
        public string? Prenom { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(100)]
        public string? Poste { get; set; }

        public int? RoleId { get; set; }

        [StringLength(100)]
        public string? Equipe { get; set; }

        [StringLength(100)]
        public string? Departement { get; set; }

        public bool? EstActif { get; set; }
    }
}
