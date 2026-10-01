using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Intégration des évolutions du module Courriels : fils, HTML assaini, pièces jointes,
/// recherche, envoi et édition IA.
///
/// Deux principes fondamentaux sont respectés :
/// <list type="bullet">
/// <item>la base est jetable et migrée par les migrations réelles (TestDatabase) ;</item>
/// <item>l'isolation par propriétaire est vérifiée AVEC deux utilisateurs distincts ;
/// un test qui n'en vérifierait qu'un ne prouverait rien.</item>
/// </list>
/// </summary>
public class GmailEvolutionIntegrationTests : IClassFixture<TacheApiFactory>
{
    private readonly TacheApiFactory _factory;

    public GmailEvolutionIntegrationTests(TacheApiFactory factory) => _factory = factory;

    private Task<TestUser> CreateUserAsync(string id) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
        });

    private async Task<(int MessageId, int ConnectionId)> SeedMessageAsync(
        string userId, string suffixe, string? threadId = null, string? subject = "Commande 4821",
        string? bodyText = "Contenu", string? bodyHtml = null, bool isRead = false,
        DateTime? receivedAt = null, int? attachmentCount = null, bool isStarred = false,
        bool hasAttachments = false)
    {
        return await _factory.WithDbAsync(async db =>
        {
            var connection = await db.GmailConnections
                .FirstOrDefaultAsync(c => c.UserId == userId)
                ?? throw new InvalidOperationException($"Connexion Gmail absente pour {userId}.");

            var message = new GmailMessage
            {
                GmailConnectionId = connection.Id,
                GmailMessageId = "msg-" + suffixe,
                GmailThreadId = threadId ?? ("thread-" + suffixe),
                From = "client@exemple.fr",
                To = userId + "@ims.test",
                Subject = subject,
                Rfc822MessageId = "rfc822-" + suffixe,
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                Snippet = (bodyText ?? "").Length > 80 ? bodyText![..80] : bodyText,
                ReceivedAt = receivedAt ?? DateTime.UtcNow,
                IsRead = isRead,
                IsStarred = isStarred,
                HasAttachments = hasAttachments || (attachmentCount ?? 0) > 0,
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };

            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();

            for (var i = 0; i < (attachmentCount ?? 0); i++)
            {
                db.GmailAttachments.Add(new GmailAttachment
                {
                    GmailMessageId = message.Id,
                    GmailAttachmentId = $"att-{suffixe}-{i}",
                    FileName = $"plan-{i}.pdf",
                    MimeType = "application/pdf",
                    SizeBytes = 1024 * (i + 1),
                    IsInline = i == 0,
                    ContentId = i == 0 ? "logo-ims" : null
                });
            }

            await db.SaveChangesAsync();
            return (message.Id, connection.Id);
        });
    }

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

    // ── Phase 1 : fils ───────────────────────────────────────────────────

    [Fact]
    public async Task G20_La_liste_des_fils_ne_renvoie_qu_une_ligne_par_discussion()
    {
        var user = await CreateUserAsync("gmail-fils-a");
        await EnsureConnectionAsync(user.Id);

        // 3 messages dans le même fil + 2 fils distincts => 3 lignes, pas 5.
        await SeedMessageAsync(user.Id, "f1a", threadId: "fil-1", subject: "Retard livraison", receivedAt: DateTime.UtcNow.AddHours(-3));
        await SeedMessageAsync(user.Id, "f1b", threadId: "fil-1", subject: "Retard livraison", receivedAt: DateTime.UtcNow.AddHours(-2));
        await SeedMessageAsync(user.Id, "f1c", threadId: "fil-1", subject: "Re: Retard livraison", receivedAt: DateTime.UtcNow.AddHours(-1));
        await SeedMessageAsync(user.Id, "f2", threadId: "fil-2", subject: "Facture", receivedAt: DateTime.UtcNow.AddHours(-5));
        await SeedMessageAsync(user.Id, "f3", threadId: "fil-3", subject: "Confirmation", receivedAt: DateTime.UtcNow.AddHours(-6));

        var response = await user.Client.GetAsync("/api/gmail/threads?pageSize=50");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(3, page.GetProperty("total").GetInt32());

        // Chaque ligne pointe sur le message LE PLUS RÉCENT de son fil.
        var fil1 = items.Single(i => i.GetProperty("gmailThreadId").GetString() == "fil-1");
        Assert.Equal(3, fil1.GetProperty("messageCount").GetInt32());
        Assert.Equal(3, fil1.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task G21_Le_detail_d_un_fil_est_ordonne_du_plus_ancien_au_plus_recent()
    {
        var user = await CreateUserAsync("gmail-fils-b");
        await EnsureConnectionAsync(user.Id);

        await SeedMessageAsync(user.Id, "o1", threadId: "ordre", receivedAt: DateTime.UtcNow.AddHours(-5));
        await SeedMessageAsync(user.Id, "o2", threadId: "ordre", receivedAt: DateTime.UtcNow.AddHours(-3));
        await SeedMessageAsync(user.Id, "o3", threadId: "ordre", receivedAt: DateTime.UtcNow.AddHours(-1));

        var response = await user.Client.GetAsync("/api/gmail/threads/ordre");
        response.EnsureSuccessStatusCode();

        var thread = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messages = thread.GetProperty("messages").EnumerateArray().ToList();

        Assert.Equal(3, messages.Count);
        var dates = messages.Select(m => m.GetProperty("receivedAt").GetDateTime()).ToList();
        Assert.Equal(dates.OrderBy(d => d), dates);
    }

    [Fact]
    public async Task G22_Un_fil_appartenant_a_autrui_est_indisponible()
    {
        var alice = await CreateUserAsync("gmail-iso-a");
        var bob = await CreateUserAsync("gmail-iso-b");
        await EnsureConnectionAsync(alice.Id);
        await EnsureConnectionAsync(bob.Id);

        await SeedMessageAsync(alice.Id, "prive", threadId: "fil-prive");

        // 404 et non 403 : un code 403 confirmerait l'existence du fil à Bob.
        var response = await bob.Client.GetAsync("/api/gmail/threads/fil-prive");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task G23_Un_identifiant_de_fil_deguise_ne_renvoie_pas_le_contenu()
    {
        var alice = await CreateUserAsync("gmail-iso-c");
        var bob = await CreateUserAsync("gmail-iso-d");
        await EnsureConnectionAsync(alice.Id);
        await EnsureConnectionAsync(bob.Id);

        await SeedMessageAsync(alice.Id, "inj", threadId: "secret-alice", subject: "Salaires 2026");

        // Tentatives de traversée : quote, espace, casse.
        foreach (var candidate in new[] { "secret-alice' OR '1'='1", "SECRET-ALICE", "secret-alice " })
        {
            var response = await bob.Client.GetAsync(
                "/api/gmail/threads/" + Uri.EscapeDataString(candidate));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // ── Phase 2 : HTML ───────────────────────────────────────────────────

    [Fact]
    public async Task G24_Le_html_renvoye_est_assaini_et_sans_secret_de_boite()
    {
        var user = await CreateUserAsync("gmail-html-a");
        await EnsureConnectionAsync(user.Id);

        var hostile = "<p>Bonjour</p><script>alert(1)</script><img src=x onerror=alert(2)><a href=\"javascript:alert(3)\">clic</a>";
        var (messageId, _) = await SeedMessageAsync(user.Id, "html", bodyHtml: hostile);

        var response = await user.Client.GetAsync($"/api/gmail/messages/{messageId}");
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        var html = detail.GetProperty("bodyHtml").GetString() ?? "";

        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bonjour", html);
    }

    [Fact]
    public async Task G25_Les_images_integrees_sont_pointees_vers_le_proxy_ims()
    {
        var user = await CreateUserAsync("gmail-html-b");
        await EnsureConnectionAsync(user.Id);

        var (messageId, _) = await SeedMessageAsync(
            user.Id, "cid",
            bodyHtml: "<img src=\"cid:logo-ims@exemple.fr\" alt=\"Logo\" width=\"120\">");

        var response = await user.Client.GetAsync($"/api/gmail/messages/{messageId}");
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        var html = detail.GetProperty("bodyHtml").GetString() ?? "";

        // Le « cid: » doit avoir disparu au profit du proxy : sinon l'image est cassée.
        Assert.DoesNotContain("cid:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/api/gmail/messages/{messageId}/inline/logo-ims%40exemple.fr", html);
    }

    [Fact]
    public async Task G26_Le_html_hostile_d_un_autrui_utilisateur_est_refuse()
    {
        var alice = await CreateUserAsync("gmail-html-c");
        var bob = await CreateUserAsync("gmail-html-d");
        await EnsureConnectionAsync(alice.Id);
        await EnsureConnectionAsync(bob.Id);

        var (aliceMessage, _) = await SeedMessageAsync(alice.Id, "conf", bodyHtml: "<p>Confidentiel</p>");

        var response = await bob.Client.GetAsync($"/api/gmail/messages/{aliceMessage}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Phase 3 : pièces jointes ─────────────────────────────────────────

    [Fact]
    public async Task G27_Le_detail_expose_les_metadonnees_des_pieces_jointes()
    {
        var user = await CreateUserAsync("gmail-pj-a");
        await EnsureConnectionAsync(user.Id);

        var (messageId, _) = await SeedMessageAsync(user.Id, "pj", attachmentCount: 2);

        var response = await user.Client.GetAsync($"/api/gmail/messages/{messageId}");
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        var attachments = detail.GetProperty("attachments").EnumerateArray().ToList();

        Assert.Equal(2, attachments.Count);
        Assert.True(detail.GetProperty("hasAttachments").GetBoolean());

        // Le binaire ne transite jamais dans le JSON : seules les métadonnées et l'URL.
        foreach (var attachment in attachments)
        {
            Assert.False(attachment.TryGetProperty("contentBase64", out _));
            Assert.StartsWith($"/api/gmail/messages/{messageId}/attachments/", attachment.GetProperty("url").GetString());
        }
    }

    [Fact]
    public async Task G28_Une_piece_jointe_d_autrui_ne_se_telecharge_pas()
    {
        var alice = await CreateUserAsync("gmail-pj-b");
        var bob = await CreateUserAsync("gmail-pj-c");
        await EnsureConnectionAsync(alice.Id);
        await EnsureConnectionAsync(bob.Id);

        var (aliceMessage, _) = await SeedMessageAsync(alice.Id, "pjiso", attachmentCount: 1);

        var response = await bob.Client.GetAsync($"/api/gmail/messages/{aliceMessage}/attachments/att-pjiso-0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task G29_Une_piece_jointe_inconnue_du_message_est_refusee()
    {
        var user = await CreateUserAsync("gmail-pj-d");
        await EnsureConnectionAsync(user.Id);

        var (messageId, _) = await SeedMessageAsync(user.Id, "pjx", attachmentCount: 1);

        // L'identifiant existe ailleurs, mais pas pour CE message : l'index composite fait
        // foi et l'on n'appelle pas Gmail pour rien.
        var response = await user.Client.GetAsync($"/api/gmail/messages/{messageId}/attachments/att-pjiso-0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task G30_Une_image_non_image_ne_passe_pas_par_le_proxy_inline()
    {
        var user = await CreateUserAsync("gmail-pj-e");
        await EnsureConnectionAsync(user.Id);

        var (messageId, _) = await SeedMessageAsync(user.Id, "svgx", attachmentCount: 1);

        await _factory.WithDbAsync(async db =>
        {
            var attachment = await db.GmailAttachments.FirstAsync(a => a.GmailAttachmentId == "att-svgx-0");
            attachment.MimeType = "image/svg+xml";
            await db.SaveChangesAsync();
        });

        // Un SVG est un document actif : même « inline », il ne doit pas être rendu.
        var response = await user.Client.GetAsync($"/api/gmail/messages/{messageId}/inline/logo-ims");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Phase 3 (bis) : recherche ───────────────────────────────────────

    [Fact]
    public async Task G31_La_recherche_porte_sur_les_quatre_champs_et_ignore_la_casse()
    {
        var user = await CreateUserAsync("gmail-recherche-a");
        await EnsureConnectionAsync(user.Id);

        await SeedMessageAsync(user.Id, "r1", subject: "Commande TISSU 4821",
            bodyText: "Le tissu bleu est en rupture sur la série 4821.");
        await SeedMessageAsync(user.Id, "r2", subject: "Facture janvier",
            bodyText: "Aucun rapport avec les textiles.");
        await SeedMessageAsync(user.Id, "r3", subject: "Avis d'expédition",
            bodyText: "Le colis partira jeudi.");

        // « TISSU » en majuscules doit trouver l'objet écrit « TISSU ».
        var majuscules = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages?search=TISSU");
        Assert.Equal(1, majuscules.GetProperty("total").GetInt32());

        // Un terme présent UNIQUEMENT dans le corps, loin de l'extrait tronqué par Gmail.
        var corps = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages?search=rupture%20sur%20la");
        Assert.Equal(1, corps.GetProperty("total").GetInt32());

        // Un terme de l'expéditeur (tous les messages du test partagent ce From).
        var expediteur = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages?search=client@exemple");
        Assert.Equal(3, expediteur.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task G32_Les_jokers_de_recherche_ne_sont_pas_interpretes()
    {
        var user = await CreateUserAsync("gmail-recherche-b");
        await EnsureConnectionAsync(user.Id);

        await SeedMessageAsync(user.Id, "j1", subject: "Taux 100% coton", bodyText: "Composition 100% coton.");
        await SeedMessageAsync(user.Id, "j2", subject: "Autre chose", bodyText: "Sans pourcentage.");

        // « % » est un joker LIKE : échappé, il doit se comporter comme un caractère
        // ordinaire, donc ne ramener QUE le message qui contient vraiment « 100% ».
        var result = await user.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages?search=100%25");
        Assert.Equal(1, result.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task G33_La_recherche_ne_fuit_pas_les_emails_d_autrui()
    {
        var alice = await CreateUserAsync("gmail-recherche-c");
        var bob = await CreateUserAsync("gmail-recherche-d");
        await EnsureConnectionAsync(alice.Id);
        await EnsureConnectionAsync(bob.Id);

        await SeedMessageAsync(alice.Id, "s1", subject: "MOTS SECRETS ALICE", bodyText: "rien");
        await SeedMessageAsync(bob.Id, "s2", subject: "MOTS SECRETS BOB", bodyText: "rien");

        var result = await bob.Client.GetFromJsonAsync<JsonElement>("/api/gmail/messages?search=MOTS%20SECRETS");
        var items = result.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(1, items.Count);
        Assert.Contains("BOB", items[0].GetProperty("subject").GetString());
    }

    // ── Phase 4 : envoi ──────────────────────────────────────────────────

    [Fact]
    public async Task G34_L_envoi_refuse_un_destinataire_invalide()
    {
        var user = await CreateUserAsync("gmail-envoi-a");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/send", new
        {
            to = new[] { "ceci n'est pas une adresse" },
            subject = "Test",
            bodyText = "Bonjour"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task G35_L_envoi_refuse_un_message_vide()
    {
        var user = await CreateUserAsync("gmail-envoi-b");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/send", new
        {
            to = new[] { "client@exemple.fr" },
            subject = "Test",
            bodyText = "",
            bodyHtml = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task G36_L_envoi_refuse_une_piece_jointe_hors_plafond_de_20_mo()
    {
        var user = await CreateUserAsync("gmail-envoi-c");
        await EnsureConnectionAsync(user.Id);

        // 21 Mo en Base64 : au-delà du plafond IMS de 20 Mo, et le refus doit survenir
        // AVANT tout appel à Gmail.
        var gros = new byte[21 * 1024 * 1024];
        Random.Shared.NextBytes(gros.AsSpan(0, 1024));

        var response = await user.Client.PostAsJsonAsync("/api/gmail/send", new
        {
            to = new[] { "client@exemple.fr" },
            subject = "Gros fichier",
            bodyText = "Voici le plan de coupe.",
            attachments = new[]
            {
                new { fileName = "plan.pdf", mimeType = "application/pdf", contentBase64 = Convert.ToBase64String(gros) }
            }
        });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task G37_L_envoi_refuse_un_contenu_piece_jointe_invalide()
    {
        var user = await CreateUserAsync("gmail-envoi-d");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/send", new
        {
            to = new[] { "client@exemple.fr" },
            subject = "Test",
            bodyText = "Bonjour",
            attachments = new[]
            {
                new { fileName = "plan.pdf", mimeType = "application/pdf", contentBase64 = "ceci-n-est-pas-du-base64!!" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Phase 5 : édition IA ─────────────────────────────────────────────

    [Theory]
    [InlineData("DE")]
    [InlineData("ES")]
    [InlineData("zh")]
    [InlineData("")]
    public async Task G38_Une_langue_hors_liste_fermee_est_refusee(string langue)
    {
        var user = await CreateUserAsync("gmail-ia-a");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "Bonjour, le tissu arrive vendredi.",
            action = "Translate",
            targetLanguage = langue
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("EN")]
    [InlineData("AR")]
    [InlineData("fr")]
    public void Les_trois_langues_autorisees_sont_acceptees(string langue)
    {
        Assert.True(TranslateLanguage.IsSupported(langue));
    }

    [Fact]
    public async Task G39_Une_action_inconnue_est_refusee()
    {
        var user = await CreateUserAsync("gmail-ia-b");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "Bonjour",
            action = "SupprimerTout"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task G40_L_edition_ia_ne_cree_aucun_enregistrement()
    {
        var user = await CreateUserAsync("gmail-ia-c");
        await EnsureConnectionAsync(user.Id);
        await SeedMessageAsync(user.Id, "noedit");

        var avant = await _factory.WithDbAsync(async db => new
        {
            Reponses = await db.EmailAiReponses.CountAsync(),
            Analyses = await db.EmailAiAnalyses.CountAsync()
        });

        // Le harnais retire GROQ_API_KEY de l'environnement (TacheApiFactory) : l'IA est
        // donc indisponible, la réponse attendue est un 503 propre, et surtout AUCUNE
        // ligne créée. Un appel au vrai Groq depuis les tests serait payant, non
        // déterministe, et dépendrait du poste qui exécute la suite.
        var response = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "Bonjour, le tissu arrive vendredi.",
            action = "Rewrite",
            instruction = "Rends la formulation plus polie"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var apres = await _factory.WithDbAsync(async db => new
        {
            Reponses = await db.EmailAiReponses.CountAsync(),
            Analyses = await db.EmailAiAnalyses.CountAsync()
        });

        Assert.Equal(avant.Reponses, apres.Reponses);
        Assert.Equal(avant.Analyses, apres.Analyses);
    }

    [Fact]
    public async Task G41_Sans_droit_courriels_l_edition_ia_refusee()
    {
        var user = await _factory.CreateUserAsync("gmail-ia-d", "S", "D");
        await EnsureConnectionAsync(user.Id);

        var response = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "Bonjour",
            action = "Rewrite"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Non-régression : Gmail rattache l'étoile et le trombone au FIL, pas au dernier
    /// message. Lire l'état du message le plus récent faisait disparaître le suivi dès
    /// qu'un tiers répondait, et masquait la pièce jointe d'un message plus ancien.
    /// </summary>
    [Fact]
    public async Task G42_Le_suivi_et_les_pieces_jointes_portent_sur_le_fil_entier()
    {
        var user = await CreateUserAsync("gmail-fils-suivi");
        await EnsureConnectionAsync(user.Id);

        // Le PREMIER message est suivi et porte une pièce jointe ; le dernier ne l'est pas.
        // C'est la situation réelle : on suit le mail de départ, la client répond.
        await SeedMessageAsync(user.Id, "s1", threadId: "suivi", receivedAt: DateTime.UtcNow.AddHours(-2),
            isStarred: true, hasAttachments: true);
        await SeedMessageAsync(user.Id, "s2", threadId: "suivi", receivedAt: DateTime.UtcNow.AddHours(-1));

        var response = await user.Client.GetAsync("/api/gmail/threads?pageSize=50");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fil = page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("gmailThreadId").GetString() == "suivi");

        Assert.True(fil.GetProperty("isStarred").GetBoolean(),
            "Le fil doit rester suivi même si seul un ancien message l'est.");
        Assert.True(fil.GetProperty("hasAttachments").GetBoolean(),
            "La pièce jointe d'un message plus ancien doit rester visible dans la liste.");
    }
}
