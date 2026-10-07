namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Garde de démarrage du mode SMTP : si l'opérateur a choisi SMTP
    /// (<c>Smtp__Host</c> renseigné) mais a oublié une autre clé, l'API doit
    /// <b>échouer au démarrage</b> avec la liste des clés manquantes — pas
    /// démarrer puis échouer silencieusement à la première invitation, en
    /// renvoyant à l'administrateur un lien qu'aucun destinataire ne recevra.
    ///
    /// C'est le même principe que le garde du secret JWT
    /// (<c>Program.cs</c>, <c>JWT Secret is not configured</c>) et que le contrôle
    /// de longueur de la clé HMAC : on échoue tôt, avec la cause.
    ///
    /// Le garde ne s'applique QUE si le mode SMTP est choisi. Sinon — poste de
    /// dev, suite de tests, recette — l'application doit continuer à démarrer en
    /// se rabattant sur l'API Gmail puis sur la journalisation, comme avant.
    ///
    /// Les messages ne nomment que des VARIABLES D'ENVIRONNEMENT, jamais de valeur.
    /// </summary>
    public static class VerificationDemarrageSmtp
    {
        /// <summary>Lit la section Smtp puis vérifie. Retourne les options liées.</summary>
        /// <exception cref="InvalidOperationException">Mode SMTP choisi mais clé obligatoire manquante.</exception>
        public static SmtpOptions Verifier(IConfiguration configuration)
        {
            var options = configuration.GetSection(SmtpOptions.PrefixeConfiguration).Get<SmtpOptions>()
                          ?? new SmtpOptions();
            return Verifier(options);
        }

        /// <summary>Vérifie des options déjà construites. Variante directe, utilisée par les tests.</summary>
        /// <exception cref="InvalidOperationException">Mode SMTP choisi mais clé obligatoire manquante.</exception>
        public static SmtpOptions Verifier(SmtpOptions options)
        {
            if (!options.EstModeSmtp)
                return options;

            var manquantes = options.ChampsManquants();
            if (manquantes.Count == 0)
                return options;

            throw new InvalidOperationException(
                "SMTP configuration incomplete: Smtp__Host is set, so this application would send " +
                "system emails by SMTP, but the following environment variables are missing: " +
                string.Join(", ", manquantes) +
                ". Set them (see .env.example), or remove Smtp__Host to fall back to the Gmail API / logging.");
        }
    }
}
