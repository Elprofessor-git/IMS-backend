using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Actions de boîte : lu/non-lu, étoile, archive, corbeille.
/// <para>
/// Trois propriétés sont vérifiées, dans cet ordre d'importance :
/// <list type="number">
/// <item><b>Gmail d'abord</b> : si l'appel à Gmail échoue, la base IMS ne doit pas bouger
/// (un état local optimiste mentirait à l'utilisateur) ;</item>
/// <item><b>le miroir local</b> suit l'étiquette réellement posée ;</item>
/// <item><b>la liste se vide</b> de ce qui n'est plus dans la boîte de réception.</item>
/// </list>
/// </para>
/// </summary>
public class GmailMessageActionTests : IClassFixture<GmailActionApiFactory>
{
    private readonly GmailActionApiFactory _factory;

    public GmailMessageActionTests(GmailActionApiFactory factory) => _factory = factory;

    private Task<TestUser> CreateUserAsync(string id) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
        });

    private async Task EnsureConnectionAsync(string userId, string suffixe)
    {
        await _factory.WithDbAsync(async db =>
        {
            if (await db.GmailConnections.AnyAsync(c => c.UserId == userId)) return;

            db.GmailConnections.Add(new GmailConnection
            {
                UserId = userId,
                GmailAddress = userId + "@gmail.test",
                GoogleUserId = "google-" + userId,
                RefreshTokenEncrypted = "jeton-chiffre-de-test",
                GrantedScopes = "https://www.googleapis.com/auth/gmail.modify",
                ConnectedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
    }

    /// <summary>labelsJson = null simule un email importé avant le stockage des étiquettes.</summary>
    private async Task<(int MessageId, string GmailMessageId)> SeedMessageAsync(
        string userId, string suffixe, string? labelsJson, bool isRead = true)
    {
        return await _factory.WithDbAsync(async db =>
        {
            var connection = await db.GmailConnections.FirstAsync(c => c.UserId == userId);

            var message = new GmailMessage
            {
                GmailConnectionId = connection.Id,
                GmailMessageId = "msg-" + suffixe,
                GmailThreadId = "thread-" + suffixe,
                From = "client@exemple.fr",
                To = userId + "@ims.test",
                Subject = "Commande 4821",
                BodyText = "Contenu",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-1),
                IsRead = isRead,
                LabelsJson = labelsJson,
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };

            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();
            return (message.Id, message.GmailMessageId);
        });
    }

    private async Task<HashSet<string>> ListThreadsAsync(TestUser user)
    {
        var result = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/threads");
        return result.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("lastGmailMessageId").GetString()!)
            .ToHashSet();
    }

    private async Task<GmailMessage> ReadMessageAsync(int messageId) =>
        await _factory.WithDbAsync(db => db.GmailMessages.AsNoTracking().FirstAsync(m => m.Id == messageId));

    /// <summary>
    /// Le double est un singleton partagé par tous les tests de la classe : on filtre donc
    /// sur l'identifiant du message du test courant, sinon on compterait les appels des
    /// tests voisins (xUnit exécute les méthodes d'une classe en parallèle).
    /// </summary>
    private List<RecordingGmailApiService.ModifyCall> ModifiesFor(string gmailMessageId) =>
        _factory.Recorder.Modifies.Where(c => c.MessageId == gmailMessageId).ToList();

    private List<RecordingGmailApiService.TrashCall> TrashesFor(string gmailMessageId) =>
        _factory.Recorder.Trashes.Where(c => c.MessageId == gmailMessageId).ToList();

    private List<RecordingGmailApiService.ModifyCall> ThreadModifiesFor(string gmailThreadId) =>
        _factory.Recorder.ThreadModifies.Where(c => c.MessageId == gmailThreadId).ToList();

    private List<RecordingGmailApiService.TrashCall> ThreadTrashesFor(string gmailThreadId) =>
        _factory.Recorder.ThreadTrashes.Where(c => c.MessageId == gmailThreadId).ToList();

    private async Task<HashSet<string>> ThreadIdsAsync(TestUser user)
    {
        var result = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/threads");
        return result.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("gmailThreadId").GetString()!)
            .ToHashSet();
    }

    // ── Étiquettes ───────────────────────────────────────────────────────

    [Fact]
    public async Task A1_Marquer_non_lu_demande_l_etiquette_UNREAD_a_gmail()
    {
        var user = await CreateUserAsync("gmail-act-a1");
        await EnsureConnectionAsync(user.Id, "a1");
        var (messageId, gmailMessageId) = await SeedMessageAsync(user.Id, "a1", "[\"INBOX\"]", isRead: true);

        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { isRead = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isRead").GetBoolean());

        var call = Assert.Single(ModifiesFor(gmailMessageId));
        Assert.Contains("UNREAD", call.Added);
        Assert.DoesNotContain("UNREAD", call.Removed);

        var message = await ReadMessageAsync(messageId);
        Assert.False(message.IsRead);
        Assert.Contains("UNREAD", message.LabelsJson);
    }

    [Fact]
    public async Task A2_L_etoile_demande_l_etiquette_STARRED()
    {
        var user = await CreateUserAsync("gmail-act-a2");
        await EnsureConnectionAsync(user.Id, "a2");
        var (messageId, gmailMessageId) = await SeedMessageAsync(user.Id, "a2", "[\"INBOX\"]");

        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { isStarred = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(ModifiesFor(gmailMessageId));
        Assert.Contains("STARRED", call.Added);

        var message = await ReadMessageAsync(messageId);
        Assert.True(message.IsStarred);
    }

    [Fact]
    public async Task A3_L_archive_retire_INBOX_et_le_message_quitte_la_liste()
    {
        var user = await CreateUserAsync("gmail-act-a3");
        await EnsureConnectionAsync(user.Id, "a3");
        var (messageId, gmailMessageId) = await SeedMessageAsync(user.Id, "a3", "[\"INBOX\",\"CATEGORIAL_PROMOTIONS\"]");

        Assert.Contains(gmailMessageId, await ListThreadsAsync(user));

        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { archive = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isArchived").GetBoolean());

        var call = Assert.Single(ModifiesFor(gmailMessageId));
        Assert.Contains("INBOX", call.Removed);

        Assert.DoesNotContain(gmailMessageId, await ListThreadsAsync(user));
    }

    [Fact]
    public async Task A4_La_corbeille_passe_par_trash_et_non_par_modify()
    {
        var user = await CreateUserAsync("gmail-act-a4");
        await EnsureConnectionAsync(user.Id, "a4");
        var (messageId, gmailMessageId) = await SeedMessageAsync(user.Id, "a4", "[\"INBOX\"]");

        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { trash = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isTrashed").GetBoolean());
        Assert.True(body.GetProperty("isArchived").GetBoolean());

        // trash et non modify : Gmail gère seul le retrait de INBOX et l'ajout de TRASHED.
        Assert.Single(TrashesFor(gmailMessageId));
        Assert.Empty(ModifiesFor(gmailMessageId));

        Assert.DoesNotContain(gmailMessageId, await ListThreadsAsync(user));
    }

    // ── Sécurité de l'état local ────────────────────────────────────────

    [Fact]
    public async Task A5_Un_echec_gmail_laisse_la_base_ims_intacte()
    {
        var user = await CreateUserAsync("gmail-act-a5");
        await EnsureConnectionAsync(user.Id, "a5");
        var (messageId, gmailMessageId) = await SeedMessageAsync(user.Id, "a5", "[\"INBOX\"]", isRead: true);

        _factory.Recorder.FailingMessageId = gmailMessageId;

        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { isRead = false });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        // Le point essentiel : pas d'état optimiste. Le message reste lu, INBOX intact.
        var message = await ReadMessageAsync(messageId);
        Assert.True(message.IsRead);
        Assert.DoesNotContain("UNREAD", message.LabelsJson);
        Assert.Contains("INBOX", message.LabelsJson);

        _factory.Recorder.FailingMessageId = null;
    }

    [Fact]
    public async Task A6_On_ne_peut_pas_agir_sur_le_message_d_un_autrui()
    {
        var alice = await CreateUserAsync("gmail-act-a6-alice");
        var bob = await CreateUserAsync("gmail-act-a6-bob");
        await EnsureConnectionAsync(alice.Id, "a6a");
        await EnsureConnectionAsync(bob.Id, "a6b");

        var (aliceMessage, aliceGmailId) = await SeedMessageAsync(alice.Id, "a6a", "[\"INBOX\"]");

        var response = await bob.Client.PatchAsJsonAsync($"/api/gmail/messages/{aliceMessage}", new { isRead = false });

        // 404 et non 403 : l'existence du message d'autrui ne doit pas être révélée.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(ModifiesFor(aliceGmailId));
        Assert.Empty(TrashesFor(aliceGmailId));
    }

    [Fact]
    public async Task A7_Sans_droit_d_ecriture_l_action_est_refusee()
    {
        var lecteur = await _factory.CreateUserAsync("gmail-act-a7", "L", "C");
        await EnsureConnectionAsync(lecteur.Id, "a7");
        var (messageId, gmailMessageId) = await SeedMessageAsync(lecteur.Id, "a7", "[\"INBOX\"]");

        var response = await lecteur.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { trash = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(TrashesFor(gmailMessageId));
    }

    // ── Non-régression du filtre « boîte de réception » ────────────────

    [Fact]
    public async Task A8_Un_email_ancien_sans_etiquettes_reste_dans_la_boite_de_reception()
    {
        var user = await CreateUserAsync("gmail-act-a8");
        await EnsureConnectionAsync(user.Id, "a8");

        // LabelsJson nul = synchronisé avant l'existence du stockage des étiquettes.
        var (ancienId, ancienGmailId) = await SeedMessageAsync(user.Id, "a8-ancien", labelsJson: null);
        var (_, archiveGmailId) = await SeedMessageAsync(user.Id, "a8-archive", labelsJson: "[\"ARCHIVED\"]");
        var (_, poubelleGmailId) = await SeedMessageAsync(user.Id, "a8-poubelle", labelsJson: "[\"TRASHED\"]");

        var liste = await ListThreadsAsync(user);

        Assert.Contains(ancienGmailId, liste);
        Assert.DoesNotContain(archiveGmailId, liste);
        Assert.DoesNotContain(poubelleGmailId, liste);
        Assert.True(ancienId > 0);
    }

    [Fact]
    public async Task A9_La_liste_des_messages_applique_le_meme_filtre_que_les_fils()
    {
        var user = await CreateUserAsync("gmail-act-a9");
        await EnsureConnectionAsync(user.Id, "a9");

        await SeedMessageAsync(user.Id, "a9-inbox", labelsJson: "[\"INBOX\"]");
        await SeedMessageAsync(user.Id, "a9-archive", labelsJson: "[\"ARCHIVED\"]");

        var result = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages");
        var ids = result.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("gmailMessageId").GetString()!)
            .ToList();

        Assert.Contains("msg-a9-inbox", ids);
        Assert.DoesNotContain("msg-a9-archive", ids);
        Assert.Equal(1, result.GetProperty("total").GetInt32());
    }

    // ── Actions au niveau de la conversation ─────────────────────────────

    [Fact]
    public async Task A10_Marquer_un_fil_comme_lu_utilise_un_seul_appel_gmail()
    {
        var user = await CreateUserAsync("gmail-act-a10");
        await EnsureConnectionAsync(user.Id, "a10");

        var (m1, _) = await SeedMessageAsync(user.Id, "a10-1", "[\"INBOX\"]", isRead: false);
        var (m2, _) = await SeedMessageAsync(user.Id, "a10-2", "[\"INBOX\"]", isRead: false);
        await _factory.WithDbAsync(async db =>
        {
            // Les deux messages partagent le même fil.
            var connection = await db.GmailConnections.FirstAsync(c => c.UserId == user.Id);
            var messages = await db.GmailMessages
                .Where(m => m.Id == m1 || m.Id == m2)
                .ToListAsync();
            foreach (var m in messages) m.GmailThreadId = "thread-a10";
            await db.SaveChangesAsync();
        });

        var response = await user.Client.PatchAsJsonAsync("/api/gmail/threads/thread-a10", new { isRead = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("messageCount").GetInt32());
        Assert.True(body.GetProperty("isRead").GetBoolean());

        // Un seul appel, au niveau du fil : c'est le but de l'endpoint.
        Assert.Single(ThreadModifiesFor("thread-a10"));
        Assert.Empty(ModifiesFor("msg-a10-1"));
        Assert.Empty(ModifiesFor("msg-a10-2"));

        var flags = await _factory.WithDbAsync(async db => await db.GmailMessages
            .Where(m => m.Id == m1 || m.Id == m2)
            .Select(m => new { m.IsRead })
            .ToListAsync());
        Assert.All(flags, f => Assert.True(f.IsRead));
    }

    [Fact]
    public async Task A11_La_corbeille_d_un_fil_appelle_trash_une_seule_fois()
    {
        var user = await CreateUserAsync("gmail-act-a11");
        await EnsureConnectionAsync(user.Id, "a11");

        var (m1, _) = await SeedMessageAsync(user.Id, "a11-1", "[\"INBOX\"]");
        var (m2, _) = await SeedMessageAsync(user.Id, "a11-2", "[\"INBOX\"]");
        await _factory.WithDbAsync(async db =>
        {
            var messages = await db.GmailMessages.Where(m => m.Id == m1 || m.Id == m2).ToListAsync();
            foreach (var m in messages) m.GmailThreadId = "thread-a11";
            await db.SaveChangesAsync();
        });

        var response = await user.Client.PatchAsJsonAsync("/api/gmail/threads/thread-a11", new { trash = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isTrashed").GetBoolean());

        Assert.Single(ThreadTrashesFor("thread-a11"));
        Assert.Empty(ThreadModifiesFor("thread-a11"));

        // Le fil disparaît de la liste, ses deux lignes sont marquées TRASHED.
        Assert.DoesNotContain("thread-a11", await ThreadIdsAsync(user));
        var labels = await _factory.WithDbAsync(async db => await db.GmailMessages
            .Where(m => m.Id == m1 || m.Id == m2)
            .Select(m => m.LabelsJson!)
            .ToListAsync());
        Assert.All(labels, l => Assert.Contains("TRASHED", l));
        Assert.All(labels, l => Assert.DoesNotContain("INBOX", l));
    }

    [Fact]
    public async Task A12_On_ne_peut_pas_agir_sur_le_fil_d_un_autrui()
    {
        var alice = await CreateUserAsync("gmail-act-a12-alice");
        var bob = await CreateUserAsync("gmail-act-a12-bob");
        await EnsureConnectionAsync(alice.Id, "a12a");
        await EnsureConnectionAsync(bob.Id, "a12b");
        await SeedMessageAsync(alice.Id, "a12a", "[\"INBOX\"]");

        var response = await bob.Client.PatchAsJsonAsync("/api/gmail/threads/thread-a12a", new { trash = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(ThreadTrashesFor("thread-a12a"));
    }

    [Fact]
    public async Task A13_Un_echec_gmail_sur_un_fil_ne_laisse_aucun_milieu_d_etat()
    {
        var user = await CreateUserAsync("gmail-act-a13");
        await EnsureConnectionAsync(user.Id, "a13");

        var (m1, _) = await SeedMessageAsync(user.Id, "a13-1", "[\"INBOX\"]", isRead: false);
        var (m2, _) = await SeedMessageAsync(user.Id, "a13-2", "[\"INBOX\"]", isRead: false);
        await _factory.WithDbAsync(async db =>
        {
            var messages = await db.GmailMessages.Where(m => m.Id == m1 || m.Id == m2).ToListAsync();
            foreach (var m in messages) m.GmailThreadId = "thread-a13";
            await db.SaveChangesAsync();
        });

        _factory.Recorder.FailingMessageId = "thread-a13";

        var response = await user.Client.PatchAsJsonAsync("/api/gmail/threads/thread-a13", new { isRead = true });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        // Aucun message du fil ne doit être à moitié traité.
        var flags = await _factory.WithDbAsync(async db => await db.GmailMessages
            .Where(m => m.Id == m1 || m.Id == m2)
            .Select(m => new { m.IsRead, m.LabelsJson })
            .ToListAsync());
        Assert.All(flags, f => Assert.False(f.IsRead));
        Assert.All(flags, f => Assert.DoesNotContain("UNREAD", f.LabelsJson ?? ""));

        _factory.Recorder.FailingMessageId = null;
    }

    [Fact]
    public async Task A14_Des_etiquettes_absentes_ne_creent_pas_de_ligne_dans_le_fil()
    {
        var user = await CreateUserAsync("gmail-act-a14");
        await EnsureConnectionAsync(user.Id, "a14");
        var (messageId, _) = await SeedMessageAsync(user.Id, "a14", "[\"INBOX\"]");

        // Requête vide : ni une étiquette, ni une action.
        var response = await user.Client.PatchAsJsonAsync($"/api/gmail/messages/{messageId}", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(ModifiesFor("msg-a14"));
        Assert.Empty(TrashesFor("msg-a14"));
    }
}
