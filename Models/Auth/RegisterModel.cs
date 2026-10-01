using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models.Auth
{
    /// <summary>
    /// Création d'un compte par un administrateur (invitation).
    /// </summary>
    /// <remarks>
    /// VOLONTAIREMENT SANS champ <c>Password</c> : l'administrateur ne connaît pas le
    /// mot de passe du compte qu'il crée. Le destinataire le choisit lui-même via le
    /// lien reçu par email, par le mécanisme de réinitialisation d'Identity. Un champ
    /// <c>Password</c> ici rouvrirait la porte à un mot de passe choisi par un tiers,
    /// transmis par un canal non maîtrisé et souvent réutilisé.
    /// </remarks>
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

        /// <summary>
        /// ID du rôle personnalisé (table Role). 0 ou null = pas de rôle assigné.
        /// </summary>
        public int? RoleId { get; set; }
    }
}
