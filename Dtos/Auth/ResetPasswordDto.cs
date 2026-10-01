using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Auth
{
    /// <summary>
    /// Choix d'un nouveau mot de passe à partir d'un lien reçu par email.
    /// </summary>
    /// <remarks>
    /// DTO dédié, jamais une entité EF bindée : <c>userId</c> et <c>token</c>
    /// proviennent de l'URL fournie par l'utilisateur, et rien d'autre n'est accepté
    /// de sa part — ni roleId, ni estActif, ni aucune donnée du compte.
    /// </remarks>
    public class ResetPasswordDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NouveauMotDePasse { get; set; } = string.Empty;

        /// <summary>Confirmation saisie : comparée côté serveur, jamais en JavaScript seul.</summary>
        [Required]
        [Compare(nameof(NouveauMotDePasse), ErrorMessage = "La confirmation ne correspond pas au nouveau mot de passe.")]
        public string Confirmation { get; set; } = string.Empty;
    }
}
