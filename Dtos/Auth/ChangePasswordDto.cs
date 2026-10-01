using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Auth
{
    /// <summary>
    /// Changement de mot de passe par l'utilisateur lui-même, session ouverte.
    /// </summary>
    public class ChangePasswordDto
    {
        [Required]
        public string AncienMotDePasse { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NouveauMotDePasse { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NouveauMotDePasse), ErrorMessage = "La confirmation ne correspond pas au nouveau mot de passe.")]
        public string Confirmation { get; set; } = string.Empty;
    }
}
