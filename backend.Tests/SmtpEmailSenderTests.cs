using Backend_Gestion_Magasin_API.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Tests;

/// <summary>
/// <see cref="SmtpEmailSender"/> (émetteur) et <see cref="VerificationDemarrageSmtp"/>
/// (garde de démarrage) : logique pure sur un faux transport <b>en mémoire</b>.
/// Aucune connexion réseau n'est ouverte — c'est <see cref="ISmtpTransport"/> qui
/// est doublé, donc le code MailKit seul responsable d'un socket n'est jamais
/// appelé par la suite.
/// </summary>
public class SmtpEmailSenderTests
{
    private static SmtpOptions OptionsComplet() => new()
    {
        Host = "smtp.gmail.com",
        Port = 587,
        User = "systeme@example.com",
        Password = "mot-de-passe-d-application-factice",
        From = "systeme@example.com",
        FromName = "Système de Gestion Textile",
    };

    private static SmtpEmailSender Emetteur(ISmtpTransport transport, SmtpOptions options, JournalCapturant<SmtpEmailSender>? journal = null)
        => new(
            Microsoft.Extensions.Options.Options.Create(options),
            transport,
            journal ?? new JournalCapturant<SmtpEmailSender>());

    // ══════════ 1. Envoi réel sur transport simulé ══════════

    [Fact]
    public async Task Envoi_appelle_le_transport_une_fois_avec_destinataire_objet_et_corps()
    {
        var transport = new FauxTransportSmtp();
        var sender = Emetteur(transport, OptionsComplet());

        await sender.SendAsync("destinataire@example.com", "Votre accès", "<p>Choisissez votre mot de passe</p>");

        var envoi = Assert.Single(transport.Envois);
        Assert.Equal("destinataire@example.com", envoi.To);
        Assert.Equal("Votre accès", envoi.Subject);
        Assert.Equal("<p>Choisissez votre mot de passe</p>", envoi.HtmlBody);
        Assert.Equal("smtp.gmail.com", envoi.Options.Host);
        Assert.Equal(587, envoi.Options.Port);
    }

    [Fact]
    public async Task EstConfigure_reflete_la_completude_de_la_section_smtp()
    {
        var complet = Emetteur(new FauxTransportSmtp(), OptionsComplet());
        Assert.True(complet.EstConfigure);

        var sansHote = OptionsComplet();
        sansHote.Host = "  ";
        Assert.False(Emetteur(new FauxTransportSmtp(), sansHote).EstConfigure);
    }

    // ══════════ 2. Échec d'envoi ══════════

    [Fact]
    public async Task Echec_du_transport_est_emballe_en_EmailEnvoyeException()
    {
        var transport = new FauxTransportSmtp
        {
            FailWith = new InvalidOperationException("535 Authentication failed"),
        };
        var sender = Emetteur(transport, OptionsComplet());

        var ex = await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("smtp.gmail.com:587", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Echec_du_transport_ne_contient_ni_corps_ni_jeton()
    {
        var transport = new FauxTransportSmtp
        {
            FailWith = new InvalidOperationException("boom"),
        };
        var sender = Emetteur(transport, OptionsComplet());
        const string corpsAvecJeton = "<a href=\"https://app/reset-password?token=SECRET-JETON\">Choisir mon mot de passe</a>";

        var ex = await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", corpsAvecJeton));

        // L'exception voyage vers les appelsants puis vers un handler d'erreur :
        // elle ne doit pas véhiculer le lien.
        Assert.DoesNotContain("SECRET-JETON", ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("reset-password", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_echec_quelconque_ne_produit_pas_le_message_ports_bloques()
    {
        var journal = new JournalCapturant<SmtpEmailSender>();
        var transport = new FauxTransportSmtp
        {
            FailWith = new InvalidOperationException("535 Authentication failed"),
        };
        var sender = Emetteur(transport, OptionsComplet(), journal);

        await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.DoesNotContain(
            journal.Messages,
            m => m.Contains(SmtpEmailSender.MessageInjoignable, StringComparison.Ordinal));
    }

    // ══════════ 3. Timeout → diagnostic « ports bloqués » ══════════

    [Theory]
    [InlineData("The operation has timed out.")]
    [InlineData("Connection timed out while waiting for a response.")]
    public async Task Timeout_est_signale_par_le_message_sur_ports_bloques(string cause)
    {
        var journal = new JournalCapturant<SmtpEmailSender>();
        var transport = new FauxTransportSmtp { FailWith = new System.IO.IOException(cause) };
        var sender = Emetteur(transport, OptionsComplet(), journal);

        await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.Contains(
            journal.Messages,
            m => m.Contains(SmtpEmailSender.MessageInjoignable, StringComparison.Ordinal));
        // Le message d'incident pointe l'hôte, mais jamais le mot de passe.
        Assert.Contains(
            journal.Messages,
            m => m.Contains("smtp.gmail.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_inner_Timeout_est_aussi_detecte()
    {
        var journal = new JournalCapturant<SmtpEmailSender>();
        var transport = new FauxTransportSmtp
        {
            FailWith = new MailKit.ServiceNotConnectedException(
                "The Smtp client is not connected.",
                new OperationCanceledException("The operation was canceled.")),
        };
        var sender = Emetteur(transport, OptionsComplet(), journal);

        await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.Contains(
            journal.Messages,
            m => m.Contains(SmtpEmailSender.MessageInjoignable, StringComparison.Ordinal));
    }

    // ══════════ 4. Configuration ══════════

    [Fact]
    public async Task Configuration_incomplete_leve_sans_meme_appeler_le_transport()
    {
        var transport = new FauxTransportSmtp();
        var options = OptionsComplet();
        options.Password = "";
        var sender = Emetteur(transport, options);

        var ex = await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>x</p>"));

        Assert.Empty(transport.Envois);
        // Le nom de variable d'environnement est utile ; la valeur, non.
        Assert.Contains("Smtp__Password", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("mot-de-passe-d-application-factice", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_mot_de_passe_n_apparait_dans_aucun_message_de_journal()
    {
        var journal = new JournalCapturant<SmtpEmailSender>();
        var transport = new FauxTransportSmtp();
        var sender = Emetteur(transport, OptionsComplet(), journal);

        await sender.SendAsync("destinataire@example.com", "Objet", "<p>corps</p>");
        transport.FailWith = new InvalidOperationException("erreur");
        await Assert.ThrowsAsync<EmailEnvoyeException>(
            () => sender.SendAsync("destinataire@example.com", "Objet", "<p>corps</p>"));

        Assert.NotEmpty(journal.Messages);
        Assert.All(journal.Messages, m =>
        {
            Assert.DoesNotContain("mot-de-passe-d-application-factice", m, StringComparison.Ordinal);
            Assert.DoesNotContain("<p>corps</p>", m, StringComparison.Ordinal);
        });
    }

    // ══════════ 5. Garde de démarrage ══════════

    private static IConfiguration Config(IDictionary<string, string?> valeurs) =>
        new ConfigurationBuilder().AddInMemoryCollection(valeurs).Build();

    [Fact]
    public void Demarrage_refuse_si_le_mode_smtp_est_choisi_mais_qu_une_clé_manque()
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:User"] = "systeme@example.com",
            // Smtp:Password, Smtp:From, Smtp:FromName volontairement absents.
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => VerificationDemarrageSmtp.Verifier(config));

        Assert.Contains("Smtp__Password", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp__From", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp__FromName", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demarrage_nomme_toutes_les_variables_absentes_en_une_seule_fois()
    {
        var config = Config(new Dictionary<string, string?> { ["Smtp:Host"] = "smtp.gmail.com" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => VerificationDemarrageSmtp.Verifier(config));

        foreach (var cle in new[] { "Smtp__Port", "Smtp__User", "Smtp__Password", "Smtp__From", "Smtp__FromName" })
            Assert.Contains(cle, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demarrage_accepte_une_section_smtp_complete()
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:User"] = "systeme@example.com",
            ["Smtp:Password"] = "fake",
            ["Smtp:From"] = "systeme@example.com",
            ["Smtp:FromName"] = "SGT",
        });

        var options = VerificationDemarrageSmtp.Verifier(config);

        Assert.True(options.EstModeSmtp);
        Assert.True(options.EstComplete);
        Assert.Empty(options.ChampsManquants());
    }

    [Fact]
    public void Demarrage_n_impose_rien_quand_le_mode_smtp_n_est_pas_choisi()
    {
        // Ni host, ni rien d'autre : l'application doit tomber sur l'API Gmail ou la
        // journalisation et DÉMARRER. C'est le cas de la suite de tests et d'un
        // poste de dev.
        var options = VerificationDemarrageSmtp.Verifier(Config(new Dictionary<string, string?>()));

        Assert.False(options.EstModeSmtp);
        Assert.False(options.EstComplete);
        Assert.Empty(options.ChampsManquants());
    }

    [Fact]
    public void Demarrage_n_expose_que_des_noms_de_variables_et_aucune_valeur()
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Password"] = "mot-de-passe-d-application-factice",
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => VerificationDemarrageSmtp.Verifier(config));

        Assert.DoesNotContain("mot-de-passe-d-application-factice", ex.Message, StringComparison.Ordinal);
        // Le nom du fichier de gabarit indique où poser les clés.
        Assert.Contains(".env.example", ex.Message, StringComparison.Ordinal);
    }

    // ══════════ Doubles ══════════

    /// <summary>Faux SMTP en mémoire : enregistre les envois, n'ouvre aucun socket.</summary>
    private sealed class FauxTransportSmtp : ISmtpTransport
    {
        public sealed record Envoi(SmtpOptions Options, string To, string Subject, string HtmlBody);

        public List<Envoi> Envois { get; } = new();

        /// <summary>Exception à lever à la place de l'envoi.</summary>
        public Exception? FailWith { get; set; }

        public Task EnvoyerAsync(SmtpOptions options, string to, string subject, string htmlBody)
        {
            if (FailWith is not null) throw FailWith;
            Envois.Add(new Envoi(options, to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    /// <summary>Capture les messages formatés pour vérifier ce qui n'y figure jamais.</summary>
    private sealed class JournalCapturant<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null) Messages.Add(exception.ToString());
        }
    }
}
