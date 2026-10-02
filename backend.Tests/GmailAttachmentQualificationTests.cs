using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Régression A2 : une pièce jointe ne doit PLUS être classée « inline » sur la seule
/// présence d'un Content-ID.
/// <para>
/// L'ancien critère (<c>isInline = !string.IsNullOrEmpty(contentId)</c>) était faux :
/// beaucoup de clients de messagerie posent un Content-ID sur des pièces jointes
/// ordinaires. Ces pièces étaient alors filtrées côté UI (donc invisibles) et le
/// trombone disparaissait de l'en-tête. La règle correcte est le Content-Disposition :
/// « attachment » l'emporte même avec un Content-ID, et « inline » ne vaut que si le
/// corps RÉFÉRENCE réellement l'identifiant par cid:.
/// </para>
/// <para>
/// Ces tests visent la qualification elle-même, via l'endpoint de maintenance alimenté
/// par le double enregistreur : aucun appel à l'API Google réelle.
/// </para>
/// </summary>
public class GmailAttachmentQualificationTests : IClassFixture<GmailActionApiFactory>
{
    private readonly GmailActionApiFactory _factory;

    public GmailAttachmentQualificationTests(GmailActionApiFactory factory) => _factory = factory;

    private Task<TestUser> CreateUserAsync(string id, bool admin = false) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
            role.EstAdministrateur = admin;
        });

    private async Task EnsureConnectionAsync(string userId)
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

    /// <summary>
    /// Message + pièces en base, avec la qualification PÉRIMÉE (inline = tout ce qui a
    /// un Content-ID), exactement comme l'a produit la synchronisation d'avant le correctif.
    /// </summary>
    private async Task<int> SeedMessageWithAttachmentsAsync(
        string userId, string suffixe, params (string Id, string Name, string Mime, string Cid)[] pieces)
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
                Subject = "Message " + suffixe,
                BodyText = "Contenu",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-1),
                IsRead = true,
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };
            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();

            foreach (var p in pieces)
            {
                db.GmailAttachments.Add(new GmailAttachment
                {
                    GmailMessageId = message.Id,
                    GmailAttachmentId = p.Id,
                    FileName = p.Name,
                    MimeType = p.Mime,
                    SizeBytes = 1024,
                    // Qualification d'origine : inline dès qu'il y a un Content-ID.
                    IsInline = !string.IsNullOrEmpty(p.Cid),
                    ContentId = string.IsNullOrEmpty(p.Cid) ? null : p.Cid,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
            return message.Id;
        });
    }

    private Task<List<GmailAttachment>> ReadAttachmentsAsync(int messageId) =>
        _factory.WithDbAsync(db => db.GmailAttachments
            .Where(a => a.GmailMessageId == messageId)
            .ToListAsync());

    private async Task<JsonElement> RequalifyAsync(TestUser user, string query = "")
    {
        var response = await user.Client.PostAsync(
            "/api/gmail/maintenance/attachments/requalify" + query, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── Le cas du rapport : un PDF avec Content-ID est une PIÈCE JOINTE ──────

    [Fact]
    public async Task Piece_avec_Content_ID_mais_Content_Disposition_attachment_reste_piece_jointe()
    {
        var user = await CreateUserAsync("a2-pdf", admin: true);
        await EnsureConnectionAsync(user.Id);

        var messageId = await SeedMessageWithAttachmentsAsync(
            user.Id, "a2pdf", ("att-1", "plan-de-coupe.pdf", "application/pdf", "cid:logo-interne"));

        // Gmail dit : attachment, malgré le Content-ID.
        _factory.Recorder.MessageCatalog["msg-a2pdf"] = RecordingGmailApiService.Message("msg-a2pdf", new List<GmailApiAttachment>
        {
            new("att-1", "plan-de-coupe.pdf", "application/pdf", 1024,
                IsInline: false, ContentId: null)
        });

        await RequalifyAsync(user);

        var apres = await ReadAttachmentsAsync(messageId);
        var pdf = Assert.Single(apres);
        Assert.False(pdf.IsInline);
        Assert.Null(pdf.ContentId);
    }

    // ── Une vraie image intégrée reste inline ───────────────────────────────

    [Fact]
    public async Task Image_inline_avec_Content_ID_reste_inline()
    {
        var user = await CreateUserAsync("a2-img", admin: true);
        await EnsureConnectionAsync(user.Id);

        var messageId = await SeedMessageWithAttachmentsAsync(
            user.Id, "a2img", ("att-logo", null, "image/png", "cid:logo1"));

        _factory.Recorder.MessageCatalog["msg-a2img"] = RecordingGmailApiService.Message("msg-a2img", new List<GmailApiAttachment>
        {
            new("att-logo", null, "image/png", 512, IsInline: true, ContentId: "logo1")
        });

        await RequalifyAsync(user);

        var logo = Assert.Single(await ReadAttachmentsAsync(messageId));
        Assert.True(logo.IsInline);
        Assert.Equal("logo1", logo.ContentId);
    }

    // ── Un Content-ID sans Content-Disposition explicite, sur une image ──────

    [Fact]
    public async Task Piece_non_image_avec_Content_ID_est_reclassee_en_piece_jointe()
    {
        var user = await CreateUserAsync("a2-doc", admin: true);
        await EnsureConnectionAsync(user.Id);

        var messageId = await SeedMessageWithAttachmentsAsync(
            user.Id, "a2doc", ("att-x", "facture.xlsx", "application/vnd.ms-excel", "cid:quelconque"));

        _factory.Recorder.MessageCatalog["msg-a2doc"] = RecordingGmailApiService.Message("msg-a2doc", new List<GmailApiAttachment>
        {
            new("att-x", "facture.xlsx", "application/vnd.ms-excel", 2048,
                IsInline: false, ContentId: null)
        });

        await RequalifyAsync(user);

        var doc = Assert.Single(await ReadAttachmentsAsync(messageId));
        Assert.False(doc.IsInline);
    }

    // ── Idempotence ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Relancer_la_maintenance_ne_modifie_plus_rien()
    {
        var user = await CreateUserAsync("a2-idem", admin: true);
        await EnsureConnectionAsync(user.Id);

        var messageId = await SeedMessageWithAttachmentsAsync(
            user.Id, "a2idem", ("att-1", "devis.pdf", "application/pdf", "cid:x"));

        _factory.Recorder.MessageCatalog["msg-a2idem"] = RecordingGmailApiService.Message("msg-a2idem", new List<GmailApiAttachment>
        {
            new("att-1", "devis.pdf", "application/pdf", 900, IsInline: false, ContentId: null)
        });

        var premiere = await RequalifyAsync(user);
        Assert.Equal(1, premiere.GetProperty("reclasses").GetInt32());

        var apresPremiere = await ReadAttachmentsAsync(messageId);
        var seconde = await RequalifyAsync(user);

        // Deuxième passe : rien à reclasser.
        Assert.Equal(0, seconde.GetProperty("reclasses").GetInt32());
        Assert.Equal(1, seconde.GetProperty("examines").GetInt32());

        var apresSeconde = await ReadAttachmentsAsync(messageId);
        Assert.Equal(
            apresPremiere.Single().IsInline, apresSeconde.Single().IsInline);
    }

    // ── Périmètre : aucun message modifié ───────────────────────────────────

    [Fact]
    public async Task La_maintenance_ne_modifie_aucun_message()
    {
        var user = await CreateUserAsync("a2-perimetre", admin: true);
        await EnsureConnectionAsync(user.Id);

        var messageId = await SeedMessageWithAttachmentsAsync(
            user.Id, "a2perim", ("att-1", "x.pdf", "application/pdf", "cid:x"));

        var avant = await _factory.WithDbAsync(db => db.GmailMessages.AsNoTracking().FirstAsync(m => m.Id == messageId));

        _factory.Recorder.MessageCatalog["msg-a2perim"] = RecordingGmailApiService.Message("msg-a2perim", new List<GmailApiAttachment>
        {
            new("att-1", "x.pdf", "application/pdf", 10, IsInline: false, ContentId: null)
        });

        await RequalifyAsync(user);

        var apres = await _factory.WithDbAsync(db => db.GmailMessages.AsNoTracking().FirstAsync(m => m.Id == messageId));

        Assert.Equal(avant.Subject, apres.Subject);
        Assert.Equal(avant.BodyText, apres.BodyText);
        Assert.Equal(avant.BodyHtml, apres.BodyHtml);
        Assert.Equal(avant.GmailMessageId, apres.GmailMessageId);
        Assert.Equal(avant.IsRead, apres.IsRead);
        Assert.Equal(avant.IsStarred, apres.IsStarred);
    }

    // ── Accès : administrateur obligatoire ─────────────────────────────────

    [Fact]
    public async Task Un_non_administrateur_est_refuse()
    {
        var user = await CreateUserAsync("a2-nonadmin", admin: false);
        await EnsureConnectionAsync(user.Id);

        // La file est partagée par la classe : on note l'état avant l'appel plutôt que
        // d'exiger une file vide, ce qui dépendrait de l'ordre d'exécution des tests.
        var listingsAvant = _factory.Recorder.MessageListings.Count;

        var response = await user.Client.PostAsync(
            "/api/gmail/maintenance/attachments/requalify", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Le point essentiel : aucun message n'a été relu auprès de Gmail. Le refus doit
        // précéder toute consommation de quota.
        Assert.Equal(listingsAvant, _factory.Recorder.MessageListings.Count);
    }

    // ── Quota : le budget borne le nombre de lectures ──────────────────────

    [Fact]
    public async Task Le_budget_borne_le_nombre_de_messages_relus()
    {
        var user = await CreateUserAsync("a2-budget", admin: true);
        await EnsureConnectionAsync(user.Id);

        for (var i = 0; i < 3; i++)
        {
            await SeedMessageWithAttachmentsAsync(
                user.Id, "budget" + i, ("att-" + i, "doc" + i + ".pdf", "application/pdf", "cid:x"));
        }

        var lecturesAvant = _factory.Recorder.MessageListings.Count;
        var resultat = await RequalifyAsync(user, "?maxMessages=2");

        Assert.Equal(2, resultat.GetProperty("examines").GetInt32());
        Assert.Equal(2, _factory.Recorder.MessageListings.Count - lecturesAvant);
    }

    [Fact]
    public async Task Le_budget_est_plafonne_a_la_limite_de_quota()
    {
        var user = await CreateUserAsync("a2-plafond", admin: true);
        await EnsureConnectionAsync(user.Id);

        await SeedMessageWithAttachmentsAsync(
            user.Id, "plafond", ("att-1", "doc.pdf", "application/pdf", "cid:x"));

        // Un budget absurde est ramené au plafond : au-delà, Gmail répondrait 429.
        var resultat = await RequalifyAsync(user, "?maxMessages=100000");

        Assert.Equal(1, resultat.GetProperty("examines").GetInt32());
    }

    // ── Résilience : un message en erreur n'interrompt pas les autres ──────

    [Fact]
    public async Task Un_message_en_erreur_n_interrompt_pas_la_requalification()
    {
        var user = await CreateUserAsync("a2-erreur", admin: true);
        await EnsureConnectionAsync(user.Id);

        await SeedMessageWithAttachmentsAsync(
            user.Id, "erreur", ("att-1", "a.pdf", "application/pdf", "cid:x"));
        await SeedMessageWithAttachmentsAsync(
            user.Id, "erreur2", ("att-1", "b.pdf", "application/pdf", "cid:x"));

        _factory.Recorder.MessageCatalog["msg-erreur"] =
            RecordingGmailApiService.Message("msg-erreur", new List<GmailApiAttachment>
            {
                new("att-1", "a.pdf", "application/pdf", 10, IsInline: false, ContentId: null)
            });

        // Message supprimé côté Gmail : GmailApiService simule l'échec de lecture.
        _factory.Recorder.FailingMessageIds.Add("msg-erreur2");

        var resultat = await RequalifyAsync(user);

        Assert.Equal(2, resultat.GetProperty("examines").GetInt32());
        Assert.Equal(1, resultat.GetProperty("reclasses").GetInt32());
        Assert.Equal(1, resultat.GetProperty("erreurs").GetInt32());
    }
}
