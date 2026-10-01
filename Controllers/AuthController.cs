using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Dtos.Auth;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Auth;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        /// <summary>Réponse unique de « mot de passe oublié », quel que soit l'email saisi.</summary>
        private const string ReponseMotDePasseOublie =
            "Si un compte actif existe pour cette adresse, un lien de réinitialisation vient d'être envoyé.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordSetupLinkService _lienMotDePasse;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            TokenService tokenService,
            ILogger<AuthController> logger,
            ApplicationDbContext context,
            IPasswordSetupLinkService lienMotDePasse)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _logger = logger;
            _context = context;
            _lienMotDePasse = lienMotDePasse;
        }

        /// <summary>
        /// Invitation d'un compte (réservé aux administrateurs / gestionnaires du module
        /// Utilisateurs).
        ///
        /// L'admin ne choisit PAS de mot de passe. Le compte reçoit par email un lien
        /// « choisir mon mot de passe » — exactement le même mécanisme que « mot de
        /// passe oublié » (<c>GeneratePasswordResetTokenAsync</c> puis
        /// <c>ResetPasswordAsync</c>), pas un flux séparé.
        ///
        /// Un mot de passe aléatoire est malgré tout posé en base : Identity exige un
        /// hash au moment de la création, et il doit être inconnu de tous. C'est ce
        /// qui rend le compte INUTILISABLE tant que le lien n'a pas été utilisé — aucun
        /// drapeau supplémentaire n'est nécessaire, et aucun mot de passe n'est
        /// retourné ni journalisé.
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        [RequireModulePermission("utilisateurs", requireWrite: true)]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // L'invitation est RÉSERVÉE AUX ADMINISTRATEURS.
            //
            // [RequireModulePermission("utilisateurs", write)] ne suffit pas : le droit
            // « gérer les utilisateurs » est accordable indépendamment de
            // EstAdministrateur (MapModule), donc un rôle non-admin peut le posséder et
            // atteindre cet endpoint. Or inviter quelqu'un, c'est créer un compte avec
            // le rôle que l'on choisit — un droit d'escalade que seul un administrateur
            // doit détenir. Le garde est donc testé ici, sur le rôle du demandeur.
            var demandeurId = _userManager.GetUserId(User);
            var demandeur = demandeurId != null
                ? await _userManager.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == demandeurId)
                : null;

            if (demandeur?.Role?.EstAdministrateur != true)
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Réservé aux administrateurs : l'invitation d'un utilisateur nécessite le rôle Administrateur."
                });

            var normalizedEmail = _userManager.NormalizeEmail(model.Email) ?? model.Email;
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Un utilisateur avec cet email existe déjà.");

            // Le garde « rôle Administrateur réservé aux administrateurs » a disparu : le
            // contrôle d'accès en tête de méthode l'implique déjà (le demandeur est
            // administrateur, sinon 403). Le garder en double demanderait le rôle du
            // demandeur une seconde fois et divergerait un jour de la règle réelle.

            var user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = model.Email,
                Nom = model.Nom,
                Prenom = model.Prenom,
                RoleId = model.RoleId == 0 ? null : model.RoleId,
            };

            // Création SANS mot de passe : PasswordHash reste null, donc
            // CheckPasswordAsync ne peut aboutir pour aucune chaîne, y compris la
            // vide. Aucun secret n'est généré, donc aucun n'est à protéger. Avant ce
            // lot un mot de passe aléatoire était posé puis oublié : le compte
            // disposait d'un mot de passe que seul le serveur connaissait, sans que
            // personne ne puisse plus s'en servir.
            var result = await _userManager.CreateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _lienMotDePasse.EnvoyerLienAsync(user, "invitation");

            return Ok(new
            {
                message = $"Utilisateur créé. Un email d'invitation a été envoyé à {model.Email} : il doit choisir son mot de passe via le lien reçu pour pouvoir se connecter.",
                id = user.Id
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            // Compte inexistant, mot de passe erroné et compte DÉSACTIVÉ rendent le
            // même 401 : aucune ne doit révéler qu'un compte existe ou est inactif.
            // (Avant ce lot, EstActif n'était pas vérifié ici du tout.)
            if (user == null || !user.EstActif || !await _userManager.CheckPasswordAsync(user, model.Password))
                return Unauthorized();

            // Charger l'utilisateur avec son rôle personnalisé
            var userWithRole = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            var token = _tokenService.CreateToken(userWithRole ?? user);
            return Ok(new { token });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                nom = user.Nom ?? user.UserName,
                prenom = user.Prenom ?? "",
                role = user.Role?.NomRole,
                roleId = user.RoleId,
                estAdministrateur = user.Role?.EstAdministrateur == true,
                estActif = user.EstActif
            });
        }

        /// <summary>
        /// Demande de lien de réinitialisation.
        ///
        /// La réponse HTTP est IDENTIQUE dans les trois cas — email existant, email
        /// inexistant, compte désactivé — pour ne jamais révéler quels emails
        /// correspondent à un compte. Un compte désactivé ne reçoit rien : l'envoyer
        /// confirmerait son existence ET permettrait de lui poser un nouveau mot de passe
        /// alors qu'il est hors service.
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                // Journalisé à titre de diagnostic uniquement, jamais renvoyé.
                _logger.LogInformation("Demande de réinitialisation pour un email sans compte actif.");
            }
            else if (!user.EstActif)
            {
                _logger.LogInformation("Demande de réinitialisation pour un compte désactivé : aucun email envoyé.");
            }
            else
            {
                await _lienMotDePasse.EnvoyerLienAsync(user, "reinitialisation");
            }

            return Ok(new { message = ReponseMotDePasseOublie });
        }

        /// <summary>
        /// Consommation du lien : choix d'un nouveau mot de passe.
        ///
        /// L'<see cref="ResetPasswordDto"/> ne porte que ce que l'utilisateur a lui-même
        /// reçu par email : aucune donnée du compte n'est acceptée depuis le client.
        /// Un token invalide, expiré, déjà utilisé (car le SecurityStamp a changé) ou
        /// appartenant à un compte supprimé donne tous la même réponse, sans indice
        /// supplémentaire.
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogInformation("Réinitialisation : compte introuvable.");
                return BadRequest(new { message = "Ce lien de réinitialisation n'est plus valide. Demandez-en un nouveau." });
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NouveauMotDePasse);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    _logger.LogInformation("Réinitialisation refusée pour {UserId} : {Code}", user.Id, error.Code);

                return BadRequest(new
                {
                    message = "Ce lien de réinitialisation n'est plus valide (expiré, déjà utilisé ou incorrect). Demandez-en un nouveau.",
                    erreurs = result.Errors.Select(e => e.Description).Distinct().ToArray()
                });
            }

            // ResetPasswordAsync fait déjà tourner le SecurityStamp nativement :
            // AUCUN UpdateSecurityStampAsync ici, sinon on invaliderait deux fois pour
            // rien. Conséquence voulue : le lien est à usage unique (le token
            // embarquait l'ancien stamp) et toute session ouverte est coupée.
            return Ok(new { message = "Mot de passe défini. Vous pouvez vous connecter." });
        }

        /// <summary>
        /// Changement de mot de passe par l'utilisateur connecté.
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(
                user, model.AncienMotDePasse, model.NouveauMotDePasse);

            if (!result.Succeeded)
            {
                // « mot de passe actuel incorrect » n'est pas distingué des règles de
                // robustesse pour ne rien apprendre à un attaquant qui n'a pas
                // l'ancien mot de passe.
                return BadRequest(new
                {
                    message = "Mot de passe actuel incorrect, ou nouveau mot de passe trop faible (6 caractères minimum, avec majuscule, minuscule et chiffre).",
                    erreurs = result.Errors.Select(e => e.Description).Distinct().ToArray()
                });
            }

            // Comme pour ResetPasswordAsync, ChangePasswordAsync met à jour le
            // SecurityStamp nativement : le jeton utilisé pour CET appel devient
            // aussitôt invalide. Conséquence réelle, à assumer : l'utilisateur est
            // déconnecté de tous ses autres appareils et devra se reconnecter. C'est le
            // comportement attendu d'un changement de mot de passe.
            return Ok(new
            {
                message = "Mot de passe modifié. Vos autres sessions ont été déconnectées : reconnectez-vous."
            });
        }

        /// <summary>
        /// Déconnexion.
        ///
        /// ⚠️ Endpoint SANS EFFET sur la validité des jetons. Un JWT reste valide jusqu'à
        /// son expiration (24 h) ou jusqu'au prochain changement de SecurityStamp
        /// (changement/réinitialisation de mot de passe, désactivation, changement de
        /// rôle). Il n'y a volontairement ni liste de révocation ni
        /// refresh token : la déconnexion côté client se contente d'effacer le cookie
        /// httpOnly, et c'est ce geste qui suffit.
        ///
        /// L'endpoint existe pour les clients non-navigateur, qui n'ont pas de cookie à
        /// effacer. Il renvoie 204 pour être explicite sur le contrat, sans laisser
        /// croire à une révocation côté serveur.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout() => NoContent();

        /// <summary>
        /// Mot de passe aléatoire, forte entropie, jamais journalisé ni retourné.
        ///
        /// Cryptographiquement aléatoire (<see cref="RandomNumberGenerator"/>), alphanumérique
        /// étendu : il ne sert qu'à satisfaire Identity à la création du compte, et ne
        /// sera jamais communiqué. Il est volontairement long et sans caractère
        /// exotique, pour ne jamais buter sur une règle de robustesse ni sur un
        /// problème d'encodage.
        /// </summary>
    }
}
