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
/// Composeur unique : « Nouveau message », « Répondre », « Répondre à tous » et
/// « Transférer » partagent le même éditeur, la même barre d'outils et le MÊME endpoint
/// d'envoi.
/// <para>
/// Ce qui est vérifié ici tient en quatre exigences :</para>
/// <list type="bullet">
/// <item>la citation du message d'origine n'entre JAMAIS dans la zone de rédaction, donc
/// « Reformuler » ou « Traduire » ne peut pas la réécrire ;</item>
/// <item>ce qui part est le texte affiché, dans les quatre modes — c'est la régression du
/// bug A1, rejouée sur le nouveau chemin d'envoi ;</item>
/// <item>la trace IA conserve le texte généré ET le texte réellement envoyé ;</item>
/// <item>l'IA ne déclenche jamais d'envoi : seul un envoi volontaire la solde.</item>
/// </list>
/// <para>
/// Tous les échanges passent par le double enregistreur : aucun appel à l'API Google
/// réelle, aucun appel à Groq. Les textes sont scriptés, donc les assertions portent sur
/// des faits et non sur le style d'un modèle.</para>
/// </summary>
public class GmailComposeTests : IClassFixture<GmailComposeApiFactory>
{
    private readonly GmailComposeApiFactory _factory;

    public GmailComposeTests(GmailComposeApiFactory factory) => _factory = factory;

    private Task<TestUser> CreateUserAsync(string id) =>
        _factory.CreateUserAsync(id, id.ToUpperInvariant()[0].ToString(), id, role =>
        {
            role.PeutVoirCourriels = true;
            role.PeutGererCourriels = true;
        });

    private async Task EnsureConnectionAsync(string userId, string adresse = null!)
    {
        await _factory.WithDbAsync(async db =>
        {
            if (await db.GmailConnections.AnyAsync(c => c.UserId == userId)) return;

            db.GmailConnections.Add(new GmailConnection
            {
                UserId = userId,
                GmailAddress = adresse ?? userId + "@gmail.test",
                GoogleUserId = "google-" + userId,
                RefreshTokenEncrypted = "jeton-chiffre-de-test",
                GrantedScopes = "https://www.googleapis.com/auth/gmail.modify",
                ConnectedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
    }

    private Task<int> SeedMessageAsync(
        string userId,
        string suffixe,
        string from = "client@exemple.fr",
        string? to = null,
        string? cc = null,
        string? subject = null,
        string? bodyText = "Bonjour,\nle tissu arrive vendredi.",
        string? bodyHtml = null) =>
        _factory.WithDbAsync(async db =>
        {
            var connection = await db.GmailConnections.FirstAsync(c => c.UserId == userId);

            var message = new GmailMessage
            {
                GmailConnectionId = connection.Id,
                GmailMessageId = "msg-" + suffixe,
                GmailThreadId = "thread-" + suffixe,
                Rfc822MessageId = "<" + suffixe + "@exemple.fr>",
                From = from,
                To = to ?? userId + "@gmail.test",
                Cc = cc,
                Subject = subject ?? "Commande " + suffixe,
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                ReceivedAt = new DateTime(2026, 6, 12, 9, 15, 0, DateTimeKind.Utc),
                IsRead = true,
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };
            db.GmailMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });

    private Task<GmailMessage> ReadMessageAsync(int id) =>
        _factory.WithDbAsync(db => db.GmailMessages.AsNoTracking().FirstAsync(m => m.Id == id));

    private Task<List<EmailAiReply>> ReadRepliesAsync(int messageId) =>
        _factory.WithDbAsync(db => db.EmailAiReponses
            .Where(r => r.GmailMessageId == messageId)
            .OrderBy(r => r.Id)
            .ToListAsync());

    /// <summary>
    /// Un échec doit dire POURQUOI le serveur a refusé : une assertion sur le seul code
    /// HTTP transformerait un 400 en « je ne sais pas quoi corriger ».
    /// </summary>
    private static async Task AssertOkAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.OK) return;

        var corps = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Attendu 200, reçu {(int)response.StatusCode} {response.StatusCode} — {corps}");
    }

    private async Task<JsonElement> PrefillAsync(TestUser user, int messageId, ComposeMode mode)
    {
        var response = await user.Client.GetAsync(
            $"/api/gmail/compose/prefill?messageId={messageId}&mode={mode}");
        await AssertOkAsync(response);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> SendAsync(TestUser user, object dto) =>
        user.Client.PostAsJsonAsync("/api/gmail/send", dto);

    private void Reset()
    {
        _factory.ResetSimulatedFailures();
        _factory.Ai.Reset();
        while (_factory.Recorder.DirectSends.TryDequeue(out _)) { }
        while (_factory.Recorder.DraftDeletions.TryDequeue(out _)) { }
    }

    // ── Préremplissage : la citation reste HORS de la zone de rédaction ──────────

    [Fact]
    public async Task Prefill_reponse_cible_l_expediteur_et_laisse_la_zone_de_redaction_vide()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-rep");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "rep");

        var rapport = await PrefillAsync(user, messageId, ComposeMode.Reply);

        Assert.Equal("Reply", rapport.GetProperty("mode").GetString());
        Assert.Equal(new[] { "client@exemple.fr" }, rapport.GetProperty("to").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty( rapport.GetProperty("cc").EnumerateArray());
        Assert.Equal("Re: Commande rep", rapport.GetProperty("subject").GetString());
        Assert.Equal(messageId, rapport.GetProperty("replyToMessageId").GetInt32());

        // Règle 3 : la zone de rédaction ne contient AUCUNE citation. Si elle en contenait,
        // « Traduire » réécrirait le texte de l'expéditeur.
        Assert.Equal(string.Empty, rapport.GetProperty("bodyText").GetString());

        // Elle est en revanche affichée, en lecture seule, sous la zone de rédaction.
        var citation = rapport.GetProperty("quotedText").GetString() ?? "";
        Assert.Contains("client@exemple.fr", citation);
        Assert.Contains("> Bonjour,", citation);
    }

    [Fact]
    public async Task Prefill_repondre_a_tous_met_les_participants_en_copie_sans_soi_meme()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-tous");
        await EnsureConnectionAsync(user.Id, "moi@atelier.test");
        var messageId = await SeedMessageAsync(
            user.Id, "tous",
            from: "Client <client@exemple.fr>",
            to: "moi@atelier.test, colleague@exemple.fr",
            cc: "client@exemple.fr, qualite@exemple.fr");

        var rapport = await PrefillAsync(user, messageId, ComposeMode.ReplyAll);

        // Le mode doit être celui demandé : s'il revenait « Reply », les participants
        // resteraient tous en « To » et la copie vide s'expliquerait par le mode, pas
        // par les en-têtes.
        Assert.Equal("ReplyAll", rapport.GetProperty("mode").GetString());

        // Les en-têtes du message semé sont la base du test : sans eux, il n'y aurait
        // simplement personne à mettre en copie, et le reste passerait à vide.
        var message = await ReadMessageAsync(messageId);
        Assert.Contains("colleague@exemple.fr", message.To);
        Assert.Contains("qualite@exemple.fr", message.Cc);

        // L'objet prouve que le composeur a bien reçu le message semé : sans cette
        // vérification, un échec sur les participants serait cherché du mauvais côté.
        // Le serveur ajoute le préfixe, et ne l'ajoute qu'une fois.
        Assert.Equal("Re: Commande tous", rapport.GetProperty("subject").GetString());

        var to = rapport.GetProperty("to").EnumerateArray().Select(e => e.GetString()).ToList();
        var cc = rapport.GetProperty("cc").EnumerateArray().Select(e => e.GetString()).ToList();

        // L'expéditeur est le destinataire principal, même s'il est aussi en copie.
        Assert.Equal(new[] { "client@exemple.fr" }, to);

        // Les autres participants vont en copie, l'adresse du compte connecté est retirée
        // (se mettre en copie de sa propre réponse n'aide personne), et aucun doublon —
        // l'expéditeur figurait dans le Cc du message reçu.
        Assert.Equal(new[] { "colleague@exemple.fr", "qualite@exemple.fr" }, cc);
        Assert.DoesNotContain("moi@atelier.test", cc);
        Assert.DoesNotContain("client@exemple.fr", cc);
    }

    [Fact]
    public async Task Prefill_transfert_choisit_le_destinataire_par_l_utilisateur()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-trs");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "trs");

        var rapport = await PrefillAsync(user, messageId, ComposeMode.Forward);

        // Pré-remplir les participants d'un transfert le transformerait en « répondre à
        // tous » par un appui sur le mauvais bouton.
        Assert.Empty(rapport.GetProperty("to").EnumerateArray());
        Assert.Empty(rapport.GetProperty("cc").EnumerateArray());
        Assert.Equal("Fwd: Commande trs", rapport.GetProperty("subject").GetString());
        Assert.Contains(
            "---------- Message transféré ----------",
            rapport.GetProperty("quotedText").GetString());
        Assert.Equal(string.Empty, rapport.GetProperty("bodyText").GetString());
    }

    [Fact]
    public async Task Prefill_ne_prefixe_pas_un_objet_deja_repondu()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-obj");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "obj", subject: "Re: Commande 42");

        var rapport = await PrefillAsync(user, messageId, ComposeMode.Reply);

        // Sinon une réponse à une réponse devient « Re: Re: » à chaque aller-retour.
        Assert.Equal("Re: Commande 42", rapport.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Prefill_refuse_un_mode_sans_message_de_reference()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-new");

        var response = await user.Client.GetAsync("/api/gmail/compose/prefill?messageId=1&mode=New");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Prefill_refuse_un_message_qui_n_appartient_pas_a_l_utilisateur()
    {
        Reset();
        var lecteur = await CreateUserAsync("cmp-prefill-autre");
        var autre = await CreateUserAsync("cmp-prefill-proprio");
        await EnsureConnectionAsync(lecteur.Id);
        await EnsureConnectionAsync(autre.Id);
        var messageId = await SeedMessageAsync(autre.Id, "prive");

        var response = await lecteur.Client.GetAsync(
            $"/api/gmail/compose/prefill?messageId={messageId}&mode=Reply");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Prefill_ajoute_une_proposition_ia_encore_exploitable()
    {
        Reset();
        var user = await CreateUserAsync("cmp-prefill-ia");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "ia");

        _factory.Ai.GeneratedText = "Proposition déjà générée.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });
        Assert.Equal(HttpStatusCode.OK, generation.StatusCode);
        var creee = await generation.Content.ReadFromJsonAsync<JsonElement>();

        var rapport = await PrefillAsync(user, messageId, ComposeMode.Reply);

        // Régénérer une proposition déjà payée et déjà lue serait du gaspillage : le
        // composeur propose l'existante.
        Assert.Equal(creee.GetProperty("id").GetInt32(), rapport.GetProperty("aiReplyId").GetInt32());
    }

    // ── Régression A1 : le texte affiché est celui qui part, dans les quatre modes ──

    [Theory]
    [InlineData(ComposeMode.New, null)]
    [InlineData(ComposeMode.Reply, "rep")]
    [InlineData(ComposeMode.ReplyAll, "tous")]
    [InlineData(ComposeMode.Forward, "trs")]
    public async Task Le_texte_affiche_est_celui_qui_part_dans_tous_les_modes(
        ComposeMode mode, string? suffixe)
    {
        Reset();
        var user = await CreateUserAsync("cmp-envoi-" + mode);
        await EnsureConnectionAsync(user.Id);

        int? messageId = null;
        if (suffixe != null) messageId = await SeedMessageAsync(user.Id, suffixe);

        // Un texte saisi à l'écran : il ne figure nulle part en base, c'est donc bien
        // l'affichage qui est Asserté, et non une relecture de quelque chose de stocké.
        const string saisie = "Bonjour Madame,\nle tissu est bien arrive.\nCordialement.";
        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            subject = "Commande — suite",
            bodyText = saisie,
            mode = mode.ToString(),
            replyToMessageId = messageId
        });

        await AssertOkAsync(reponse);

        var envoi = Assert.Single(_factory.Recorder.DirectSends);
        Assert.StartsWith(saisie, envoi.BodyText);
        Assert.Equal("Commande — suite", envoi.Subject);

        // Le fil et l'In-Reply-To viennent du serveur, jamais du client.
        if (mode == ComposeMode.New)
        {
            Assert.Null(envoi.ThreadId);
            Assert.Null(envoi.InReplyTo);
        }
        else
        {
            var reference = await ReadMessageAsync(messageId!.Value);
            Assert.Equal(reference.GmailThreadId, envoi.ThreadId);
            Assert.Equal(reference.Rfc822MessageId, envoi.InReplyTo);
        }
    }

    [Fact]
    public async Task La_citation_envoyee_est_celle_du_serveur_pas_celle_du_client()
    {
        Reset();
        var user = await CreateUserAsync("cmp-citation");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "cit", bodyText: "Le texte ORIGINAL de l'expéditeur.");

        // Un client qui tente d'injecter sa propre citation ne peut pas la placer dans le
        // texte envoyé : le serveur ajoute la sienne, après le texte de l'utilisateur.
        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "Ma réponse.\n\n-- le client prétend que ceci est la citation",
            mode = "Reply",
            replyToMessageId = messageId
        });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var envoi = Assert.Single(_factory.Recorder.DirectSends);
        Assert.StartsWith("Ma réponse.", envoi.BodyText);
        Assert.Contains("Le texte ORIGINAL de l'expéditeur.", envoi.BodyText);
    }

    [Fact]
    public async Task Un_transfert_sans_note_de_l_utilisateur_est_un_transfert_valide()
    {
        Reset();
        var user = await CreateUserAsync("cmp-trs-vide");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "trsvide", bodyText: "Ci-joint le bon pour commande.");

        // « Je te transfère, tu en fais ce que tu veux » : le corps de l'utilisateur est
        // vide, et c'est normal. Le serveur y ajoute la citation complète.
        var reponse = await SendAsync(user, new
        {
            to = new[] { "tiers@exemple.fr" },
            bodyText = "",
            mode = "Forward",
            replyToMessageId = messageId
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var envoi = Assert.Single(_factory.Recorder.DirectSends);
        Assert.Contains("---------- Message transféré ----------", envoi.BodyText);
        Assert.Contains("Ci-joint le bon pour commande.", envoi.BodyText);
    }

    [Fact]
    public async Task Un_message_neuf_sans_texte_est_refuse()
    {
        Reset();
        var user = await CreateUserAsync("cmp-new-vide");

        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "   ",
            mode = "New"
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    [Fact]
    public async Task Un_mode_autre_que_nouveau_sans_message_de_reference_est_refuse()
    {
        Reset();
        var user = await CreateUserAsync("cmp-sans-ref");

        // Sans message de référence, la réponse partirait sans fil ni In-Reply-To : elle
        // arriverait dans la boîte comme un message orphelin, sans rapport avec le fil.
        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "Bonjour.",
            mode = "ReplyAll"
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Empty(_factory.Recorder.DirectSends);
    }

    [Fact]
    public async Task Un_message_de_reference_appartenant_a_autre_connexion_est_refuse()
    {
        Reset();
        var attaquant = await CreateUserAsync("cmp-ref-prive");
        var proprietaire = await CreateUserAsync("cmp-ref-proprio");
        await EnsureConnectionAsync(proprietaire.Id);
        var messageId = await SeedMessageAsync(proprietaire.Id, "confidentiel");

        var reponse = await SendAsync(attaquant, new
        {
            to = new[] { "attaquant@exemple.fr" },
            bodyText = "Bonjour.",
            mode = "Reply",
            replyToMessageId = messageId
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Empty(_factory.Recorder.DirectSends);
    }

    // ── Trace IA : le texte généré ET le texte envoyé, soldés à l'envoi seulement ──

    [Fact]
    public async Task La_trace_garde_le_texte_genere_et_le_texte_reellement_envoye()
    {
        Reset();
        var user = await CreateUserAsync("cmp-trace");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "trc");

        _factory.Ai.GeneratedText = "PROPOSITION DE L'ASSISTANCE.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });
        Assert.Equal(HttpStatusCode.OK, generation.StatusCode);
        var proposition = await generation.Content.ReadFromJsonAsync<JsonElement>();
        var propositionId = proposition.GetProperty("id").GetInt32();

        // L'utilisateur corrige le texte de la proposition avant de l'envoyer.
        const string relu = "Bonjour, le tissu arrive bien vendredi. Cordialement.";
        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            subject = "Re: Commande trc",
            bodyText = relu,
            mode = "Reply",
            replyToMessageId = messageId,
            aiReplyId = propositionId
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var trace = Assert.Single(await ReadRepliesAsync(messageId));
        Assert.Equal("PROPOSITION DE L'ASSISTANCE.", trace.Body); // ce que l'IA a écrit, intact
        Assert.Equal(relu, trace.SentBody);                       // ce qui est réellement parti
        Assert.Equal("Re: Commande trc", trace.SentSubject);
        Assert.Equal(StatutReponseIa.Sent, trace.Statut);
        Assert.NotNull(trace.SentAt);
        Assert.NotNull(trace.GmailSentMessageId);
    }

    [Fact]
    public async Task Un_echec_gmail_ne_solde_pas_la_proposition()
    {
        Reset();
        var user = await CreateUserAsync("cmp-trace-echec");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "echec");

        _factory.Ai.GeneratedText = "PROPOSITION.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });
        var proposition = await generation.Content.ReadFromJsonAsync<JsonElement>();

        _factory.Recorder.FailingSend = true;
        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "Ma réponse.",
            mode = "Reply",
            replyToMessageId = messageId,
            aiReplyId = proposition.GetProperty("id").GetInt32()
        });

        Assert.Equal(HttpStatusCode.BadGateway, reponse.StatusCode);

        // La proposition reste réutilisable : marquer « envoyé » alors que Gmail a échoué
        // ferait disparaître une proposition encore perfectible de l'écran.
        var trace = Assert.Single(await ReadRepliesAsync(messageId));
        Assert.Equal(StatutReponseIa.Generated, trace.Statut);
        Assert.Null(trace.SentAt);
        Assert.Null(trace.SentBody);
    }

    [Fact]
    public async Task Une_proposition_portant_sur_autre_message_est_refusee()
    {
        Reset();
        var user = await CreateUserAsync("cmp-trace-croisee");
        await EnsureConnectionAsync(user.Id);
        var premier = await SeedMessageAsync(user.Id, "un");
        var second = await SeedMessageAsync(user.Id, "deux");

        _factory.Ai.GeneratedText = "PROPOSITION.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{premier}/replies", new { instruction = (string?)null });
        var proposition = await generation.Content.ReadFromJsonAsync<JsonElement>();

        var reponse = await SendAsync(user, new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "Ma réponse.",
            mode = "Reply",
            replyToMessageId = second,
            aiReplyId = proposition.GetProperty("id").GetInt32()
        });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Empty(_factory.Recorder.DirectSends);
    }

    [Fact]
    public async Task Une_proposition_deja_envoyee_ne_peut_pas_etre_reenvoyee()
    {
        Reset();
        var user = await CreateUserAsync("cmp-trace-double");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "double");

        _factory.Ai.GeneratedText = "PROPOSITION.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });
        var proposition = await generation.Content.ReadFromJsonAsync<JsonElement>();
        var propositionId = proposition.GetProperty("id").GetInt32();

        var dto = new
        {
            to = new[] { "client@exemple.fr" },
            bodyText = "Ma réponse.",
            mode = "Reply",
            replyToMessageId = messageId,
            aiReplyId = propositionId
        };
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(user, dto)).StatusCode);

        // Le même identifiant ne peut pas produire deux envois : le premier message est
        // déjà parti, le second ne ferait qu'une trace fausse.
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(user, dto)).StatusCode);
    }

    [Fact]
    public async Task Abandonner_une_proposition_la_refuse_et_supprime_le_brouillon_gmail()
    {
        Reset();
        var user = await CreateUserAsync("cmp-abandon");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "aband");

        _factory.Ai.GeneratedText = "PROPOSITION.";
        var generation = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });
        var proposition = await generation.Content.ReadFromJsonAsync<JsonElement>();
        var propositionId = proposition.GetProperty("id").GetInt32();

        var brouillon = await user.Client.PostAsync(
            $"/api/gmail/replies/{propositionId}/draft", null);
        Assert.Equal(HttpStatusCode.OK, brouillon.StatusCode);
        var avecBrouillon = await brouillon.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(avecBrouillon.GetProperty("gmailDraftId").GetString()));

        var refus = await user.Client.PostAsync($"/api/gmail/replies/{propositionId}/reject", null);
        Assert.Equal(HttpStatusCode.NoContent, refus.StatusCode);

        // Piste d'audit : la trace subsiste, marquée refusée…
        var trace = Assert.Single(await ReadRepliesAsync(messageId));
        Assert.Equal(StatutReponseIa.Rejected, trace.Statut);
        Assert.Equal("PROPOSITION.", trace.Body);

        // …et le brouillon Gmail disparaît, sans quoi une proposition abandonnée
        // continuerait de traîner dans la boîte de rédaction.
        Assert.Equal(
            avecBrouillon.GetProperty("gmailDraftId").GetString(),
            Assert.Single(_factory.Recorder.DraftDeletions));
        Assert.Null(trace.GmailDraftId);
    }

    // ── Génération IA : le mode change la consigne, la citation n'est jamais réécrite ──

    [Theory]
    [InlineData(ComposeMode.Reply)]
    [InlineData(ComposeMode.ReplyAll)]
    [InlineData(ComposeMode.Forward)]
    public async Task La_generation_recoit_le_mode_du_composeur(ComposeMode mode)
    {
        Reset();
        var user = await CreateUserAsync("cmp-gen-" + mode);
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "gen" + mode);

        _factory.Ai.GeneratedText = "Texte généré.";
        var reponse = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies",
            new { instruction = "sois bref", mode = mode.ToString() });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        // Le fil est transmis comme contexte, et le mode suit : un transfert attend une
        // note d'accompagnement, pas une réponse à l'expéditeur.
        var appel = Assert.Single(_factory.Ai.Generates);
        Assert.Equal(mode, appel.Mode);
        Assert.Equal("client@exemple.fr", appel.EmailFrom);
        Assert.Equal("sois bref", appel.Instruction);
        Assert.Contains("tissu", appel.Body);
    }

    [Fact]
    public async Task Une_generation_vide_en_transfert_est_refusee_sans_laisser_de_trace()
    {
        Reset();
        var user = await CreateUserAsync("cmp-gen-vide");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "genvide");

        _factory.Ai.GeneratedText = "   ";
        var reponse = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies",
            new { instruction = (string?)null, mode = "Forward" });

        Assert.Equal(HttpStatusCode.BadGateway, reponse.StatusCode);

        // Aucune trace vide : une proposition sans texte n'expliquerait rien à l'écran.
        Assert.Empty(await ReadRepliesAsync(messageId));
    }

    [Fact]
    public async Task Une_reponse_vide_tombe_sur_le_cordialement_d_usage()
    {
        Reset();
        var user = await CreateUserAsync("cmp-gen-courte");
        await EnsureConnectionAsync(user.Id);
        var messageId = await SeedMessageAsync(user.Id, "court");

        _factory.Ai.GeneratedText = "  ";
        var reponse = await user.Client.PostAsJsonAsync(
            $"/api/gmail/messages/{messageId}/replies", new { instruction = (string?)null });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var trace = Assert.Single(await ReadRepliesAsync(messageId));
        Assert.Equal("Cordialement,", trace.Body);
    }

    [Fact]
    public async Task La_generation_sans_fil_exige_une_consigne()
    {
        Reset();
        var user = await CreateUserAsync("cmp-new-ia");

        // « Nouveau message » n'a pas de fil à transmettre : sans consigne, il n'y a rien
        // à demander au modèle. Le refus est explicite plutôt qu'un texte inventé.
        var sansConsigne = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "",
            action = "Generate",
            instruction = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, sansConsigne.StatusCode);
        Assert.Contains(
            "consigne",
            await sansConsigne.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        _factory.Ai.EditedText = "Bonjour, voici notre disponibilite.";
        var avecConsigne = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "",
            action = "Generate",
            instruction = "confirmer la disponibilite pour jeudi"
        });
        // AssertOkAsync renvoie le message d'erreur du serveur : un 400 ici se lit
        // directement au lieu d'être un simple « BadRequest » sans explication.
        await AssertOkAsync(avecConsigne);

        var appel = Assert.Single(_factory.Ai.Edits);
        Assert.Equal(DraftEditAction.Generate, appel.Action);
        Assert.Equal("confirmer la disponibilite pour jeudi", appel.Instruction);
    }

    [Fact]
    public async Task Les_actions_ia_agissent_sur_le_texte_courant_du_composeur()
    {
        Reset();
        var user = await CreateUserAsync("cmp-edits");
        await EnsureConnectionAsync(user.Id);

        _factory.Ai.EditedText = "Texte reecrit.";
        var reponse = await user.Client.PostAsJsonAsync("/api/gmail/drafts/edit", new
        {
            text = "mon texte modifié à la main",
            action = "Rewrite",
            instruction = "plus bref"
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        // Règle 2 : l'action IA porte sur le texte AFFICHÉ, y compris s'il a été corrigé
        // à la main — et jamais sur la citation, qui n'est pas transmise ici.
        var appel = Assert.Single(_factory.Ai.Edits);
        Assert.Equal(DraftEditAction.Rewrite, appel.Action);
        Assert.Equal("mon texte modifié à la main", appel.Text);
    }
}

/// <summary>Assertion de commodité : la réponse est un 200 et porte du texte.</summary>
internal static class ComposeAssertExtensions
{
    public static HttpResponseMessage ConsigneEnPlace(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }
}