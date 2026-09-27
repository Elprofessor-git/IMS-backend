using System.Net;
using System.Net.Http.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Intégration Email → Tâche.
///
/// Ces tests ne touchent pas à l'architecture Gmail existante : ils vérifient que
/// la création d'une tâche depuis un email conserve l'isolation par utilisateur
/// (un message n'est visible que par son propriétaire) et que la propriété de la
/// tâche est décidée côté serveur.
/// </summary>
public class GmailTacheIntegrationTests : IClassFixture<TacheApiFactory>
{
    private readonly TacheApiFactory _factory;

    public GmailTacheIntegrationTests(TacheApiFactory factory) => _factory = factory;

    /// <summary>Utilisateur autorisé à lire/gérer les courriels et le module tâches.</summary>
    private Task<TestUser> CreateCourrielAsync(string id, bool peutAssigner = false) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = peutAssigner;
        });

    /// <summary>
    /// Insère une connexion Gmail et un message pour l'utilisateur donné, sans passer
    /// par les services OAuth (hors périmètre de ces tests).
    /// </summary>
    private async Task<int> CreerMessageAsync(string userId, string objet)
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
                GmailMessageId = "msg-" + objet,
                GmailThreadId = "thread-" + objet,
                From = "expediteur@exemple.fr",
                To = userId + "@ims.test",
                Subject = objet,
                BodyText = "Contenu de l'email",
                ReceivedAt = DateTime.Now
            };
            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });
    }

    // ══════════════ Isolation : un message n'est lisible que par son propriétaire ══════════════

    [Fact]
    public async Task G01_Un_message_n_est_lisible_que_par_son_proprietaire()
    {
        var alice = await CreateCourrielAsync("g01-alice");
        var bob = await CreateCourrielAsync("g01-bob");

        var messageAlice = await CreerMessageAsync(alice.Id, "Dossier Alice");
        var messageBob = await CreerMessageAsync(bob.Id, "Dossier Bob");

        // Chacun voit son propre message.
        Assert.Equal(HttpStatusCode.OK,
            (await alice.Client.GetAsync($"/api/gmail/messages/{messageAlice}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await bob.Client.GetAsync($"/api/gmail/messages/{messageBob}")).StatusCode);

        // Le message de l'autre renvoie 404 (et non 403) : on ne confirme pas son existence.
        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.Client.GetAsync($"/api/gmail/messages/{messageBob}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.Client.GetAsync($"/api/gmail/messages/{messageAlice}")).StatusCode);
    }

    // ══════════════ La tâche créée depuis un email appartient à son auteur ══════════════

    [Fact]
    public async Task G02_La_tache_creee_depuis_un_email_appartient_a_celui_qui_valide()
    {
        var alice = await CreateCourrielAsync("g02-alice");
        var messageId = await CreerMessageAsync(alice.Id, "Demande de devis");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "Traiter la demande de devis", priorite = "Haute" });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var tacheId = (await reponse.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["taskId"];
        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .SingleAsync(t => t.Id == int.Parse(tacheId!.ToString()!)));

        // Le propriétaire est l'utilisateur IMS qui a validé, jamais l'expéditeur de l'email
        // ni une valeur envoyée par le client.
        Assert.Equal(alice.Id, tache.CreatedByUserId);
        Assert.Equal(alice.Id, tache.AssignedToUserId);

        // Le message pointe désormais vers la tâche créée.
        var message = await _factory.WithDbAsync(db =>
            db.GmailMessages.AsNoTracking().SingleAsync(m => m.Id == messageId));
        Assert.Equal(tache.Id, message.CreatedTaskId);
    }

    [Fact]
    public async Task G03_Le_client_ne_peut_pas_imposer_le_createur_de_la_tache_issue_d_un_email()
    {
        var alice = await CreateCourrielAsync("g03-alice");
        var bob = await CreateCourrielAsync("g03-bob");
        var messageId = await CreerMessageAsync(alice.Id, "Email de Alice");

        // Alice tente d'attribuer la tâche à Bob (qui n'a aucun droit d'assignation)
        // et d'usurper la propriété via des champs non exposés.
        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new
            {
                titre = "Tâche injectée",
                createdByUserId = bob.Id,
                assigneeUserId = bob.Id,
                assigneUserId = bob.Id
            });

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);

        // Aucune tâche n'a été créée.
        Assert.False(await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().AnyAsync(t => t.Titre == "Tâche injectée")));
    }

    [Fact]
    public async Task G04_Un_utilisateur_ne_peut_pas_creer_de_tache_depuis_le_message_d_autrui()
    {
        var alice = await CreateCourrielAsync("g04-alice");
        var bob = await CreateCourrielAsync("g04-bob");
        var messageAlice = await CreerMessageAsync(alice.Id, "Email privé de Alice");

        var reponse = await bob.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageAlice}/analysis/approve",
            new { titre = "Tâche depuis un email d'autrui" });

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        Assert.False(await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .AnyAsync(t => t.Titre == "Tâche depuis un email d'autrui")));
    }

    [Fact]
    public async Task G05_Avec_le_droit_dassignation_la_tache_peut_etre_designee_a_un_tiers()
    {
        var alice = await CreateCourrielAsync("g05-alice", peutAssigner: true);
        var carol = await CreateCourrielAsync("g05-carol");
        var messageId = await CreerMessageAsync(alice.Id, "Email à déléguer");

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "À traiter par Carol", assigneUserId = carol.Id });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var tacheId = (await reponse.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["taskId"];
        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .SingleAsync(t => t.Id == int.Parse(tacheId!.ToString()!)));

        // Le créateur reste Alice, seule l'assignation est déléguée.
        Assert.Equal(alice.Id, tache.CreatedByUserId);
        Assert.Equal(carol.Id, tache.AssignedToUserId);
    }

    [Fact]
    public async Task G06_Une_tache_ne_pourrait_pas_etre_assignee_a_un_utilisateur_inactif()
    {
        var alice = await CreateCourrielAsync("g06-alice", peutAssigner: true);
        var inactif = await _factory.CreateUserAsync("g06-inactif", "I", "Inactif", role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
        });
        await _factory.WithDbAsync(db =>
        {
            var u = db.Users.Single(u => u.Id == inactif.Id);
            u.EstActif = false;
            return db.SaveChangesAsync();
        });

        var messageId = await CreerMessageAsync(alice.Id, "Email vers utilisateur inactif");
        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "Tâche pour un inactif", assigneUserId = inactif.Id });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.False(await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().AnyAsync(t => t.Titre == "Tâche pour un inactif")));
    }

    [Fact]
    public async Task G07_Valider_deux_fois_le_meme_email_ne_cree_quune_seule_tache()
    {
        var alice = await CreateCourrielAsync("g07-alice");
        var messageId = await CreerMessageAsync(alice.Id, "Email unique");

        var premiere = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "Première tâche" });
        Assert.Equal(HttpStatusCode.OK, premiere.StatusCode);

        var seconde = await alice.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/analysis/approve",
            new { titre = "Seconde tâche" });

        Assert.Equal(HttpStatusCode.Conflict, seconde.StatusCode);
        Assert.Equal(1, await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .CountAsync(t => t.Titre == "Première tâche" || t.Titre == "Seconde tâche")));
    }

    [Fact]
    public async Task G08_Sans_droit_courriels_l_utilisateur_obtient_403()
    {
        var alice = await CreateCourrielAsync("g08-alice");
        var sansDroit = await _factory.CreateUserAsync("g08-sansdroit", "S", "SansDroit", role =>
        {
            role.PeutVoirCourriels = false;
            role.PeutGererCourriels = false;
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
        });

        var messageId = await CreerMessageAsync(alice.Id, "Email protégé");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await sansDroit.Client.GetAsync($"/api/gmail/messages/{messageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await sansDroit.Client.PostAsJsonAsync(
                $"/api/gmail/messages/{messageId}/analysis/approve",
                new { titre = "Tâche non autorisée" })).StatusCode);
    }
}
