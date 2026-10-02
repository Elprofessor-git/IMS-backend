using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Régression A1 : l'envoi doit transporter le texte AFFICHÉ, pas celui relu en base.
/// <para>
/// Le défaut d'origine : « Envoyer » n'envoyait que l'identifiant du brouillon. Le corps
/// était relu depuis <c>EmailAiReply.Body</c>, donc la dernière version tapped au clavier
/// disparaissait sans erreur — l'API répondait 200 et l'utilisateur croyait avoir envoyé
/// sa relecture.
/// </para>
/// <para>
/// Les DEUX chemins sont couverts : brouillon créé à l'envoi, et brouillon préexistant
/// déjà figé dans Gmail (qui portait le texte d'une version antérieure). Aucun appel à
/// l'API Google réelle : <see cref="RecordingGmailApiService"/> enregistre les corps.
/// </para>
/// </summary>
public class GmailReplySendTests : IClassFixture<GmailActionApiFactory>
{
    private readonly GmailActionApiFactory _factory;

    public GmailReplySendTests(GmailActionApiFactory factory) => _factory = factory;

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

    /// <summary>Message reçu + réponse IA en base, dans l'état « jamais envoyé ».</summary>
    private async Task<(int ReplyId, string Suffixe)> SeedReplyAsync(string userId, string suffixe)
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
                Subject = "Commande " + suffixe,
                Rfc822MessageId = "<rfc822-" + suffixe + "@exemple.fr>",
                BodyText = "Contenu",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-1),
                IsRead = true,
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };
            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();

            var reply = new EmailAiReply
            {
                GmailMessageId = message.Id,
                Subject = "Re: Commande " + suffixe,
                Body = "Texte genere par l'IA, jamais relu.",
                Statut = StatutReponseIa.Generated,
                GeneratedAt = DateTime.UtcNow
            };
            db.EmailAiReponses.Add(reply);
            await db.SaveChangesAsync();

            return (reply.Id, suffixe);
        });
    }

    private async Task<EmailAiReply> ReadReplyAsync(int replyId) =>
        await _factory.WithDbAsync(db => db.EmailAiReponses.AsNoTracking().FirstAsync(r => r.Id == replyId));

    /// <summary>
    /// Les appels sont dans des files partagées par tous les tests de la classe : on filtre
    /// sur le corps propre au test courant, sinon onasserterait les appels des voisins
    /// (xUnit exécute les méthodes d'une classe en parallèle).
    /// </summary>
    private List<RecordingGmailApiService.DraftCall> CreatesWithBody(string body) =>
        _factory.Recorder.DraftCreates.Where(c => c.Body == body).ToList();

    private List<RecordingGmailApiService.DraftCall> UpdatesWithBody(string body) =>
        _factory.Recorder.DraftUpdates.Where(c => c.Body == body).ToList();

    // ── Chemin 1 : pas de brouillon Gmail → création puis envoi ─────────────

    [Fact]
    public async Task Envoyer_avec_texte_modifie_cree_le_brouillon_avec_ce_texte()
    {
        var user = await CreateUserAsync("a1-envoi-1");
        await EnsureConnectionAsync(user.Id, "a1envoi1");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi1");

        const string relecture = "Bonjour, le tissu arrive vendredi. Cordialement.";

        // Identifiant propre à ce test : les files du double sont partagées par la classe.
        _factory.Recorder.NextDraftId = "draft-a1-envoi-1";

        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = relecture, subject = "Re: Commande a1envoi1 — relu" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Le texte relu a bien atteint Gmail, et pas celui de l'IA.
        var created = CreatesWithBody(relecture);
        Assert.Single(created);
        Assert.Equal("client@exemple.fr", created[0].To);
        Assert.Equal("Re: Commande a1envoi1 — relu", created[0].Subject);
        Assert.DoesNotContain(_factory.Recorder.DraftCreates, c => c.Body.Contains("jamais relu"));

        // Le brouillon créé par CE test est bien celui qui a été envoyé, et il ne l'a été
        // qu'une fois : pas de doublon, pas d'oubli.
        Assert.Equal(1, _factory.Recorder.DraftSends.Count(d => d == "draft-a1-envoi-1"));
    }

    // ── Chemin 2 : brouillon Gmail préexistant → RÉÉCRIT avant envoi ────────

    [Fact]
    public async Task Envoyer_avec_brouillon_preexistant_reecrit_le_brouillon_avant_envoi()
    {
        var user = await CreateUserAsync("a1-envoi-2");
        await EnsureConnectionAsync(user.Id, "a1envoi2");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi2");

        // Le brouillon existe déjà dans Gmail, figé sur une version antérieure du texte.
        await _factory.WithDbAsync(async db =>
        {
            var reply = await db.EmailAiReponses.FirstAsync(r => r.Id == replyId);
            reply.GmailDraftId = "draft-ancien-" + replyId;
            reply.Statut = StatutReponseIa.Approved;
            await db.SaveChangesAsync();
        });

        const string relecture = "Version relue, différente de celle figée dans Gmail.";

        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = relecture, subject = "Objet final" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Le brouillon préexistant a été RÉÉCRIT, pas envoyé tel quel.
        var updated = UpdatesWithBody(relecture);
        Assert.Single(updated);
        Assert.Equal("draft-ancien-" + replyId, updated[0].DraftId);

        // Aucune création de brouillon : on a mis à jour, on n'a pas dupliqué.
        Assert.Empty(CreatesWithBody(relecture));

        // Et c'est ce brouillon mis à jour qui a été envoyé.
        Assert.Contains("draft-ancien-" + replyId, _factory.Recorder.DraftSends);
    }

    // ── Persistance : Body/Subject en base AVANT l'action Gmail ──────────────

    [Fact]
    public async Task Envoyer_persiste_le_texte_en_base()
    {
        var user = await CreateUserAsync("a1-envoi-3");
        await EnsureConnectionAsync(user.Id, "a1envoi3");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi3");

        const string relecture = "Texte persisté en base.";

        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = relecture, subject = "Objet persisté" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await ReadReplyAsync(replyId);
        Assert.Equal(relecture, stored.Body);
        Assert.Equal("Objet persisté", stored.Subject);
        Assert.Equal(StatutReponseIa.Sent, stored.Statut);
        Assert.NotNull(stored.SentAt);
    }

    [Fact]
    public async Task Envoyer_persiste_le_texte_meme_si_Gmail_echoue()
    {
        var user = await CreateUserAsync("a1-envoi-4");
        await EnsureConnectionAsync(user.Id, "a1envoi4");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi4");

        await _factory.WithDbAsync(async db =>
        {
            var reply = await db.EmailAiReponses.FirstAsync(r => r.Id == replyId);
            reply.GmailDraftId = "draft-ko-" + replyId;
            await db.SaveChangesAsync();
        });

        _factory.Recorder.FailingDraftId = "draft-ko-" + replyId;

        const string relecture = "Texte modifié, Gmail indisponible.";
        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = relecture, subject = "Objet malgré l'échec" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        // La modification de l'utilisateur est conservée : au second essai, il ne repart
        // pas d'un texte périmé.
        var stored = await ReadReplyAsync(replyId);
        Assert.Equal(relecture, stored.Body);
        Assert.NotEqual(StatutReponseIa.Sent, stored.Statut);
        Assert.Null(stored.SentAt);
    }

    // ── Refus : corps vide ──────────────────────────────────────────────────

    [Theory]
    [InlineData("", 1)]
    [InlineData("   ", 2)]
    public async Task Envoyer_refuse_un_corps_vide(string corps, int variante)
    {
        // Le suffixe varie par cas : la base de test est partagée par la classe et
        // l'unicité (GmailConnectionId, GmailMessageId) refuserait deux seeds identiques.
        var suffixe = "a1envoi5-" + variante;
        var user = await CreateUserAsync("a1-envoi-5-" + variante);
        await EnsureConnectionAsync(user.Id, suffixe);
        var (replyId, _) = await SeedReplyAsync(user.Id, suffixe);

        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = corps, subject = "Objet" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Aucun appel Gmail ne doit avoir eu lieu.
        Assert.DoesNotContain(_factory.Recorder.DraftCreates, c => c.Body == corps);
        Assert.DoesNotContain(_factory.Recorder.DraftSends, d => d.Contains(replyId.ToString()));

        var stored = await ReadReplyAsync(replyId);
        Assert.Equal(StatutReponseIa.Generated, stored.Statut);
    }

    [Fact]
    public async Task Envoyer_refuse_un_corps_absent()
    {
        var user = await CreateUserAsync("a1-envoi-6");
        await EnsureConnectionAsync(user.Id, "a1envoi6");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi6");

        // Corps omis : le binding [FromBody] est requis.
        var response = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { subject = "Objet sans corps" });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,
            $"Attendu 400/415, reçu {response.StatusCode}");

        var stored = await ReadReplyAsync(replyId);
        Assert.Equal(StatutReponseIa.Generated, stored.Statut);
    }

    // ── Idempotence : ré-envoyer une réponse déjà envoyée est refusé ────────

    [Fact]
    public async Task Envoyer_deux_fois_est_refuse()
    {
        var user = await CreateUserAsync("a1-envoi-7");
        await EnsureConnectionAsync(user.Id, "a1envoi7");
        var (replyId, _) = await SeedReplyAsync(user.Id, "a1envoi7");

        var first = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send", new { body = "Premier envoi." });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send", new { body = "Second envoi, doit échouer." });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
