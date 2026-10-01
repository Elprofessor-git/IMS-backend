using System.Net;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services.Email;
using Microsoft.AspNetCore.Identity;

namespace Backend_Gestion_Magasin_API.Services.Auth
{
    public interface IPasswordSetupLinkService
    {
        /// <summary>
        /// Génère un lien « choisir votre mot de passe » et l'envoie par email.
        /// Retourne le lien (utile en test et en développement) ; l'envoi, lui, ne
        /// fait jamais échouer l'appelant.
        /// </summary>
        Task<string> EnvoyerLienAsync(ApplicationUser user, string raison);
    }

    /// <summary>
    /// Point d'entrée UNIQUE des emails de mot de passe d'ASP.NET Identity.
    ///
    /// Deux besoins très différents partagent EXACTEMENT le même mécanisme, et c'est
    /// volontaire : « mot de passe oublié » (l'utilisateur demande un lien) et
    /// « invitation » (un administrateur crée un compte, sans jamais connaître de
    /// mot de passe). Les deux passent par
    /// <c>UserManager.GeneratePasswordResetTokenAsync</c> puis
    /// <c>ResetPasswordAsync</c> — un flux de réinitialisation, pas un système de
    /// token séparé.
    ///
    /// Conséquences assumées :
    /// <list type="bullet">
    /// <item><b>Un mot de passe déjà choisi est remplaçable sans l'ancien.</b> Un
    /// administrateur qui dispose d'une boîte mail peut donc réinitialiser le mot de
    /// passe d'un compte. C'est le prix d'un flux sans secret partagé, et c'est
    /// cohérent avec l'absence de MFA sur l'application ; le remède est le MFA, pas
    /// un second système de jetons.</item>
    /// <item><b>Le lien est à usage unique et à durée de vie courte</b> (voir
    /// <c>PasswordResetTokenLifespan</c> dans Program.cs), et il cesse d'être valide
    /// dès que le SecurityStamp change — donc dès qu'un mot de passe est choisi.</item>
    /// </list>
    /// </summary>
    public class PasswordSetupLinkService : IPasswordSetupLinkService
    {
        /// <summary>Page qui consomme le lien. Doit rester alignée avec le frontend.</summary>
        private const string CheminPage = "/reset-password";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordSetupLinkService> _logger;

        public PasswordSetupLinkService(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<PasswordSetupLinkService> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> EnvoyerLienAsync(ApplicationUser user, string raison)
        {
            var email = user.Email
                        ?? throw new InvalidOperationException(
                            $"Le compte {user.Id} n'a pas d'adresse email : impossible de lui envoyer un lien.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var urlBase = FrontendBaseUrl();
            var lien = $"{urlBase}{CheminPage}?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

            // Le lien n'est PAS journalisé, même pas en développement : il porte le
            // jeton. Un niveau Information atterrit dans n'importe quel agrégateur de
            // logs (stdout, fichier, Loki…) avec une rétention bien plus longue que
            // celle du token, et se retrouve dans les discussions d'incident. Le
            // besoin de développement — voir le lien sans capture d'email — est couvert
            // par LoggingEmailSender, qui journalise le CORPS de l'email et donc le
            // lien, en development et en test seulement.
            _logger.LogInformation(
                "Lien de mot de passe ({Raison}) généré pour le compte {UserId}. Aucune donnée d'authentification n'est journalisée.",
                raison, user.Id);

            try
            {
                await _emailSender.SendAsync(email, ObjetPour(raison), CorpsHtml(user, lien, raison));
            }
            catch (Exception ex)
            {
                // Le flux appelant (création de compte, mot de passe oublié) doit
                // aboutir quoi qu'il arrive : l'email est un canal, pas une
                // transaction. Un échec est signalé au niveau ALERTE, avec la
                // conséquence énoncée, et l'appelant poursuit.
                _logger.LogError(
                    ex,
                    "Échec de l'envoi du lien de mot de passe ({Raison}) au compte {UserId}. " +
                    "Le compte reste créé/inactif : un administrateur doit renvoyer l'invitation.",
                    raison, user.Id);
            }

            return lien;
        }

        private string FrontendBaseUrl()
        {
            var url = _configuration["Email:FrontendBaseUrl"];
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning(
                    "Email:FrontendBaseUrl absent de la configuration : lien de mot de passe relatif généré. " +
                    "Renseigner Email__FrontendBaseUrl (ex. https://mon-app.vercel.app).");
                return string.Empty;
            }

            return url.TrimEnd('/');
        }

        private static string ObjetPour(string raison) => raison switch
        {
            "invitation" => "Votre accès au Système de Gestion Textile",
            _ => "Réinitialisation de votre mot de passe"
        };

        private static string CorpsHtml(ApplicationUser user, string lien, string raison)
        {
            var prenom = WebUtility.HtmlEncode(user.Prenom ?? user.Nom);
            var url = WebUtility.HtmlEncode(lien);
            var intro = raison == "invitation"
                ? @"<p>Un compte a été créé pour vous sur le <strong>Système de Gestion Textile</strong>.
                    Choisissez votre mot de passe pour activer votre accès.</p>"
                : @"<p>Une réinitialisation de mot de passe a été demandée pour votre compte.</p>";

            return $@"<!DOCTYPE html>
<html lang=""fr"">
  <body style=""font-family: Arial, Helvetica, sans-serif; font-size: 15px; color: #1f2937; line-height: 1.5;"">
    <p>Bonjour {prenom},</p>
    {intro}
    <p>
      <a href=""{url}"" style=""background:#111827;color:#ffffff;padding:11px 18px;border-radius:6px;
         text-decoration:none;display:inline-block;"">Choisir mon mot de passe</a>
    </p>
    <p style=""font-size: 13px; color: #6b7280;"">
      Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur :<br />
      <span style=""word-break: break-all;"">{url}</span>
    </p>
    <p style=""font-size: 13px; color: #6b7280;"">
      Ce lien est à usage unique et expire rapidement. Si vous n'êtes pas à l'origine de cette demande,
      ignorez ce message : votre mot de passe actuel reste valable.
    </p>
  </body>
</html>";
        }
    }
}
