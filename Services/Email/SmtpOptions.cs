namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Configuration des emails système envoyés par SMTP (section <c>Smtp</c>).
    ///
    /// Variable d'environnement associée (jamais commitée) :
    /// <c>Smtp__Host</c>, <c>Smtp__Port</c>, <c>Smtp__User</c>, <c>Smtp__Password</c>,
    /// <c>Smtp__From</c>, <c>Smtp__FromName</c>.
    ///
    /// Pourquoi SMTP plutôt que l'API Gmail (OAuth) pour l'Auth : l'API impose un
    /// écran de consentement, un branding et un refresh token expirant en mode Test.
    /// SMTP ne demande qu'un mot de passe d'application. Note : Render bloque les
    /// ports 25/465/587 sur les instances gratuites (changelog du 16/09/2025) ; 465
    /// et 587 fonctionnent sur les instances payantes. Le mode est donc CHOISI par
    /// configuration (voir <see cref="SystemEmailSender"/>), jamais imposé : si
    /// <c>Smtp__Host</c> est absent, l'application garde le chemin API Gmail.
    ///
    /// Le mot de passe n'est jamais journalisé, ni dans <see cref="ToString"/>, ni
    /// nulle part ailleurs : cette classe ne redéfinit pas <c>ToString</c> et aucun
    /// appelant n'interpole les options dans un message.
    /// </summary>
    public sealed class SmtpOptions
    {
        /// <summary>Nom de la section IConfiguration. « Smtp » → variables d'environnement « Smtp__* ».</summary>
        public const string PrefixeConfiguration = "Smtp";

        /// <summary>Hôte SMTP. Sa présence est ce qui CHOISIT le mode SMTP (sinon : API Gmail, puis journalisation).</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Port SMTP. 587 = soumission avec STARTTLS, la valeur à poser (voir .env.example).
        ///
        /// PAS de valeur par défaut ici, et c'est volontaire : si le constructeur
        /// posait 587, l'opérateur qui omet <c>Smtp__Port</c> ne serait jamais signalé
        /// par la garde de démarrage — qui ne verrait qu'une valeur valide. Le défaut
        /// vit donc dans .env.example, pas dans le code, pour que « la variable est
        /// absente » et « la variable vaut 587 » restent distinguables.
        /// </summary>
        public int Port { get; set; }

        /// <summary>Identifiant d'authentification — pour Gmail, l'adresse complète.</summary>
        public string User { get; set; } = string.Empty;

        /// <summary>Mot de passe d'application (jamais le mot de passe du compte Google).</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Adresse d'expéditeur. Doit être celle du compte <c>User</c>, sinon Gmail refuse.</summary>
        public string From { get; set; } = string.Empty;

        /// <summary>Nom d'affichage de l'expéditeur.</summary>
        public string FromName { get; set; } = string.Empty;

        /// <summary>Vrai si la configuration demande explicitement le mode SMTP.</summary>
        public bool EstModeSmtp => !string.IsNullOrWhiteSpace(Host);

        /// <summary>Vrai si TOUTES les clés nécessaires à un envoi réel sont présentes.</summary>
        public bool EstComplete =>
            EstModeSmtp
            && Port > 0
            && User.Length > 0
            && Password.Length > 0
            && From.Length > 0
            && FromName.Length > 0;

        /// <summary>
        /// Liste les clés manquantes, en nommant la variable d'environnement à poser.
        /// N'inclut <b>jamais</b> de valeur : seulement des noms de clés.
        /// </summary>
        public IReadOnlyList<string> ChampsManquants()
        {
            var manquantes = new List<string>(6);
            if (!EstModeSmtp) return manquantes;
            if (Port <= 0) manquantes.Add($"{PrefixeConfiguration}__Port");
            if (User.Length == 0) manquantes.Add($"{PrefixeConfiguration}__User");
            if (Password.Length == 0) manquantes.Add($"{PrefixeConfiguration}__Password");
            if (From.Length == 0) manquantes.Add($"{PrefixeConfiguration}__From");
            if (FromName.Length == 0) manquantes.Add($"{PrefixeConfiguration}__FromName");
            return manquantes;
        }
    }
}
