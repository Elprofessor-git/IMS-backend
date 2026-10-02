using System.Net;
using System.Text;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Qualification des pièces jointes AU NIVEAU DU SERVICE (A2).
/// <para>
/// <see cref="GmailAttachmentQualificationTests"/> prouve le comportement de bout en
/// bout via le double enregistreur, mais le double court-circuite le service : le
/// parcours MIME — lecture de <c>Content-Disposition</c>, remontée de l'arbre des
/// parties, comparaison des <c>cid:</c> — n'y est pas exercé. C'est pourtant
/// exactement là que se situait le défaut.
/// </para>
/// <para>
/// Ces tests instancient le vrai <see cref="GmailApiService"/> avec un gestionnaire HTTP
/// qui renvoie un payload Gmail fabriqué : la charge utile est réaliste (parties
/// imbriquées, en-têtes, <c>body.attachmentId</c>), mais AUCUN appel ne sort.
/// </para>
/// </summary>
public class GmailApiAttachmentParsingTests
{
    /// <summary>Gestionnaire qui répond un payload JSON Gmail fixé, et compte les appels.</summary>
    private sealed class PayloadHandler : HttpMessageHandler
    {
        private readonly string _json;
        public List<string> Urls { get; } = new();

        public PayloadHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class OAuthFactice : IGmailOAuthService
    {
        public bool IsConfigured => true;
        public string BuildAuthorizationUrl(string userId) => "https://exemple.test/auth";
        public Task<GmailConnection> HandleCallbackAsync(string code, string state) => throw new NotSupportedException();
        public string BuildPostCallbackRedirectUrl(bool success, string? errorMessage = null) => "https://ims.test";
        public Task RevokeAsync(GmailConnection connection) => Task.CompletedTask;
        public Task<string> GetFreshAccessTokenAsync(GmailConnection connection) => Task.FromResult("jeton-de-test");
    }

    private sealed class ClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public ClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static GmailConnection Connexion() => new()
    {
        UserId = "u1",
        GmailAddress = "moi@ims.test",
        GoogleUserId = "google-1",
        RefreshTokenEncrypted = "chiffre",
        GrantedScopes = "gmail.readonly"
    };

    private static (GmailApiMessage Message, List<string> Urls) Lire(string json)
    {
        var handler = new PayloadHandler(json);
        var service = new GmailApiService(
            new OAuthFactice(), new ClientFactory(handler), NullLogger<GmailApiService>.Instance);
        return (service.GetMessageAsync(Connexion(), "msg-1").GetAwaiter().GetResult(), handler.Urls);
    }

    // ── Outils de fabrication d'un payload Gmail ─────────────────────────────

    private static string EnTete(string nom, string valeur) => $$"""{"name": "{{nom}}", "value": "{{valeur}}"}""";

    private static string Part(string partId, string mime, string disposition, string? cid, string filename, int size = 1024)
    {
        // En-tête Content-Disposition réel : « attachment; filename="x.pdf" » ou « inline ».
        // C'est CET en-tête que le service lit — la valeur de `disposition` ci-dessus.
        // Les guillemets du nom de fichier sont ÉCHAPPÉS pour le JSON : la valeur est
        // interpolée dans une chaîne JSON, et un guillemet nu la rendrait illisible.
        var dispositionValue = disposition == "attachment"
            ? $"attachment; filename=\\\"{filename}\\\""
            : "inline";
        var cidHeader = cid == null ? "" : EnTete("Content-ID", $"<{cid}>") + ", ";

        return $$"""
            {
              "partId": "{{partId}}",
              "mimeType": "{{mime}}",
              "filename": "{{filename}}",
              "headers": [{{cidHeader}}{{EnTete("Content-Disposition", dispositionValue)}}, {{EnTete("Content-Type", mime)}}],
              "body": { "size": {{size}}, "attachmentId": "att-{{partId}}" }
            }
            """;
    }

    private static string Message(string html, params string[] parts)
    {
        var partList = string.Join(",", parts);
        // Le corps texte est renvoyé par Gmail en base64 dans body.data : sans lui, le
        // service ne trouve aucun HTML et n'aurait aucun cid: à examiner.
        var htmlData = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

        return $$"""
            {
              "id": "msg-1",
              "threadId": "thread-1",
              "labelIds": ["INBOX"],
              "snippet": "extrait",
              "internalDate": "1767225600000",
              "payload": {
                "partId": "",
                "mimeType": "multipart/mixed",
                "headers": [{{EnTete("Message-Id", "<abc@exemple.fr>")}}, {{EnTete("Subject", "Bonjour")}}, {{EnTete("From", "client@exemple.fr")}}],
                "body": { "size": 0 },
                "parts": [
                  {
                    "partId": "0",
                    "mimeType": "text/plain",
                    "headers": [{{EnTete("Content-Type", "text/plain; charset=UTF-8")}}],
                    "body": { "size": 10, "data": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes("Bonjour"))}}" }
                  },
                  {
                    "partId": "1",
                    "mimeType": "text/html",
                    "headers": [{{EnTete("Content-Type", "text/html; charset=UTF-8")}}],
                    "body": { "size": {{html.Length}}, "data": "{{htmlData}}" }
                  }{{(partList.Length > 0 ? "," + partList : "")}}
                ]
              }
            }
            """;
    }

    // ── 1. Le cas du rapport : PDF « attachment » PORTE un Content-ID ────────

    [Fact]
    public void Piece_Content_Disposition_attachment_avec_Content_ID_nest_pas_inline()
    {
        var json = Message(
            "<p>Bonjour, voici le document.</p>",
            Part("2", "application/pdf", "attachment", "doc-interne", "plan-de-coupe.pdf"));

        var (message, _) = Lire(json);

        var pdf = Assert.Single(message.Attachments);
        Assert.False(pdf.IsInline);
        Assert.Null(pdf.ContentId);
        Assert.True(message.HasAttachments);
    }

    // ── 2. Une image réellement référencée par cid: reste inline ────────────

    [Fact]
    public void Image_inline_referencee_par_un_cid_dans_le_html_reste_inline()
    {
        var json = Message(
            """<p>Bonjour <img src="cid:logo-signature"></p>""",
            Part("2", "image/png", "inline", "logo-signature", ""));

        var (message, _) = Lire(json);

        var logo = Assert.Single(message.Attachments);
        Assert.True(logo.IsInline);
        Assert.Equal("logo-signature", logo.ContentId);
        Assert.False(message.HasAttachments);
    }

    // ── 3. Une image avec Content-ID mais JAMAIS référencée : pièce jointe ──

    [Fact]
    public void Image_avec_Content_ID_non_referencee_est_classee_piece_jointe()
    {
        var json = Message(
            "<p>Aucun lien vers l'image.</p>",
            Part("2", "image/png", "inline", "logo-orphelin", ""));

        var (message, _) = Lire(json);

        var image = Assert.Single(message.Attachments);
        Assert.False(image.IsInline);
        Assert.Null(image.ContentId);
        Assert.True(message.HasAttachments);
    }

    // ── 4. Content-Disposition « attachment » l'emporte même si le cid: est
    //       RÉELLEMENT référencé par le HTML (pièce jointe illustrée) ────────

    [Fact]
    public void Disposition_attachment_lemporte_meme_si_le_cid_est_reference()
    {
        var json = Message(
            """<p><img src="cid:illustration"></p>""",
            Part("2", "image/jpeg", "attachment", "illustration", "schema.jpg"));

        var (message, _) = Lire(json);

        var illustration = Assert.Single(message.Attachments);
        Assert.False(illustration.IsInline);
        Assert.Null(illustration.ContentId);
    }

    // ── 5. Un cid: orphelin est retiré du corps (pas d'image cassée) ─────────

    [Fact]
    public void Reference_cid_orpheline_retiree_du_corps()
    {
        var json = Message(
            """<p>Avant<img src="cid:absent">Après</p>""",
            Part("2", "image/png", "inline", "autre", ""));

        var (message, _) = Lire(json);

        Assert.DoesNotContain("cid:absent", message.BodyHtml!);
        // La partie réellement présente n'est PAS inline non plus : son cid: n'est pas
        // référencé, donc elle devient une pièce jointe visible.
        Assert.All(message.Attachments, a => Assert.False(a.IsInline));
    }

    // ── 6. Un cid: résolu est conservé tel quel dans le corps ───────────────

    [Fact]
    public void Reference_cid_resolue_conservee_dans_le_corps()
    {
        var json = Message(
            """<p><img src="cid:logo"></p>""",
            Part("2", "image/png", "inline", "logo", ""));

        var (message, _) = Lire(json);

        Assert.Contains("cid:logo", message.BodyHtml!);
    }

    // ── 7. Partie non-image référencée : jamais inline ──────────────────────

    [Fact]
    public void Partie_non_image_referencee_par_cid_nest_pas_inline()
    {
        var json = Message(
            """<p><a href="cid:rapport">Télécharger</a></p>""",
            Part("2", "application/pdf", "inline", "rapport", "rapport.pdf"));

        var (message, _) = Lire(json);

        var doc = Assert.Single(message.Attachments);
        Assert.False(doc.IsInline);
    }

    // ── 8. Parties imbriquées (multipart dans multipart) ────────────────────

    [Fact]
    public void Les_parties_imbriquees_sont_parcourues()
    {
        var imbrique = $$"""
            {
              "partId": "3",
              "mimeType": "multipart/related",
              "headers": [{{EnTete("Content-Type", "multipart/related; boundary=in")}}, {{EnTete("Content-Disposition", "inline")}}],
              "body": { "size": 0 },
              "parts": [ {{Part("3.1", "image/png", "inline", "logo-imprime", "")}} ]
            }
            """;

        var json = Message("""<p><img src="cid:logo-imprime"></p>""", imbrique);

        var (message, _) = Lire(json);

        var logo = Assert.Single(message.Attachments);
        Assert.True(logo.IsInline);
        Assert.Equal("logo-imprime", logo.ContentId);
    }

    // ── 9. Un message sans pièce : aucune icône trombone fantôme ────────────

    [Fact]
    public void Message_sans_piece_signale_aucune_piece_jointe()
    {
        var (message, urls) = Lire(Message("<p>Bonjour.</p>"));

        Assert.Empty(message.Attachments);
        Assert.False(message.HasAttachments);
        // Une seule lecture : la maintenance ne doit pas coûter un second aller-retour
        // quand la synchronisation normale suffit.
        Assert.Single(urls);
    }
}
