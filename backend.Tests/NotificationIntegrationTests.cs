using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Notifications de la cloche pour les TÂCHES et les EMAILS (LOT 17).
///
/// Ces tests ne créent pas de système parallèle : ils exercent la table et les endpoints
/// historiques du planning, étendue aux nouveaux émetteurs. Tous les statuts HTTP observés
/// proviennent du vrai pipeline d'authentification, sur PostgreSQL migré.
///
/// Trois exigences dominent :
///   1. le destinataire est décidé par le serveur (jamais un userId du client) ;
///   2. une notification appartient à son destinataire (ni lisible, ni marquable par autrui) ;
///   3. une opération unique ne produit pas deux notifications identiques.
/// </summary>
public class NotificationIntegrationTests : IClassFixture<TacheApiFactory>
{
    private const string PlanningRoute = "/api/Notification";

    private readonly TacheApiFactory _factory;

    public NotificationIntegrationTests(TacheApiFactory factory) => _factory = factory;

    // ── Mise en place ───────────────────────────────────────────────────

    /// <summary>Opérateur du module Tâches ; <paramref name="peutAssigner"/> règle le droit d'assignation.</summary>
    private Task<TestUser> CreateOperateurAsync(string id, bool peutAssigner = false) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = false;
            role.PeutAssignerTaches = peutAssigner;
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
        });

    private async Task<int> CreerTacheAsync(TestUser auteur, string titre)
    {
        var reponse = await auteur.Client.PostAsJsonAsync("/api/TacheProduction", new { titre });
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .Where(t => t.Titre == titre)
                .Select(t => t.Id)
                .SingleAsync());
    }

    /// <summary>Connexion Gmail + message pour l'utilisateur, sans passer par OAuth (hors périmètre).</summary>
    private async Task<int> CreerMessageAsync(string userId, string objet, string corps = "Contenu confidentiel")
    {
        return await _factory.WithDbAsync(async db =>
        {
            var connexion = new GmailConnection
            {
                UserId = userId,
                GmailAddress = userId + "@gmail.test",
                GoogleUserId = "google-" + userId,
                RefreshTokenEncrypted = "jeton-chiffre-de-test",
                GrantedScopes = "https://www.googleapis.com/auth/gmail.readonly",
                ConnectedAt = DateTime.Now
            };
            db.GmailConnections.Add(connexion);
            await db.SaveChangesAsync();

            var message = new GmailMessage
            {
                GmailConnectionId = connexion.Id,
                GmailMessageId = "msg-" + Guid.NewGuid().ToString("N")[..10],
                GmailThreadId = "thread-" + Guid.NewGuid().ToString("N")[..10],
                From = "expediteur@exemple.fr",
                To = userId + "@ims.test",
                Subject = objet,
                BodyText = corps,
                ReceivedAt = DateTime.Now
            };
            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });
    }

    /// <summary>Notifications d'un utilisateur, telles que renvoyées par la cloche.</summary>
    private sealed record ClocheDto(List<NotifDto> Notifications, int CountNonLivrees);

    private sealed record NotifDto(
        int Id,
        string Message,
        DateTime DateNotification,
        bool EstLivree,
        string Type,
        int? PlanningEntryId,
        int? TacheProductionId,
        int? GmailMessageId);

    private async Task<ClocheDto> LireClocheAsync(TestUser user) =>
        JsonSerializer.Deserialize<ClocheDto>(
            await (await user.Client.GetAsync(PlanningRoute + "/me")).Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private Task<List<NotifDto>> NotificationsAsync(string userId) =>
        _factory.WithDbAsync(db => db.Notifications.AsNoTracking()
            .Where(n => n.UtilisateurId == userId)
            .OrderBy(n => n.Id)
            .Select(n => new NotifDto(n.Id, n.Message, n.DateNotification, n.EstLivree,
                n.Type.ToString(), n.PlanningEntryId, n.TacheProductionId, n.GmailMessageId))
            .ToListAsync());

    // ══════════════ Test 1 — assignation : seul le nouveau responsable est prévenu ══════════════

    [Fact]
    public async Task N01_Assigner_une_tache_previne_le_nouveau_responsable_seulement()
    {
        var alice = await CreateOperateurAsync("n01-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n01-bob");
        var tacheId = await CreerTacheAsync(alice, "N01 Préparer le rapport client");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var notificationsBob = await NotificationsAsync(bob.Id);
        var sienne = Assert.Single(notificationsBob);
        Assert.Equal("TacheAssignee", sienne.Type);
        Assert.Equal(tacheId, sienne.TacheProductionId);
        Assert.Contains("N01 Préparer le rapport client", sienne.Message);
        Assert.False(sienne.EstLivree);

        // L'auteur n'apprend rien qu'il ne sache déjà : aucune notification inutile.
        Assert.Empty(await NotificationsAsync(alice.Id));
    }

    [Fact]
    public async Task N01bis_Reassigner_a_un_tiers_previne_le_nouveau_seul_et_retire_l_ancien()
    {
        var alice = await CreateOperateurAsync("n01b-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n01b-bob");
        var carol = await CreateOperateurAsync("n01b-carol");
        var tacheId = await CreerTacheAsync(alice, "N01b Réassignation");

        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = carol.Id });

        // Carol reçoit l'attribution…
        var carolNotifs = await NotificationsAsync(carol.Id);
        var attribution = Assert.Single(carolNotifs);
        Assert.Equal("TacheAssignee", attribution.Type);
        Assert.Equal(tacheId, attribution.TacheProductionId);

        // …et Bob le retrait : deux notifications utiles, pas davantage.
        var bobNotifs = await NotificationsAsync(bob.Id);
        Assert.Equal(2, bobNotifs.Count);
        Assert.Equal("TacheAssignee", bobNotifs[0].Type);
        var retrait = Assert.Single(bobNotifs, n => n.Type == "TacheDesassignee");
        Assert.Equal(tacheId, retrait.TacheProductionId);
        Assert.Contains("ne vous est plus assignée", retrait.Message);
    }

    // ══════════════ Test 2 — isolation de la liste ══════════════

    [Fact]
    public async Task N02_On_ne_voit_pas_les_notifications_d_autrui()
    {
        var alice = await CreateOperateurAsync("n02-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n02-bob");
        var tacheId = await CreerTacheAsync(alice, "N02 Cloche privée");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var clocheAlice = await LireClocheAsync(alice);
        Assert.Empty(clocheAlice.Notifications);
        Assert.Equal(0, clocheAlice.CountNonLivrees);

        var clocheBob = await LireClocheAsync(bob);
        Assert.Single(clocheBob.Notifications);
        Assert.Equal(1, clocheBob.CountNonLivrees);
    }

    [Fact]
    public async Task N02bis_Le_client_ne_peut_pas_demander_les_notifications_d_autrui()
    {
        var alice = await CreateOperateurAsync("n02b-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n02b-bob");
        var tacheId = await CreerTacheAsync(alice, "N02b Injection");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var notifBob = Assert.Single(await NotificationsAsync(bob.Id));

        // Aucun paramètre de destinataire n'est accepté : seuls les claims décident.
        foreach (var url in new[]
                 {
                     PlanningRoute + "/me?userId=" + bob.Id,
                     PlanningRoute + "/me?utilisateurId=" + bob.Id,
                     PlanningRoute + "/me?recipientUserId=" + bob.Id
                 })
        {
            var reponse = await alice.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
            var cloche = JsonSerializer.Deserialize<ClocheDto>(
                await reponse.Content.ReadAsStringAsync(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Empty(cloche.Notifications);
        }

        Assert.True(notifBob.Id > 0);
    }

    // ══════════════ Test 3 — IDOR sur lecture unitaire ══════════════

    [Fact]
    public async Task N03_Lire_une_notification_d_autrui_renvoie_404()
    {
        var alice = await CreateOperateurAsync("n03-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n03-bob");
        var tacheId = await CreerTacheAsync(alice, "N03 Notification d'autrui");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var notifBob = Assert.Single(await NotificationsAsync(bob.Id));

        // Alice ne voit que ses propres notifications : celle de Bob n'existe pas pour elle.
        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.Client.GetAsync($"{PlanningRoute}/{notifBob.Id}")).StatusCode);

        // Bob, lui, lit la sienne.
        Assert.Equal(HttpStatusCode.OK,
            (await bob.Client.GetAsync($"{PlanningRoute}/{notifBob.Id}")).StatusCode);
    }

    // ══════════════ Test 4 — marquage lu : jamais celui d'autrui ══════════════

    [Fact]
    public async Task N04_On_ne_peut_pas_marquer_lu_la_notification_d_autrui()
    {
        var alice = await CreateOperateurAsync("n04-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n04-bob");
        var tacheId = await CreerTacheAsync(alice, "N04 Marquage");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var notifBob = Assert.Single(await NotificationsAsync(bob.Id));

        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.Client.PostAsync($"{PlanningRoute}/{notifBob.Id}/livrer", null)).StatusCode);
        Assert.False((await NotificationsAsync(bob.Id))[0].EstLivree);

        // « Tout marquer lu » ne touche que ses propres lignes.
        Assert.Equal(HttpStatusCode.NoContent,
            (await alice.Client.PostAsync($"{PlanningRoute}/livrer-toutes", null)).StatusCode);
        Assert.False((await NotificationsAsync(bob.Id))[0].EstLivree);

        // Bob marque la sienne : le compteur retombe à zéro.
        Assert.Equal(HttpStatusCode.NoContent,
            (await bob.Client.PostAsync($"{PlanningRoute}/{notifBob.Id}/livrer", null)).StatusCode);
        var clocheBob = await LireClocheAsync(bob);
        Assert.Equal(0, clocheBob.CountNonLivrees);
        Assert.True(Assert.Single(clocheBob.Notifications).EstLivree);
    }

    // ══════════════ Test 5 — désassignation ══════════════

    [Fact]
    public async Task N05_Desassigner_previne_l_ancien_responsable_et_personne_si_il_n_y_en_avait_pas()
    {
        var alice = await CreateOperateurAsync("n05-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n05-bob");
        var carol = await CreateOperateurAsync("n05-carol");

        var tacheDeBob = await CreerTacheAsync(alice, "N05 Tâche de Bob");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheDeBob}/Assigner", new { assignedToUserId = bob.Id });

        // Désassignation explicite (null) : Bob perd la tâche.
        var desassignation = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheDeBob}/Assigner", new { assignedToUserId = (string?)null });
        Assert.Equal(HttpStatusCode.OK, desassignation.StatusCode);

        var bobNotifs = await NotificationsAsync(bob.Id);
        Assert.Equal(2, bobNotifs.Count);
        var retrait = Assert.Single(bobNotifs, n => n.Type == "TacheDesassignee");
        Assert.Equal(tacheDeBob, retrait.TacheProductionId);
        Assert.Contains("N05 Tâche de Bob", retrait.Message);

        // Une seconde désassignation n'a plus d'ancien responsable : personne n'est prévenu.
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheDeBob}/Assigner", new { assignedToUserId = (string?)null });
        Assert.Equal(2, (await NotificationsAsync(bob.Id)).Count);

        // Carol n'a jamais été responsable : son cloche reste vide malgré la désassignation.
        Assert.Empty(await NotificationsAsync(carol.Id));
        Assert.Empty(await NotificationsAsync(alice.Id));
    }

    // ══════════════ Test 6 — Email → tâche ══════════════

    [Fact]
    public async Task N06_Une_tache_creee_depuis_un_email_previne_son_responsable_designe()
    {
        var alice = await CreateOperateurAsync("n06-alice", peutAssigner: true);
        var carol = await CreateOperateurAsync("n06-carol");
        var messageId = await CreerMessageAsync(alice.Id, "Demande de devis", "CORPS CONFIDENTIEL à ne jamais exposer");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "N06 Traiter la demande de devis", assigneUserId = carol.Id });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var taskId = (await reponse.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["taskId"];
        var tacheId = int.Parse(taskId!.ToString()!);

        var carolNotifs = await NotificationsAsync(carol.Id);
        var notif = Assert.Single(carolNotifs);
        Assert.Equal("TacheDepuisEmail", notif.Type);
        Assert.Equal(tacheId, notif.TacheProductionId);
        Assert.Equal(messageId, notif.GmailMessageId);
        Assert.Contains("N06 Traiter la demande de devis", notif.Message);

        // Le message ne doit contenir NI le corps de l'email NI de donnée technique.
        Assert.DoesNotContain("CORPS CONFIDENTIEL", notif.Message);
        Assert.DoesNotContain("jeton", notif.Message, StringComparison.OrdinalIgnoreCase);

        // Alice, qui a validé, n'est pas prévenue : elle sait qu'elle a créé la tâche.
        Assert.Empty(await NotificationsAsync(alice.Id));

        // Carol reçoit bien la tâche (lien profond) mais pas l'email d'Alice : la notification
        // ne sert pas de raccourci pour contourner l'isolation des courriels.
        Assert.Equal(HttpStatusCode.OK,
            (await carol.Client.GetAsync($"/api/TacheProduction/{tacheId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await carol.Client.GetAsync($"/api/gmail/messages/{messageId}")).StatusCode);
    }

    [Fact]
    public async Task N06bis_Sans_designation_la_tache_depuis_email_ne_genere_aucune_notification()
    {
        var alice = await CreateOperateurAsync("n06b-alice");
        var messageId = await CreerMessageAsync(alice.Id, "Email traité par son auteur");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "N06b Tâche personnelle" });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var taskId = (await reponse.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["taskId"];
        var tacheId = int.Parse(taskId!.ToString()!);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == tacheId));
        Assert.Equal(alice.Id, tache.AssignedToUserId);
        Assert.Empty(await NotificationsAsync(alice.Id));
    }

    // ══════════════ Test 7 — déduplication ══════════════

    [Fact]
    public async Task N07_Une_operation_unique_ne_produit_pas_de_doublon()
    {
        var alice = await CreateOperateurAsync("n07-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n07-bob");
        var tacheId = await CreerTacheAsync(alice, "N07 Idempotence");

        // Deux appels identiques : le second ne change rien, donc n'informe personne.
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var bobNotifs = await NotificationsAsync(bob.Id);
        var notif = Assert.Single(bobNotifs);
        Assert.Equal("TacheAssignee", notif.Type);
        Assert.Equal(tacheId, notif.TacheProductionId);

        // La cloche n'expose qu'une seule ligne et un compteur cohérent.
        var cloche = await LireClocheAsync(bob);
        Assert.Single(cloche.Notifications);
        Assert.Equal(1, cloche.CountNonLivrees);
    }

    [Fact]
    public async Task N07bis_Une_tache_ne_cree_qu_un_seul_evenement_par_destinataire()
    {
        var alice = await CreateOperateurAsync("n07b-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n07b-bob");

        // Création d'une tâche déjà désignée à Bob : une seule notification, pas deux
        // (la création et l'assignation ne sont pas deux événements distincts).
        var reponse = await alice.Client.PostAsJsonAsync("/api/TacheProduction", new
        {
            titre = "N07b Tâche désignée à la création",
            assignedToUserId = bob.Id
        });
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var tacheId = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .Where(t => t.Titre == "N07b Tâche désignée à la création")
                .Select(t => t.Id).SingleAsync());

        var notif = Assert.Single(await NotificationsAsync(bob.Id));
        Assert.Equal("TacheAssignee", notif.Type);
        Assert.Equal(tacheId, notif.TacheProductionId);
    }

    [Fact]
    public async Task N07ter_Une_tache_reste_sans_notification_pour_son_auteur()
    {
        var alice = await CreateOperateurAsync("n07c-alice");
        await CreerTacheAsync(alice, "N07c Tâche personnelle");

        // Auto-attribution : l'auteur est le responsable, aucune cloche.
        Assert.Empty(await NotificationsAsync(alice.Id));
    }

    // ══════════════ Test 8 — deep-link : le DTO porte bien la cible ══════════════

    [Fact]
    public async Task N08_Le_dto_de_notification_expose_la_cible_du_lien_profond()
    {
        var alice = await CreateOperateurAsync("n08-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n08-bob");
        var tacheId = await CreerTacheAsync(alice, "N08 Cible cliquable");
        await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/Assigner", new { assignedToUserId = bob.Id });

        var notif = Assert.Single(await NotificationsAsync(bob.Id));

        // Le frontend construit « /taches?taskId=… » à partir de cet identifiant…
        Assert.Equal(tacheId, notif.TacheProductionId);
        // …et n'invente pas de cible pour les autres types.
        Assert.Null(notif.PlanningEntryId);
        Assert.Null(notif.GmailMessageId);

        // La notification n'accorde aucun droit : l'accès à la tâche reste filtré par l'API.
        var carol = await CreateOperateurAsync("n08-carol");
        Assert.Equal(HttpStatusCode.NotFound,
            (await carol.Client.GetAsync($"/api/TacheProduction/{tacheId}")).StatusCode);
        Assert.Empty(await NotificationsAsync(carol.Id));
    }

    [Fact]
    public async Task N08bis_Une_notification_liee_a_un_email_expose_l_email_et_la_tache()
    {
        var alice = await CreateOperateurAsync("n08b-alice", peutAssigner: true);
        var bob = await CreateOperateurAsync("n08b-bob");
        var messageId = await CreerMessageAsync(alice.Id, "Email de N08b");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "N08b Tâche depuis email", assigneUserId = bob.Id });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var notif = Assert.Single(await NotificationsAsync(bob.Id));
        Assert.Equal("TacheDepuisEmail", notif.Type);
        Assert.Equal(messageId, notif.GmailMessageId);
        Assert.NotNull(notif.TacheProductionId);

        // Identifiants d'URL exploitsables uniquement s'ils sont des entiers positifs.
        Assert.True(notif.GmailMessageId > 0);
        Assert.True(notif.TacheProductionId > 0);
    }

    // ══════════════ Test 9 — non-régression du planning ══════════════

    [Fact]
    public async Task N09_Les_notifications_de_planning_continuent_de_fonctionner()
    {
        var auteur = await _factory.CreateUserAsync("n09-auteur", "A", "AuteurPlanning", role =>
        {
            role.PeutVoirPlanning = true;
            role.PeutGererPlanning = true;
        });
        var lecteur = await _factory.CreateUserAsync("n09-lecteur", "L", "LecteurPlanning", role =>
        {
            role.PeutVoirPlanning = true;
        });

        // Une cellule de planning notifie TOUS les utilisateurs actifs (comportement initial).
        var chaineId = await _factory.WithDbAsync(async db =>
        {
            var chaine = new ChaineProduction { Nom = "Chaîne N09", EstActif = true };
            db.ChainesProduction.Add(chaine);
            await db.SaveChangesAsync();
            return chaine.Id;
        });

        var reponse = await auteur.Client.PostAsJsonAsync("/api/Planning", new
        {
            chaineProductionId = chaineId,
            dateSamedi = "2026-10-03T00:00:00Z",
            numeroCommande = "79-PO33341",
            quantite = 10,
            estLivree = false,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        // Le lecteur n'a aucun droit d'écriture : sa cloche doit malgré tout bouger.
        var planningLecteur = await NotificationsAsync(lecteur.Id);
        var notifPlanning = planningLecteur.SingleOrDefault(n => n.Message.Contains("planning a changé"));
        Assert.NotNull(notifPlanning);
        // Le type par défaut reste bien Planning : la cloche n'a pas perdu sa sémantique.
        Assert.Equal("Planning", notifPlanning!.Type);
        Assert.Null(notifPlanning.TacheProductionId);
        Assert.Null(notifPlanning.GmailMessageId);
        Assert.NotNull(notifPlanning.PlanningEntryId);

        // Compteur de la cloche et marquage « tout lu » inchangés.
        var cloche = await LireClocheAsync(lecteur);
        Assert.True(cloche.CountNonLivrees >= 1);
        Assert.Equal(HttpStatusCode.NoContent,
            (await lecteur.Client.PostAsync(PlanningRoute + "/livrer-toutes", null)).StatusCode);
        Assert.Equal(0, (await LireClocheAsync(lecteur)).CountNonLivrees);
    }

    // ══════════════ Test 10 — hygienic du destinataire supprimé ══════════════

    [Fact]
    public async Task N10_La_suppression_d_un_utilisateur_emporte_ses_notifications()
    {
        var auteur = await CreateOperateurAsync("n10-auteur", peutAssigner: true);
        var cible = await CreateOperateurAsync("n10-cible");

        // Tâche créée SANS responsable (elle reste à l'auteur), puis confiée à la cible :
        // c'est ce changement de responsable qui notifie.
        var tacheId = await CreerTacheAsync(auteur, "Tâche N10");
        var assignation = await auteur.Client.PostAsJsonAsync(
            $"/api/TacheProduction/{tacheId}/assigner", new { assignedToUserId = cible.Id });
        Assert.Equal(HttpStatusCode.OK, assignation.StatusCode);

        Assert.NotEmpty(await NotificationsAsync(cible.Id));

        // Sans purge, les notifications d'un compte supprimé resteraient orphelines
        // (aucune FK vers AspNetUsers) : la table grossirait sans fin.
        var administrateur = await _factory.CreateUserAsync("n10-admin", "A", "Admin", role =>
        {
            role.EstAdministrateur = true;
            role.PeutGererUtilisateurs = true;
        });
        var reponseSuppression =
            await administrateur.Client.DeleteAsync($"/api/Account/users/{cible.Id}");

        Assert.Equal(HttpStatusCode.NoContent, reponseSuppression.StatusCode);
        Assert.Empty(await NotificationsAsync(cible.Id));
        Assert.Empty(await _factory.WithDbAsync(db =>
            db.Notifications.AsNoTracking().Where(n => n.UtilisateurId == cible.Id).ToListAsync()));
    }
}
