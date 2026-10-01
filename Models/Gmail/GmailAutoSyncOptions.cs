namespace Backend_Gestion_Magasin_API.Models.Gmail
{
    /// <summary>
    /// Réglages de la synchronisation automatique, lus dans la section de configuration
    /// « Gmail:AutoSync » (appsettings.json / variables d'environnement).
    /// </summary>
    public class GmailAutoSyncOptions
    {
        public const string SectionName = "Gmail:AutoSync";

        /// <summary>Active le service de fond. Passé à false, seule la sync manuelle reste possible.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Minutes entre deux passages du service de fond.</summary>
        public int IntervalMinutes { get; set; } = 5;

        /// <summary>Messages rapatriés par connexion et par passage.</summary>
        public int MaxResults { get; set; } = 50;

        /// <summary>Connexions traitées en parallèle (les quotas Gmail sont par compte).</summary>
        public int MaxParallelConnections { get; set; } = 2;

        public int ResolvedIntervalMinutes =>
            Math.Clamp(IntervalMinutes, 1, 24 * 60);

        public int ResolvedMaxResults =>
            Math.Clamp(MaxResults, 1, 100);

        public int ResolvedMaxParallelConnections =>
            Math.Clamp(MaxParallelConnections, 1, 10);
    }
}
