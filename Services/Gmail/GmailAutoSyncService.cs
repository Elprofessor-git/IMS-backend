using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    /// <summary>
    /// Synchronise périodiquement toutes les connexions Gmail actives.
    /// <para>
    /// Deux protections : <see cref="GmailSyncGate"/>, verrou partagé par connexion, empêche
    /// deux passages — ou un passage et une synchronisation manuelle — de se marcher dessus sur
    /// la même boîte, sans quoi deux exécutions liraient les mêmes messages et doubleraient la
    /// base ; et chaque passage crée son propre <c>DbContext</c> scopé, car un
    /// <see cref="BackgroundService"/> vit plus longtemps que le scope injecté.
    /// </para>
    /// </summary>
    public class GmailAutoSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GmailAutoSyncOptions _options;
        private readonly ILogger<GmailAutoSyncService> _logger;

        public GmailAutoSyncService(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Options.IOptions<GmailAutoSyncOptions> options,
            ILogger<GmailAutoSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Synchronisation automatique Gmail désactivée (Gmail:AutoSync:Enabled=false).");
                return;
            }

            var interval = TimeSpan.FromMinutes(_options.ResolvedIntervalMinutes);
            _logger.LogInformation(
                "Synchronisation automatique Gmail démarrée : toutes les {Interval} minute(s), {Max} message(s) par connexion.",
                _options.ResolvedIntervalMinutes, _options.ResolvedMaxResults);

            // Premier passage légèrement décalé : au démarrage de l'application, tous les
            // clients lancent en même temps, et inonder l'API Gmail au démarrage est inutile.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    await SyncAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Une erreur globale ne doit pas tuer le service de fond : sinon un
                    // incident passager (Postgres indisponible au démarrage) désactiverait
                    // définitivement la synchronisation jusqu'au redémarrage suivant.
                    _logger.LogError(ex, "Passage de synchronisation automatique Gmail interrompu par une erreur.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task SyncAllAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var connectionIds = await context.GmailConnections
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (connectionIds.Count == 0) return;

            using var throttle = new SemaphoreSlim(_options.ResolvedMaxParallelConnections);
            var tasks = connectionIds.Select(async id =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    await SyncOneAsync(id, ct);
                }
                finally
                {
                    throttle.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task SyncOneAsync(int connectionId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sync = scope.ServiceProvider.GetRequiredService<IGmailSyncService>();

            var connection = await context.GmailConnections
                .FirstOrDefaultAsync(c => c.Id == connectionId && c.IsActive, ct);

            if (connection == null) return;

            try
            {
                // Attente nulle : si une synchronisation est déjà en cours sur cette boîte —
                // typiquement une synchronisation manuelle lancée par l'utilisateur — on saute
                // ce passage. Le verrou lui-même est détenu par GmailSyncService, ce qui couvre
                // les DEUX chemins ; un dictionnaire propre au service de fond ne couvrirait
                // que le fond et laisserait une manuelle doublonner l'autre.
                var result = await sync.SyncAsync(
                    connection, query: null, _options.ResolvedMaxResults, ct, gateWait: TimeSpan.Zero);

                if (result.MessagesNouveaux > 0)
                {
                    _logger.LogInformation(
                        "Synchronisation automatique : {Nouveaux} nouveau(x) email(s) pour la connexion {ConnectionId}.",
                        result.MessagesNouveaux, connectionId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Isolation par connexion : un token expiré sur un compte ne doit pas
                // empêcher les autres boîtes de se synchroniser.
                _logger.LogWarning(ex, "Synchronisation automatique Gmail impossible pour la connexion {ConnectionId}.", connectionId);
            }
        }
    }
}
