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
/// A2b : les messages historiques qui n'ont AUCUNE ligne de pièce jointe doivent pouvoir
/// être rattrappés.
/// <para>
/// Le cas d'origine : importés avant que le produit ne stocke les pièces, ces messages
/// n'ont rien en base. Leur trombone n'existe donc pas, et les qualifier a posteriori ne
/// peut rien créer — l'endpoint A2 ne traite que les messages qui ont DÉJÀ des lignes.
/// </para>
/// <para>
/// L'endpoint est piloté à la main par un administrateur, il est idempotent, il respecte
/// un budget de lectures espacées, et il ne touche qu'aux pièces jointes. Ces tests
/// passent par le double enregistreur : aucun appel à l'API Google réelle.
/// </para>
/// </summary>
public class GmailAttachmentBackfillTests : IClassFixture<GmailActionApiFactory>
{
    private readonly GmailActionApiFactory _factory;

    public GmailAttachmentBackfillTests(GmailActionApiFactory factory) => _factory = factory;

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

    /// <summary>Message synchronisé, SANS aucune ligne de pièce jointe : le cas à rattraper.</summary>
    private async Task<int> SeedMessageAsync(string userId, string suffixe)
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
            return message.Id;
        });
    }

    private Task<List<GmailAttachment>> ReadAttachmentsAsync(int messageId) =>
        _factory.WithDbAsync(db => db.GmailAttachments
            .Where(a => a.GmailMessageId == messageId)
            .OrderBy(a => a.GmailAttachmentId)
            .ToListAsync());

    private Task<GmailMessage> ReadMessageAsync(int messageId) =>
        _factory.WithDbAsync(db => db.GmailMessages.AsNoTracking().FirstAsync(m => m.Id == messageId));

    private async Task<JsonElement> BackfillAsync(TestUser user, string query = "")
    {
        var response = await user.Client.PostAsync(
            "/api/gmail/maintenance/attachments/rattrapage" + query, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── Le cas du rapport : un PDF avec Content-ID devient une pièce VISIBLE ──

    [Fact]
    public async Task Message_ancien_sans_piece_cree_les_pieces_manquantes()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-pdf", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bpdf");

        // Ce que Gmail renverrait : un PDF « attachment » QUI PORTE un Content-ID, et le
        // logo référencé par le corps. La qualification A2 les sépare : le PDF est une
        // pièce jointe visible, le logo est une image intégrée.
        _factory.Recorder.MessageCatalog["msg-a2bpdf"] = RecordingGmailApiService.Message(
            "msg-a2bpdf",
            new List<GmailApiAttachment>
            {
                new("piece-pdf", "plan-de-coupe.pdf", "application/pdf", 2048,
                    IsInline: false, ContentId: null),
                new("logo-cid", "logo.png", "image/png", 512,
                    IsInline: true, ContentId: "logo-cid")
            },
            html: "<p>Bonjour</p><img src=\"cid:logo-cid\">");
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2bpdf");

        var rapport = await BackfillAsync(user);

        Assert.Equal(1, rapport.GetProperty("examines").GetInt32());
        Assert.Equal(2, rapport.GetProperty("creees").GetInt32());
        Assert.Equal(0, rapport.GetProperty("restants").GetInt32());

        var pieces = await ReadAttachmentsAsync(messageId);
        Assert.Equal(2, pieces.Count);

        // La pièce qui était invisible : visible, donc listée par l'interface.
        var pdf = pieces.Single(p => p.GmailAttachmentId == "piece-pdf");
        Assert.Equal("plan-de-coupe.pdf", pdf.FileName);
        Assert.Equal("application/pdf", pdf.MimeType);
        Assert.Equal(2048, pdf.SizeBytes);
        Assert.False(pdf.IsInline);
        Assert.Null(pdf.ContentId);

        var logo = pieces.Single(p => p.GmailAttachmentId == "logo-cid");
        Assert.True(logo.IsInline);
        Assert.Equal("logo-cid", logo.ContentId);
    }

    // ── Idempotence : relancer ne crée rien et ne modifie rien ──────────────

    [Fact]
    public async Task Second_passage_ne_cree_rien_et_ne_modifie_rien()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-idem", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bidem");

        _factory.Recorder.MessageCatalog["msg-a2bidem"] = RecordingGmailApiService.Message(
            "msg-a2bidem",
            new List<GmailApiAttachment>
            {
                new("piece-1", "devis.pdf", "application/pdf", 1024,
                    IsInline: false, ContentId: null)
            });
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2bidem");

        var premier = await BackfillAsync(user);
        Assert.Equal(1, premier.GetProperty("creees").GetInt32());

        var avant = await ReadAttachmentsAsync(messageId);
        var messageAvant = await ReadMessageAsync(messageId);

        // Le message a désormais une pièce jointe : il n'est plus candidat, donc le second
        // passage ne le relit même pas. Aucune consommation de quota, aucun doublon.
        var second = await BackfillAsync(user);

        Assert.Equal(0, second.GetProperty("examines").GetInt32());
        Assert.Equal(0, second.GetProperty("creees").GetInt32());

        var apres = await ReadAttachmentsAsync(messageId);
        Assert.Equal(avant.Count, apres.Count);
        Assert.Equal(
            avant.Select(p => new { p.GmailAttachmentId, p.IsInline, p.ContentId }),
            apres.Select(p => new { p.GmailAttachmentId, p.IsInline, p.ContentId }));
    }

    [Fact]
    public async Task Piece_attachee_avec_Content_ID_reste_visible()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-cid", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bcid");

        // Cette fois la pièce n'est PAS pré-qualifiée par le double : on part d'une vraie
        // charge utile Gmail où la partie porte « Content-Disposition: attachment » ET un
        // Content-ID. C'est exactement le cas qui rendait la pièce invisible — le
        // Content-ID suffisait à la classer « inline ». La qualification est donc
        // produite par le vrai service, comme en production.
        var (parse, _) = GmailChargeFactice.Lire(
            GmailChargeFactice.Message(
                "<p>Bonjour, voici la facture.</p>",
                GmailChargeFactice.Part(
                    "2", "application/pdf", "attachment", "facture-interne", "facture-2026.pdf", 4096)));

        _factory.Recorder.MessageCatalog["msg-a2bcid"] = RecordingGmailApiService.Message(
            "msg-a2bcid",
            parse.Attachments.ToList(),
            html: parse.BodyHtml);
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2bcid");

        await BackfillAsync(user);

        // Non inline = visible : c'est le critère que l'interface applique pour lister
        // les pièces d'un message. La ligne est bien celle de la facture, pas une image
        // intégrée, sinon le trombone disparaîtrait de l'en-tête.
        var piece = Assert.Single(await ReadAttachmentsAsync(messageId));
        Assert.False(piece.IsInline);
        Assert.Null(piece.ContentId);
        Assert.Equal("facture-2026.pdf", piece.FileName);
        Assert.Equal("att-2", piece.GmailAttachmentId);
        Assert.Equal(4096, piece.SizeBytes);
    }

    // ── Le rattrapage doit être VISIBLE, pas seulement stocké ────────────────

    [Fact]
    public async Task Le_trombone_apparait_dans_la_liste_des_fils_apres_le_rattrapage()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-trombone", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2btrombone");

        // Le drapeau stocké est FAUX : le message a été importé avant que le produit ne
        // sache lire les pièces. Il le restera après le rattrapage, qui ne réécrit aucun
        // message — l'indicateur est donc déduit des pièces à la lecture.
        _factory.Recorder.MessageCatalog["msg-a2btrombone"] = RecordingGmailApiService.Message(
            "msg-a2btrombone",
            new List<GmailApiAttachment>
            {
                new("piece-1", "plan-de-coupe.pdf", "application/pdf", 2048,
                    IsInline: false, ContentId: null)
            });
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2btrombone");

        var avant = await LireFilAsync(user);
        Assert.False(avant);

        await BackfillAsync(user);

        var message = await ReadMessageAsync(messageId);
        Assert.False(message.HasAttachments); // le message, lui, n'a pas été touché

        // Sans ce complément, le rattrapage resterait invisible dans la liste des fils et
        // l'administrateur conclurait à un échec de la maintenance.
        Assert.True(await LireFilAsync(user));
    }

    private async Task<bool> LireFilAsync(TestUser user)
    {
        var response = await user.Client.GetAsync("/api/gmail/threads");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = page.GetProperty("items").EnumerateArray().ToList();
        var fil = Assert.Single(items);
        return fil.GetProperty("hasAttachments").GetBoolean();
    }

    // ── Quotas, reprise, cloisonnement ───────────────────────────────────────

    [Fact]
    public async Task Le_rattrapage_recense_les_messages_avec_piece_avant_de_les_relire()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-recensement", admin: true);
        await EnsureConnectionAsync(user.Id);
        await SeedMessageAsync(user.Id, "a2brecense");

        _factory.Recorder.DeclareSearchResults("has:attachment");

        // Le double est partagé par tous les tests de la classe : on repart d'une file
        // vide pour n'observer que les recherches de CE lancement.
        _factory.Recorder.Searches.Clear();

        await BackfillAsync(user);

        // « has:attachment » coûte 5 unités pour 100 identifiants, quand une lecture
        // complète en coûte 5 pour UN message. Sans ce recensement, le rattrapage relirait
        // toute la boîte pour découvrir que la plupart des messages n'ont rien.
        Assert.Equal(
            new[] { "has:attachment" },
            _factory.Recorder.Searches.ToArray());
    }

    [Fact]
    public async Task Budget_borne_les_lectures_et_annonce_le_reste_a_traiter()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-budget", admin: true);
        await EnsureConnectionAsync(user.Id);

        var ids = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            var suffixe = "a2bbudget" + i;
            await SeedMessageAsync(user.Id, suffixe);
            _factory.Recorder.MessageCatalog["msg-" + suffixe] = RecordingGmailApiService.Message(
                "msg-" + suffixe,
                new List<GmailApiAttachment>
                {
                    new("piece-" + i, "file-" + i + ".pdf", "application/pdf", 100,
                        IsInline: false, ContentId: null)
                });
            ids.Add("msg-" + suffixe);
        }
        _factory.Recorder.DeclareSearchResults("has:attachment", ids.ToArray());

        var rapport = await BackfillAsync(user, "?maxMessages=2");

        // Deux messages relus, pas trois : le troisième est signalé pour la relance.
        Assert.Equal(2, rapport.GetProperty("examines").GetInt32());
        Assert.Equal(2, rapport.GetProperty("creees").GetInt32());
        Assert.Equal(1, rapport.GetProperty("restants").GetInt32());

        // Le budget se lit aussi dans le nombre de lectures faites chez Gmail.
        Assert.Equal(
            2,
            _factory.Recorder.MessageListings.Count(l => ids.Contains(l)));

        // La relance termine le travail, et le rapport ne ment pas sur le reste à faire.
        var suite = await BackfillAsync(user, "?maxMessages=2");
        Assert.Equal(1, suite.GetProperty("examines").GetInt32());
        Assert.Equal(1, suite.GetProperty("creees").GetInt32());
        Assert.Equal(0, suite.GetProperty("restants").GetInt32());
    }

    [Fact]
    public async Task Message_sans_piece_dans_Gmail_ne_consomme_aucune_lecture()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-vide", admin: true);
        await EnsureConnectionAsync(user.Id);
        await SeedMessageAsync(user.Id, "a2bvide");

        // Le recensement « has:attachment » ne renvoie rien : savoir qu'un message n'a pas
        // de pièce ne doit pas coûter une lecture complète. C'est ce qui rend la
        // maintenance tenable sur une boîte de plusieurs milliers de messages.
        _factory.Recorder.DeclareSearchResults("has:attachment");

        var rapport = await BackfillAsync(user);

        Assert.Equal(0, rapport.GetProperty("examines").GetInt32());
        Assert.Equal(0, rapport.GetProperty("creees").GetInt32());
        Assert.DoesNotContain(_factory.Recorder.MessageListings, id => id == "msg-a2bvide");
    }

    [Fact]
    public async Task Recensement_indisponible_ne_bloque_pas_le_rattrapage()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-panique", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bpanique");

        _factory.Recorder.MessageCatalog["msg-a2bpanique"] = RecordingGmailApiService.Message(
            "msg-a2bpanique",
            new List<GmailApiAttachment>
            {
                new("piece-1", "devis.pdf", "application/pdf", 1024,
                    IsInline: false, ContentId: null)
            });
        _factory.Recorder.FailingSearchQueries.Add("has:attachment");

        // Une maintenance lancée à la main qui répond « 0 message » sans explication
        // ressemblerait à « tout est déjà fait ». On continue donc sans le filtre : le
        // budget borne toujours la facture.
        var rapport = await BackfillAsync(user);

        Assert.Equal(1, rapport.GetProperty("examines").GetInt32());
        Assert.Equal(1, rapport.GetProperty("creees").GetInt32());
        Assert.Single(await ReadAttachmentsAsync(messageId));
    }

    [Fact]
    public async Task Un_message_en_erreur_n_interrompt_pas_le_rattrapage()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-erreur", admin: true);
        await EnsureConnectionAsync(user.Id);
        var ids = new List<int>();

        for (var i = 1; i <= 2; i++)
        {
            var suffixe = "a2berreur" + i;
            ids.Add(await SeedMessageAsync(user.Id, suffixe));
            _factory.Recorder.MessageCatalog["msg-" + suffixe] = RecordingGmailApiService.Message(
                "msg-" + suffixe,
                new List<GmailApiAttachment>
                {
                    new("piece-" + i, "file.pdf", "application/pdf", 100,
                        IsInline: false, ContentId: null)
                });
        }

        // Premier message supprimé côté Gmail entre la liste et la lecture.
        _factory.Recorder.FailingMessageIds.Add("msg-a2berreur1");
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2berreur1", "msg-a2berreur2");

        var rapport = await BackfillAsync(user);

        Assert.Equal(2, rapport.GetProperty("examines").GetInt32());
        Assert.Equal(1, rapport.GetProperty("creees").GetInt32());
        Assert.Equal(1, rapport.GetProperty("erreurs").GetInt32());

        // L'échec est nommé dans le rapport : un administrateur doit pouvoir savoir
        // quel message repasser, pas constater un simple « 1 erreur ». La comparaison se
        // fait sur la ligne ENTIÈRE — « ignoré » plutôt qu'un Contains, qui laisserait
        // passer « Message 1 » pour « Message 12 ».
        var details = rapport.GetProperty("messages").EnumerateArray().Select(m => m.GetString()!).ToList();
        Assert.Contains(details, d => d == $"Message {ids[0]} : ignoré (InvalidOperationException).");
        Assert.DoesNotContain(details, d => d.StartsWith($"Message {ids[1]} : ignoré"));

        // Le message suivant a bien été rattrapé malgré l'erreur du premier.
        Assert.Single(await ReadAttachmentsAsync(ids[1]));
        Assert.Empty(await ReadAttachmentsAsync(ids[0]));
    }

    [Fact]
    public async Task Aucune_modification_du_message_lui_meme()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-intact", admin: true);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bintact");

        _factory.Recorder.MessageCatalog["msg-a2bintact"] = RecordingGmailApiService.Message(
            "msg-a2bintact",
            new List<GmailApiAttachment>
            {
                new("piece-1", "photo.jpg", "image/jpeg", 900,
                    IsInline: false, ContentId: null)
            });
        _factory.Recorder.DeclareSearchResults("has:attachment", "msg-a2bintact");

        var avant = await ReadMessageAsync(messageId);
        await BackfillAsync(user);
        var apres = await ReadMessageAsync(messageId);

        // Le rattrapage porte sur les pièces jointes. Un message n'est ni réécrit, ni
        // déplacé, ni marqué : c'est une condition du cahier des charges, parce qu'une
        // maintenance doit rester réversible par construction.
        Assert.Equal(avant.Subject, apres.Subject);
        Assert.Equal(avant.BodyText, apres.BodyText);
        Assert.Equal(avant.BodyHtml, apres.BodyHtml);
        Assert.Equal(avant.HasAttachments, apres.HasAttachments);
        Assert.Equal(avant.IsRead, apres.IsRead);
        Assert.Equal(avant.IsSynchronized, apres.IsSynchronized);
        Assert.Equal(avant.LabelsJson, apres.LabelsJson);
    }

    [Fact]
    public async Task Utilisateur_non_administrateur_refuse()
    {
        _factory.ResetSimulatedFailures();

        var user = await CreateUserAsync("a2b-simple", admin: false);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "a2bsimple");

        var response = await user.Client.PostAsync(
            "/api/gmail/maintenance/attachments/rattrapage", null);

        // Le rôle applicatif ne suffit pas : ces endpoints lancent une boîte Gmail et
        // consomment le quota du compte qui la porte.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await ReadAttachmentsAsync(messageId));
        Assert.DoesNotContain(_factory.Recorder.MessageListings, id => id == "msg-a2bsimple");
    }
}