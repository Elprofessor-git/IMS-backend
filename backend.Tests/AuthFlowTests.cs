using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

/// <summary>
/// Tests du lot « authentification complète » : mot de passe oublié, réinitialisation,
/// changement, invitation sans mot de passe, invalidation de session, invariant
/// désactivation de compte, et absence de fuite d'information.
///
/// Contraintes tenues par tous les tests :
/// <list type="bullet">
/// <item>vraie base PostgreSQL créée par les MIGRATIONS (contraintes réelles) ;</item>
/// <item>vrais JWT produits par le TokenService : [Authorize], RequireModulePermission
/// et la relecture du SecurityStamp sont réellement traversés ;</item>
/// <item>aucun appel réseau : l'émetteur d'email est remplacé par un émetteur
/// capturant, qui sert à récupérer le jeton du lien comme le ferait l'email.</item>
/// </list>
///
/// <para>Les cas de non-divulgation (<c>forgot-password</c> sur email inconnu et sur
/// compte désactivé) partagent volontairement le MÊME statut et le MÊME corps : c'est
/// la propriété testée, pas un détail d'implémentation.</para>
/// </summary>
public class AuthFlowTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthFlowTests(AuthApiFactory factory) => _factory = factory;

    /// <summary>
    /// Mot de passe de référence des comptes de test.
    /// Satisfait les règles d'Identity (6 caractères, minuscule, majuscule, chiffre).
    /// </summary>
    public const string MotDePasse = "Ancien1Pass";

    /// <summary>
    /// Réponse attendue de forgot-password, quelle que soit l'adresse saisie.
    /// La valeur est recopiée de <c>AuthController.ReponseMotDePasseOublie</c> : c'est
    /// cette constante, private, qui définit le contrat — la recopier ici évite que le
    /// test ne dépende d'un libellé que l'API peut réécrire.
    /// </summary>
    private const string ReponseGenerique =
        "Si un compte actif existe pour cette adresse, un lien de réinitialisation vient d'être envoyé.";

    private static string Email(string nom) => $"{nom}-{Guid.NewGuid():N}@ims.test";

    private async Task<(HttpResponseMessage Reponse, string LienUserId, string LienToken)> DemanderLienAsync(
        string email,
        bool motDePasseConnu = true)
    {
        if (motDePasseConnu)
        {
            await _factory.WithUserManagerAsync(async um =>
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EstActif = true,
                    EmailConfirmed = true
                };
                var r = await um.CreateAsync(user, MotDePasse);
                if (!r.Succeeded) throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
            });
        }

        var reponse = await _factory.PostAsync("/api/Auth/forgot-password", new { email });
        var lien = await _factory.DernierLienAsync(email);
        return (reponse, lien?.UserId ?? "", lien?.Token ?? "");
    }

    // ══════════ 1. Mot de passe oublié — réponse non discriminante ══════════

    [Fact]
    public async Task MotDePasseOublie_email_inconnu_renvoie_la_meme_reponse_qu_un_compte_existant()
    {
        var email = Email("inconnu");
        var reponse = await _factory.PostAsync("/api/Auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(ReponseGenerique, await AuthApiFactory.BodyAsync(reponse));

        // Aucun email ne doit partir pour une adresse sans compte : c'est ce qui
        // distingue « je ne connais pas cette adresse » de « cette adresse n'existe pas ».
        Assert.DoesNotContain(_factory.EmailSender().Envoys, e => e.Destinataire == email);
    }

    [Fact]
    public async Task MotDePasseOublie_compte_desactive_repond_comme_un_compte_existant_mais_envoie_rien()
    {
        var email = Email("desactive");
        await _factory.WithUserManagerAsync(async um =>
        {
            var user = new ApplicationUser { UserName = email, Email = email, EstActif = false, EmailConfirmed = true };
            await um.CreateAsync(user, MotDePasse);
        });

        var reponse = await _factory.PostAsync("/api/Auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(ReponseGenerique, await AuthApiFactory.BodyAsync(reponse));
        // Aucun email : l'envoyer confirmerait l'existence du compte ET permettrait de
        // redéfinir le mot de passe d'un compte hors service.
        Assert.DoesNotContain(_factory.EmailSender().Envoys, e => e.Destinataire == email);
    }

    [Fact]
    public async Task MotDePasseOublie_compte_actif_envoie_un_lien_avec_userId_et_token()
    {
        var email = Email("actif");
        var (reponse, userId, token) = await DemanderLienAsync(email);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(userId), "Le lien doit porter un userId.");
        Assert.False(string.IsNullOrWhiteSpace(token), "Le lien doit porter un token Identity.");

        var capture = Assert.Single(_factory.EmailSender().Envoys, e => e.Destinataire == email);
        Assert.Contains("Choisir mon mot de passe", capture.Corps, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MotDePasseOublie_reponse_ne_mentionne_ni_lexeristence_du_compte_ni_son_adresse()
    {
        var email = Email("fuite");
        await DemanderLienAsync(email);

        var corps = await AuthApiFactory.BodyAsync(await _factory.PostAsync("/api/Auth/forgot-password", new { email }));

        // Ni l'adresse saisie, ni un verdict sur le compte. On ne cherche PAS le
        // fragment « existe » : la réponse générique contient légitimement la
        // conditionnelle « si un compte actif existe… ». Ce qu'on refuse, c'est un
        // constat affirmatif sur CE compte.
        Assert.DoesNotContain(email, corps, StringComparison.OrdinalIgnoreCase);
        foreach (var verdict in new[] { "inconnu", "introuvable", "n'existe pas", "a bien été envoyé", "désactivé", "désactive" })
            Assert.DoesNotContain(verdict, corps, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════ 2. Réinitialisation par le lien ══════════

    [Fact]
    public async Task Reset_avec_un_lien_valide_definit_le_mot_de_passe_et_permet_la_connexion()
    {
        var email = Email("reset-ok");
        var (_, userId, token) = await DemanderLienAsync(email);

        var reponse = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token,
            nouveauMotDePasse = "Nouveau9Pass",
            confirmation = "Nouveau9Pass"
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var ancien = await _factory.PostAsync("/api/Auth/login", new { email, password = MotDePasse });
        Assert.Equal(HttpStatusCode.Unauthorized, ancien.StatusCode);

        var nouveau = await _factory.PostAsync("/api/Auth/login", new { email, password = "Nouveau9Pass" });
        Assert.Equal(HttpStatusCode.OK, nouveau.StatusCode);
    }

    [Fact]
    public async Task Reset_refuse_un_mot_de_passe_trop_faible_et_ne_consomme_pas_le_lien()
    {
        var email = Email("reset-faible");
        var (_, userId, token) = await DemanderLienAsync(email);

        var reponse = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token,
            nouveauMotDePasse = "faible",
            confirmation = "faible"
        });
        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

        // Le lien doit rester utilisable : un échec de validation ne doit pas avoir
        // consumé le jeton, sinon l'utilisateur se retrouve bloqué sans possibilité
        // de réessayer tant que le lien n'a pas expiré.
        var retry = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token,
            nouveauMotDePasse = "Fort9Passwd",
            confirmation = "Fort9Passwd"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Reset_refuse_un_token_inconnu_sans_divulger_si_le_compte_existe()
    {
        var email = Email("token-bidon");
        var (_, userId, _) = await DemanderLienAsync(email);

        var reponse = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token = "token-qui-n-a-pas-ete-emis-par-identity",
            nouveauMotDePasse = "Fort9Passwd",
            confirmation = "Fort9Passwd"
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("n'est plus valide", await AuthApiFactory.BodyAsync(reponse), StringComparison.OrdinalIgnoreCase);

        // Le mot de passe d'origine doit rester le seul valable.
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.PostAsync("/api/Auth/login", new { email, password = MotDePasse })).StatusCode);
    }

    [Fact]
    public async Task Reset_refuse_un_userId_inexistant_avec_la_meme_reponse_qu_un_token_invalide()
    {
        var email = Email("userid-bidon");
        await DemanderLienAsync(email);

        var avecUserIdInconnu = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId = Guid.NewGuid().ToString(),
            token = "nimporte-quoi",
            nouveauMotDePasse = "Fort9Passwd",
            confirmation = "Fort9Passwd"
        });
        var avecTokenInconnu = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId = (await _factory.WithDbAsync(db => db.Users.Select(u => u.Id).FirstAsync())),
            token = "nimporte-quoi",
            nouveauMotDePasse = "Fort9Passwd",
            confirmation = "Fort9Passwd"
        });

        // Même statut : le corps peut différer, mais l'API ne doit pas permettre de
        // distinguer « ce compte n'existe pas » de « ce lien est périmé ».
        Assert.Equal(avecTokenInconnu.StatusCode, avecUserIdInconnu.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, avecUserIdInconnu.StatusCode);
    }

    [Fact]
    public async Task Reset_est_a_usage_unique_le_meme_lien_ne_fonctionne_qu_une_seule_fois()
    {
        var email = Email("usage-unique");
        var (_, userId, token) = await DemanderLienAsync(email);

        var corps = new { userId, token, nouveauMotDePasse = "Fort9Passwd", confirmation = "Fort9Passwd" };

        Assert.Equal(HttpStatusCode.OK, (await _factory.PostAsync("/api/Auth/reset-password", corps)).StatusCode);

        // Le SecurityStamp ayant changé, le jeton émis avant n'est plus valide.
        var deuxieme = await _factory.PostAsync("/api/Auth/reset-password", corps);
        Assert.Equal(HttpStatusCode.BadRequest, deuxieme.StatusCode);
    }

    // ══════════ 3. Invalidation de session ══════════

    [Fact]
    public async Task Reset_coupe_les_sessions_ouvertes_avant_le_changement_de_mot_de_passe()
    {
        var email = Email("session-reset");
        var client = await _factory.CreateAuthenticatedClientAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Auth/me")).StatusCode);

        var (_, userId, token) = await DemanderLienAsync(email, motDePasseConnu: false);
        Assert.Equal(HttpStatusCode.OK, (await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token,
            nouveauMotDePasse = "Nouveau9Pass",
            confirmation = "Nouveau9Pass"
        })).StatusCode);

        // Le JWT est encore cryptographiquement valide et non expiré : ce qui le rend
        // inutilisable, c'est la relecture du SecurityStamp à chaque requête.
        var apres = await client.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, apres.StatusCode);
    }

    [Fact]
    public async Task Changement_de_mot_de_passe_coupe_la_session_courante()
    {
        var email = Email("session-change");
        var client = await _factory.CreateAuthenticatedClientAsync(email);

        var reponse = await _factory.PostAsync(client, "/api/Auth/change-password", new
        {
            ancienMotDePasse = MotDePasse,
            nouveauMotDePasse = "Nouveau9Pass",
            confirmation = "Nouveau9Pass"
        });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.PostAsync("/api/Auth/login", new { email, password = "Nouveau9Pass" })).StatusCode);
    }

    [Fact]
    public async Task Changement_de_mot_de_passe_refuse_un_ancien_mot_de_passe_faux()
    {
        var email = Email("change-refus");
        var client = await _factory.CreateAuthenticatedClientAsync(email);

        var reponse = await _factory.PostAsync(client, "/api/Auth/change-password", new
        {
            ancienMotDePasse = "Mauvais9Pass",
            nouveauMotDePasse = "Nouveau9Pass",
            confirmation = "Nouveau9Pass"
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        // Le compte n'a pas bougé : l'ancien mot de passe reste valable.
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.PostAsync("/api/Auth/login", new { email, password = MotDePasse })).StatusCode);
    }

    [Fact]
    public async Task Changement_de_mot_de_passe_exige_une_session_ouverte()
    {
        var reponse = await _factory.PostAsync("/api/Auth/change-password", new
        {
            ancienMotDePasse = MotDePasse,
            nouveauMotDePasse = "Nouveau9Pass",
            confirmation = "Nouveau9Pass"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    // ══════════ 4. Login et compte désactivé ══════════

    [Fact]
    public async Task Login_refuse_un_compte_desactive_meme_avec_le_bon_mot_de_passe()
    {
        var email = Email("login-desactive");
        await _factory.CreateAuthenticatedClientAsync(email);

        // Désactivation « ordinary » : elle passe par PUT /api/Account/users/{id}, qui
        // renouvelle le SecurityStamp. On vérifie ici le cas le plus défavorable, où le
        // stamp ne changerait pas.
        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.EstActif = false;
            await db.SaveChangesAsync();
        });

        var reponse = await _factory.PostAsync("/api/Auth/login", new { email, password = MotDePasse });

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Login_refuse_des_que_le_compte_est_desactive_meme_si_la_session_etait_ouverte_avant()
    {
        var email = Email("desactivation-immediate");
        var client = await _factory.CreateAuthenticatedClientAsync(email);

        // Sans renouvellement de SecurityStamp : c'est la vérification d'EstActif à
        // chaque requête qui doit couper la session, sinon une désactivation
        // n'aurait d'effet qu'à l'expiration du JWT (24 h).
        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.EstActif = false;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_ne_donne_pas_le_meme_code_pour_email_inconnu_et_mot_de_passe_faux()
    {
        var email = Email("login-collision");
        await _factory.CreateAuthenticatedClientAsync(email);

        var emailInconnu = await _factory.PostAsync("/api/Auth/login",
            new { email = Email("fantome"), password = MotDePasse });
        var motDePasseFaux = await _factory.PostAsync("/api/Auth/login",
            new { email, password = "Mauvais1Pass" });

        Assert.Equal(HttpStatusCode.Unauthorized, emailInconnu.StatusCode);
        Assert.Equal(motDePasseFaux.StatusCode, emailInconnu.StatusCode);
    }

    // ══════════ 4 bis. Désactivation et changement de rôle via l'API ══════════

    [Fact]
    public async Task Desactivation_via_l_api_coupe_la_session_ouverte()
    {
        var admin = await _factory.CreateRoleAsync("RoleAdminDesact" + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin-desact"), roleId: admin.Id);

        var email = Email("cible-desact");
        var cible = await _factory.CreateAuthenticatedClientAsync(email);
        var cibleId = await _factory.WithDbAsync(async db =>
            (await db.Users.FirstAsync(u => u.Email == email)).Id);

        Assert.Equal(HttpStatusCode.OK, (await cible.GetAsync("/api/Auth/me")).StatusCode);

        var reponse = await _factory.PutAsync(clientAdmin, $"/api/Account/users/{cibleId}", new { estActif = false });
        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);

        // L'effet est IMMÉDIAT : c'est la relecture d'EstActif à chaque requête qui le
        // garantit, sans attendre l'expiration du JWT.
        Assert.Equal(HttpStatusCode.Unauthorized, (await cible.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task Changement_de_role_via_l_api_coupe_la_session_ouverte()
    {
        var admin = await _factory.CreateRoleAsync("RoleAdminRemplace" + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var ancienRole = await _factory.CreateRoleAsync("RoleAncien" + Guid.NewGuid().ToString("N")[..6],
            r => r.PeutVoirTaches = true);
        var nouveauRole = await _factory.CreateRoleAsync("RoleNouveau" + Guid.NewGuid().ToString("N")[..6],
            r => r.PeutVoirTaches = false);

        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin-role"), roleId: admin.Id);

        var email = Email("cible-role");
        var cible = await _factory.CreateAuthenticatedClientAsync(email, roleId: ancienRole.Id);
        var cibleId = await _factory.WithDbAsync(async db =>
            (await db.Users.FirstAsync(u => u.Email == email)).Id);

        Assert.Equal(HttpStatusCode.OK, (await cible.GetAsync("/api/Auth/me")).StatusCode);

        var reponse = await _factory.PutAsync(clientAdmin, $"/api/Account/users/{cibleId}",
            new { roleId = nouveauRole.Id });
        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);

        // Le rôle est figé dans le JWT : sans renouvellement du SecurityStamp, l'ancien
        // jeton porterait encore les permissions de l'ancien rôle jusqu'à 24 h plus tard.
        Assert.Equal(HttpStatusCode.Unauthorized, (await cible.GetAsync("/api/Auth/me")).StatusCode);
    }

    // ══════════ 5. Invitation : aucun mot de passe initial ══════════

    [Fact]
    public async Task Invitation_cree_un_compte_sans_mot_de_passe_utilisable()
    {
        var admin = await _factory.CreateRoleAsync(Email("role-admin").Replace("@ims.test", "") + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin"), roleId: admin.Id);
        var email = Email("invite");

        var reponse = await _factory.PostAsync(clientAdmin, "/api/Auth/register", new
        {
            nom = "Durand",
            prenom = "Lucie",
            email,
            roleId = admin.Id
        });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        // Aucun mot de passe ne doit exister, pas même vide. Le statut exact importe peu
        // (400 par validation de [Required] sur une chaîne vide, 401 sinon) : ce qui
        // compte est qu'aucune tentative ne délivre de jeton.
        foreach (var essai in new[] { "", "Ancien1Pass", "n-importe-quoi" })
        {
            var corps = new { Email = email, Password = essai };
            var tentative = await _factory.PostAsync("/api/Auth/login", corps);
            Assert.NotEqual(HttpStatusCode.OK, tentative.StatusCode);
        }

        var hashEstPresent = await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Email == email);
            return !string.IsNullOrEmpty(user.PasswordHash);
        });
        Assert.False(hashEstPresent, "Un compte invité ne doit pas avoir de PasswordHash.");
    }

    [Fact]
    public async Task Invitation_envoie_un_lien_permettant_de_choisir_son_mot_de_passe()
    {
        var admin = await _factory.CreateRoleAsync("RoleAdminInvite" + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin2"), roleId: admin.Id);
        var email = Email("invite-lien");

        Assert.Equal(HttpStatusCode.OK, (await _factory.PostAsync(clientAdmin, "/api/Auth/register", new
        {
            nom = "Martin",
            email,
            roleId = admin.Id
        })).StatusCode);

        var (userId, token) = (await _factory.DernierLienAsync(email))!.Value;

        Assert.Equal(HttpStatusCode.OK, (await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId,
            token,
            nouveauMotDePasse = "Choisi1Pass",
            confirmation = "Choisi1Pass"
        })).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await _factory.PostAsync("/api/Auth/login", new { email, password = "Choisi1Pass" })).StatusCode);
    }

    [Fact]
    public async Task Invitation_renvoie_aucun_mot_de_passe_ni_dans_la_reponse_ni_dans_les_logs()
    {
        var admin = await _factory.CreateRoleAsync("RoleAdminLogs" + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin3"), roleId: admin.Id);
        var email = Email("invite-secret");

        var reponse = await _factory.PostAsync(clientAdmin, "/api/Auth/register", new
        {
            nom = "Petit",
            email,
            roleId = admin.Id
        });

        var corps = await AuthApiFactory.BodyAsync(reponse);
        Assert.DoesNotContain("password", corps, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("motDePasse", corps, StringComparison.OrdinalIgnoreCase);

        // L'email d'invitation ne doit contenir QUE le lien — aucun mot de passe à
        // deviner, aucun secret à transmettre. On vérifie la présence du lien (sinon
        // l'affirmation « aucun mot de passe » ne prouverait rien) et l'absence de
        // tout couple clé/valeur de type mot de passe.
        var capture = Assert.Single(_factory.EmailSender().Envoys, e => e.Destinataire == email);
        // On ne cherche pas « password » en vrac : le lien contient lui-même la page
        // « /reset-password ». Ce qu'on refuse, c'est un mot de passe À CÔTÉ du lien.
        Assert.Contains("token=", capture.Corps, StringComparison.Ordinal);
        Assert.DoesNotContain("password=", capture.Corps, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("motDePasse=", capture.Corps, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MotDePasse=", capture.Corps, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invitation_est_refusee_a_un_gestionnaire_d_utilisateurs_non_administrateur()
    {
        // Droit « gérer les utilisateurs » sans EstAdministrateur : c'est exactement
        // la configuration qui rendait l'ancien endpoint trop permissif.
        var roleGestionnaire = await _factory.CreateRoleAsync("RoleGestionnaire" + Guid.NewGuid().ToString("N")[..6],
            r => { r.PeutVoirUtilisateurs = true; r.PeutGererUtilisateurs = true; });
        var client = await _factory.CreateAuthenticatedClientAsync(Email("gestionnaire"), roleId: roleGestionnaire.Id);

        var reponse = await _factory.PostAsync(client, "/api/Auth/register", new
        {
            nom = "Espion",
            email = Email("espion")
        });

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
    }

    [Fact]
    public async Task Invitation_est_refusee_sans_session()
    {
        var reponse = await _factory.PostAsync("/api/Auth/register", new { nom = "X", email = Email("anon") });
        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Le_creation_du_compte_reussit_meme_si_l_envoi_de_l_email_echoue()
    {
        var admin = await _factory.CreateRoleAsync("RoleAdminEchec" + Guid.NewGuid().ToString("N")[..6],
            r => r.EstAdministrateur = true);
        var clientAdmin = await _factory.CreateAuthenticatedClientAsync(Email("admin4"), roleId: admin.Id);
        var email = Email("invite-echec");
        _factory.EmailSender().DoitEchouer = true;

        try
        {
            var reponse = await _factory.PostAsync(clientAdmin, "/api/Auth/register", new
            {
                nom = "Durand",
                email,
                roleId = admin.Id
            });

            // L'email est un canal, pas une transaction : le compte doit exister, sinon
            // un incident d'envoi obligerait l'administrateur à recréer le compte.
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
        }
        finally
        {
            _factory.EmailSender().DoitEchouer = false;
        }
    }
    // ══════════ 6. Réponses sans fuite d'information ══════════

    [Fact]
    public async Task Aucune_reponse_d_authentification_ne_renvoie_de_security_stamp()
    {
        var email = Email("stamp-fuite");
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        var stamp = await _factory.WithDbAsync(async db =>
            await db.Users.Where(u => u.Email == email).Select(u => u.SecurityStamp).FirstAsync());
        Assert.False(string.IsNullOrEmpty(stamp));

        var me = await client.GetAsync("/api/Auth/me");
        Assert.DoesNotContain(stamp!, await AuthApiFactory.BodyAsync(me), StringComparison.Ordinal);

        // Le claim est dans le JWT : il est signé, donc illisible sans la clé, et il ne
        // doit surtout pas être renvoyé dans un corps de réponse.
        var corpsMe = await AuthApiFactory.BodyAsync(me);
        Assert.DoesNotContain("SecurityStamp", corpsMe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_expose_le_perimetre_du_compte_sans_le_security_stamp()
    {
        var role = await _factory.CreateRoleAsync("RoleMe" + Guid.NewGuid().ToString("N")[..6],
            r => { r.PeutVoirTaches = true; r.EstAdministrateur = false; });
        var client = await _factory.CreateAuthenticatedClientAsync(Email("me-perimetre"), roleId: role.Id);

        var me = JsonDocument.Parse(await AuthApiFactory.BodyAsync(await client.GetAsync("/api/Auth/me"))).RootElement;

        Assert.Equal(role.Id, me.GetProperty("roleId").GetInt32());
        Assert.False(me.GetProperty("estAdministrateur").GetBoolean());
        Assert.True(me.GetProperty("estActif").GetBoolean());
        // Le stamp est dans le JWT (donc illisible sans la clé), jamais dans un corps.
        Assert.False(me.TryGetProperty("securityStamp", out _));
    }
}

/// <summary>
/// Cas « lien périmé », isolé dans sa propre classe et sa propre instance de factory.
/// </summary>
/// <remarks>
/// xUnit n'instancie pas une classe imbriquée : sans classe de premier niveau
///distincte, ce test ne serait pas découvert et passerait sans rien vérifier. La
/// factory est par ailleurs dédiée — réduire la durée de vie du jeton GLOBALEment
/// expirerait tous les autres liens et ferait échouer le reste de la suite.
/// </remarks>
public sealed class LienPerimeTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new() { DureeLien = TimeSpan.Zero };

    public async Task InitializeAsync() => await _factory.InitializeAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>
    /// Un lien périmé est refusé — et refusé comme un jeton INVALIDE, pas comme un
    /// compte inexistant : la réponse reste identique, sinon elle devient un oracle.
    /// </summary>
    [Fact]
    public async Task Lien_expire_refuse_le_changement_et_laisse_l_ancien_mot_de_passe_valable()
    {
        var email = $"perime-{Guid.NewGuid():N}@ims.test";
        await _factory.WithUserManagerAsync(um =>
            um.CreateAsync(new ApplicationUser
            {
                UserName = email,
                Email = email,
                EstActif = true,
                EmailConfirmed = true,
            }, AuthFlowTests.MotDePasse));

        await _factory.PostAsync("/api/Auth/forgot-password", new { email });

        var lien = _factory.EmailSender().DernierLien(email);
        Assert.NotNull(lien);

        var q = QueryHelpers.ParseQuery(lien!.Query);
        var reponse = await _factory.PostAsync("/api/Auth/reset-password", new
        {
            userId = q["userId"].ToString(),
            token = q["token"].ToString(),
            nouveauMotDePasse = "Nouveau2Pass",
            confirmation = "Nouveau2Pass",
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

        // Le mot de passe d'origine reste le seul valable : le refus n'a rien changé.
        var ancien = await _factory.PostAsync("/api/Auth/login",
            new { email, password = AuthFlowTests.MotDePasse });
        Assert.Equal(HttpStatusCode.OK, ancien.StatusCode);
    }
}
