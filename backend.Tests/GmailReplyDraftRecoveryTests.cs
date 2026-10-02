using System.Net;
using System.Net.Http.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Régression A1/A4 : un brouillon Gmail créé par « Envoyer » ne doit pas être dupliqué
/// si l'envoi échoue juste après sa création.
/// <para>
/// Le scénario : <c>drafts.create</c> réussit (Gmail a bien le brouillon, avec le texte
/// relu), puis <c>drafts.send</c> échoue. L'API répond 502. Si l'identifiant du brouillon
/// n'était pas persisté, le second essai repartirait de zéro et créerait un DEUXIÈME
/// brouillon dans Gmail : l'utilisateur se retrouverait avec deux réponses identiques
/// dans ses brouillons, dont une qui ne partira jamais.
/// </para>
/// <para>
/// Cette classe a sa propre factory, donc son propre faux Gmail : elle doit piloter
/// <c>NextDraftId</c> et <c>FailingDraftId</c>, qui sont mutables, sans empiéter sur les
/// tests voisins. Aucun appel à l'API Google réelle.
/// </para>
/// </summary>
public class GmailReplyDraftRecoveryTests : IClassFixture<GmailActionApiFactory>
{
    private readonly GmailActionApiFactory _factory;

    public GmailReplyDraftRecoveryTests(GmailActionApiFactory factory) => _factory = factory;

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
    private async Task<int> SeedReplyAsync(string userId, string suffixe)
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

            return reply.Id;
        });
    }

    private async Task<EmailAiReply> ReadReplyAsync(int replyId) =>
        await _factory.WithDbAsync(db => db.EmailAiReponses.AsNoTracking().FirstAsync(r => r.Id == replyId));

    [Fact]
    public async Task Reprise_apres_un_envoi_en_echec_reutilise_le_brouillon_cree()
    {
        var user = await CreateUserAsync("a1-reprise");
        await EnsureConnectionAsync(user.Id, "a1reprise");
        var replyId = await SeedReplyAsync(user.Id, "a1reprise");

        // Le même identifiant pour la création et l'envoi : c'est exactement le cas
        // « create OK / send KO » que l'on veut éprouver.
        const string draftId = "draft-reprise-a1";
        _factory.Recorder.NextDraftId = draftId;
        _factory.Recorder.FailingDraftId = draftId;

        const string premierEssai = "Première tentative, Gmail refuse l'envoi.";
        var echec = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = premierEssai, subject = "Objet repris" });

        Assert.Equal(HttpStatusCode.BadGateway, echec.StatusCode);

        // Le brouillon existe dans Gmail : son identifiant doit survivre au 502, sans quoi
        // le second essai en créerait un second.
        var apresEchec = await ReadReplyAsync(replyId);
        Assert.Equal(draftId, apresEchec.GmailDraftId);
        Assert.Equal(premierEssai, apresEchec.Body);
        Assert.NotEqual(StatutReponseIa.Sent, apresEchec.Statut);
        Assert.Null(apresEchec.SentAt);
        Assert.Single(_factory.Recorder.DraftCreates, c => c.Body == premierEssai);
        Assert.DoesNotContain(draftId, _factory.Recorder.DraftSends);

        // Gmail redevient disponible.
        _factory.Recorder.FailingDraftId = null;

        const string secondEssai = "Deuxième tentative, cette fois Gmail répond.";
        var reussite = await user.Client.PostAsJsonAsync(
            $"/api/gmail/replies/{replyId}/send",
            new { body = secondEssai, subject = "Objet repris" });

        Assert.Equal(HttpStatusCode.OK, reussite.StatusCode);

        // Aucun nouveau brouillon : le second essai RÉÉCRIT celui qui existait déjà.
        Assert.DoesNotContain(_factory.Recorder.DraftCreates, c => c.Body == secondEssai);
        var reecriture = Assert.Single(_factory.Recorder.DraftUpdates, c => c.Body == secondEssai);
        Assert.Equal(draftId, reecriture.DraftId);
        Assert.Contains(draftId, _factory.Recorder.DraftSends);

        var apresReussite = await ReadReplyAsync(replyId);
        Assert.Equal(StatutReponseIa.Sent, apresReussite.Statut);
        Assert.Equal("sent-" + draftId, apresReussite.GmailSentMessageId);
        Assert.Equal(draftId, apresReussite.GmailDraftId);
    }
}
