using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Email;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Emails système par Gmail : l'émetteur résout la connexion OAuth de l'adresse
/// configurée, envoie via <see cref="IGmailApiService"/> et traduit tout échec en
/// <see cref="EmailEnvoyeException"/>. Aucun appel réseau : le service Gmail est un
/// double qui n'enregistre que l'envoi.
/// </summary>
public class SystemEmailSenderTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public SystemEmailSenderTests(AuthApiFactory factory) => _factory = factory;

    private static IConfiguration Config(string? adresse) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SenderGmailAddress"] = adresse
            })
            .Build();

    private async Task<string> CreerUtilisateurAsync()
    {
        var userId = string.Empty;
        await _factory.WithUserManagerAsync(async um =>
        {
            var user = new ApplicationUser
            {
                UserName = $"sys-{Guid.NewGuid():N}",
                Email = $"sys-{Guid.NewGuid():N}@example.com",
                EstActif = true,
                EmailConfirmed = true,
            };
            var result = await um.CreateAsync(user, "MotDePasse123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Création du compte de test impossible : " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            userId = user.Id;
        });
        return userId;
    }

    private async Task CreerConnexionAsync(string adresse, bool active = true)
    {
        var userId = await CreerUtilisateurAsync();
        await _factory.WithDbAsync(async db =>
        {
            db.GmailConnections.Add(new GmailConnection
            {
                UserId = userId,
                GmailAddress = adresse,
                GoogleUserId = $"google-{Guid.NewGuid():N}",
                RefreshTokenEncrypted = "jeton-chiffre-factice",
                GrantedScopes = GmailScopes.All,
                IsActive = active,
            });
            await db.SaveChangesAsync();
        });
    }

    private Task AvecEmetteurAsync(
        RecordingSendGmailApi recorder, string? adresse, Func<GmailApiEmailSender, Task> action)
        => _factory.WithScopeAsync(sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var sender = new GmailApiEmailSender(db, recorder, Config(adresse), NullLogger<GmailApiEmailSender>.Instance);
            return action(sender);
        });

    private Task<T> AvecEmetteurAsync<T>(
        RecordingSendGmailApi recorder, string? adresse, Func<GmailApiEmailSender, Task<T>> action)
        => _factory.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var sender = new GmailApiEmailSender(db, recorder, Config(adresse), NullLogger<GmailApiEmailSender>.Instance);
            return await action(sender);
        });

    [Fact]
    public async Task Envoi_utilise_la_connexion_active_de_l_adresse_systeme()
    {
        var adresse = $"system-{Guid.NewGuid():N}@gmail.com";
        await CreerConnexionAsync(adresse);

        var recorder = new RecordingSendGmailApi();
        await AvecEmetteurAsync(recorder, adresse,
            sender => sender.SendAsync("destinataire@example.com", "Bienvenue", "<p>Choisissez votre mot de passe</p>"));

        var appel = Assert.Single(recorder.Sends);
        Assert.Equal(adresse, appel.Connection.GmailAddress);
        Assert.Equal("destinataire@example.com", appel.To);
        Assert.Equal("Bienvenue", appel.Subject);
        Assert.Equal("<p>Choisissez votre mot de passe</p>", appel.BodyHtml);
        Assert.Null(appel.BodyText);
    }

    [Fact]
    public async Task Adresse_systeme_resolue_sans_tenir_compte_de_la_casse()
    {
        var adresse = $"System-{Guid.NewGuid():N}@Gmail.com";
        await CreerConnexionAsync(adresse);

        var recorder = new RecordingSendGmailApi();
        await AvecEmetteurAsync(recorder, adresse.ToLowerInvariant(),
            sender => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.Single(recorder.Sends);
    }

    [Fact]
    public async Task Envoi_sans_connexion_active_leve_EmailEnvoyeException()
    {
        var adresse = $"absent-{Guid.NewGuid():N}@gmail.com";
        var recorder = new RecordingSendGmailApi();

        await AvecEmetteurAsync<EmailEnvoyeException>(recorder, adresse,
            async sender => await Assert.ThrowsAsync<EmailEnvoyeException>(
                () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>")));

        Assert.Empty(recorder.Sends);
    }

    [Fact]
    public async Task Connexion_desactivee_ne_peut_pas_emettre()
    {
        var adresse = $"inactif-{Guid.NewGuid():N}@gmail.com";
        await CreerConnexionAsync(adresse, active: false);

        var recorder = new RecordingSendGmailApi();
        await AvecEmetteurAsync<EmailEnvoyeException>(recorder, adresse,
            async sender => await Assert.ThrowsAsync<EmailEnvoyeException>(
                () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>")));

        Assert.Empty(recorder.Sends);
    }

    [Fact]
    public async Task Echec_Gmail_est_emballe_en_EmailEnvoyeException()
    {
        var adresse = $"echec-{Guid.NewGuid():N}@gmail.com";
        await CreerConnexionAsync(adresse);

        var recorder = new RecordingSendGmailApi
        {
            FailWith = new InvalidOperationException("L'accès Gmail a été révoqué côté Google. Reconnectez le compte.")
        };

        var exception = await AvecEmetteurAsync(recorder, adresse,
            async sender => await Assert.ThrowsAsync<EmailEnvoyeException>(
                () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>")));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task EstConfiguree_reflete_la_presence_de_l_adresse()
    {
        var recorder = new RecordingSendGmailApi();
        await _factory.WithScopeAsync(sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var configure = new GmailApiEmailSender(db, recorder, Config("chef@example.com"), NullLogger<GmailApiEmailSender>.Instance);
            var absent = new GmailApiEmailSender(db, recorder, Config("  "), NullLogger<GmailApiEmailSender>.Instance);
            Assert.True(configure.EstConfiguree);
            Assert.False(absent.EstConfiguree);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Systeme_sans_adresse_configuree_ne_touche_pas_Gmail()
    {
        var recorder = new RecordingSendGmailApi();

        await _factory.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var gmail = new GmailApiEmailSender(db, recorder, Config(null), NullLogger<GmailApiEmailSender>.Instance);
            var logging = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance, new EnvironnementDeTest());
            var sender = new SystemEmailSender(
                SmtpVide(), gmail, logging,
                OptionsSmtp(null), NullLogger<SystemEmailSender>.Instance);

            await sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>");
        });

        Assert.Empty(recorder.Sends);
    }

    // ══════════ Sélecteur : les 3 modes ══════════
    //
    // Ordre contractuel : Smtp__Host -> SMTP ; sinon Email:SenderGmailAddress ->
    // API Gmail ; sinon journalisation. Aucun appel réseau : chaque canal est doublé.

    private static SmtpEmailSender SmtpVide() =>
        new(OptionsSmtp(null), new FauxTransportSmtp(), NullLogger<SmtpEmailSender>.Instance);

    private static SmtpEmailSender SmtpConfigure(FauxTransportSmtp transport) =>
        new(OptionsSmtp(OptionsSmtpCompletes()), transport, NullLogger<SmtpEmailSender>.Instance);

    private static Microsoft.Extensions.Options.IOptions<SmtpOptions> OptionsSmtp(SmtpOptions? options) =>
        Microsoft.Extensions.Options.Options.Create(options ?? new SmtpOptions());

    private static SmtpOptions OptionsSmtpCompletes() => new()
    {
        Host = "smtp.gmail.com",
        Port = 587,
        User = "systeme@example.com",
        Password = "factice",
        From = "systeme@example.com",
        FromName = "SGT",
    };

    [Fact]
    public async Task Selection_smtp_prévaille_sur_l_API_Gmail_et_ne_l_appelle_meme_pas()
    {
        var recorder = new RecordingSendGmailApi();
        var transport = new FauxTransportSmtp();

        await _factory.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var sender = new SystemEmailSender(
                SmtpConfigure(transport),
                new GmailApiEmailSender(db, recorder, Config("chef@example.com"), NullLogger<GmailApiEmailSender>.Instance),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance, new EnvironnementDeTest()),
                OptionsSmtp(OptionsSmtpCompletes()),
                NullLogger<SystemEmailSender>.Instance);

            await sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>");
        });

        Assert.Single(transport.Envois);
        Assert.Empty(recorder.Sends);
    }

    [Fact]
    public async Task Selection_sans_smtp_utilise_l_API_Gmail()
    {
        var adresse = $"system-{Guid.NewGuid():N}@gmail.com";
        await CreerConnexionAsync(adresse);

        var recorder = new RecordingSendGmailApi();
        var transport = new FauxTransportSmtp();

        await _factory.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var sender = new SystemEmailSender(
                SmtpVide(),
                new GmailApiEmailSender(db, recorder, Config(adresse), NullLogger<GmailApiEmailSender>.Instance),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance, new EnvironnementDeTest()),
                OptionsSmtp(null),
                NullLogger<SystemEmailSender>.Instance);

            await sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>");
        });

        Assert.Single(recorder.Sends);
        Assert.Empty(transport.Envois);
    }

    [Fact]
    public async Task Selection_sans_smtp_ni_gmail_tombe_sur_la_journalisation()
    {
        var recorder = new RecordingSendGmailApi();
        var transport = new FauxTransportSmtp();

        await _factory.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var sender = new SystemEmailSender(
                SmtpVide(),
                new GmailApiEmailSender(db, recorder, Config(null), NullLogger<GmailApiEmailSender>.Instance),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance, new EnvironnementDeTest()),
                OptionsSmtp(null),
                NullLogger<SystemEmailSender>.Instance);

            // Ne doit lever : aucun émetteur réel n'est disponible, mais ce n'est
            // pas une raison de faire échouer le flux qui déclenche l'email.
            await sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>");
        });

        Assert.Empty(recorder.Sends);
        Assert.Empty(transport.Envois);
    }

    /// <summary>Faux SMTP en mémoire, même double que SmtpEmailSenderTests : aucun socket.</summary>
    private sealed class FauxTransportSmtp : ISmtpTransport
    {
        public List<(string To, string Subject, string HtmlBody)> Envois { get; } = new();

        public Task EnvoyerAsync(SmtpOptions options, string to, string subject, string htmlBody)
        {
            Envois.Add((to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class EnvironnementDeTest : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal sealed class RecordingSendGmailApi : IGmailApiService
{
    public sealed record SendCall(
        GmailConnection Connection, string To, string? Cc, string? Bcc, string? Subject,
        string? BodyText, string? BodyHtml, string? ThreadId, string? InReplyTo);

    public List<SendCall> Sends { get; } = new();

    /// <summary>Exception à lever à la place d'un envoi, pour tester la traduction en EmailEnvoyeException.</summary>
    public Exception? FailWith { get; set; }

    public Task<(string MessageId, string ThreadId)> SendMessageAsync(
        GmailConnection connection, string to, string? cc, string? bcc, string? subject,
        string? bodyText, string? bodyHtml, string? threadId, string? inReplyTo,
        IReadOnlyList<(string FileName, string MimeType, byte[] Content)> attachments)
    {
        if (FailWith is not null) throw FailWith;
        Sends.Add(new SendCall(connection, to, cc, bcc, subject, bodyText, bodyHtml, threadId, inReplyTo));
        return Task.FromResult(("message-1", "thread-1"));
    }

    private static NotSupportedException NonSimule() =>
        new("Opération Gmail non simulée : elle ne doit pas être appelée par ce test.");

    public Task<List<string>> ListMessageIdsAsync(GmailConnection c, string? q, int m = 30, string? p = null) => throw NonSimule();
    public Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(GmailConnection c, string? q, int m = 30, string? p = null) => throw NonSimule();
    public Task<GmailApiMessage> GetMessageAsync(GmailConnection c, string id) => throw NonSimule();
    public Task<string> CreateDraftAsync(GmailConnection c, string to, string subject, string body, string? threadId, string? inReplyTo, IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null) => throw NonSimule();
    public Task UpdateDraftAsync(GmailConnection c, string draftId, string to, string subject, string body, string? threadId, string? inReplyTo, IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null) => throw NonSimule();
    public Task<string> SendDraftAsync(GmailConnection c, string draftId) => throw NonSimule();
    public Task DeleteDraftAsync(GmailConnection c, string draftId) => throw NonSimule();
    public Task ModifyMessageLabelsAsync(GmailConnection c, string id, IReadOnlyCollection<string> a, IReadOnlyCollection<string> r) => throw NonSimule();
    public Task TrashMessageAsync(GmailConnection c, string id) => throw NonSimule();
    public Task ModifyThreadLabelsAsync(GmailConnection c, string id, IReadOnlyCollection<string> a, IReadOnlyCollection<string> r) => throw NonSimule();
    public Task TrashThreadAsync(GmailConnection c, string id) => throw NonSimule();
    public Task<(string FileName, string MimeType, byte[] Content)> GetAttachmentAsync(GmailConnection c, string messageId, string attachmentId) => throw NonSimule();
}
