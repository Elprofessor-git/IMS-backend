using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models.Auth
{
    public class RegisterModel
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Prenom { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// ID du rôle personnalisé (table Role). 0 ou null = pas de rôle assigné.
        /// </summary>
        public int? RoleId { get; set; }
    }
}
