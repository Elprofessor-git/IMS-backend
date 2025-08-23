using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models.Auth
{
    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}

