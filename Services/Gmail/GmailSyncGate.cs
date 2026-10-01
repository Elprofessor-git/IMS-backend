using System.Collections.Concurrent;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    /// <summary>
    /// Verrou d'exclusion mutuelle par connexion Gmail, partagé entre TOUS les appelants de
    /// la synchronisation.
    /// <para>
    /// L'instanciation est <b>singleton</b> : c'est la seule façon d'y arriver, puisque
    /// <c>IGmailSyncService</c> est scoped et que le service de fond vit plus longtemps que
    /// le scope injecté. Un dictionnaire détenu par l'un ou l'autre ne partagerait rien et
    /// laisserait passer les collisions qu'il prétend éviter.
    /// </para>
    /// </summary>
    public interface IGmailSyncGate
    {
        /// <summary>
        /// Tente de prendre le verrou d'une connexion.
        /// <para>
        /// Une attente de <see cref="TimeSpan.Zero"/> signifie « ne pas attendre » : l'appelant
        /// reçoit <c>null</c> immédiatement si une synchronisation est déjà en cours. C'est le
        /// comportement du service de fond, qui préfère sauter son passage plutôt que de
        /// retarder le timer. Une attente positive (synchronisation manuelle) fait la queue.
        /// </para>
        /// </summary>
        Task<IAsyncDisposable?> TryAcquireAsync(int connectionId, TimeSpan wait, CancellationToken ct);
    }

    public class GmailSyncGate : IGmailSyncGate
    {
        // Un verrou par connexion : la clé est l'Id de GmailConnection, pas l'utilisateur.
        // Deux boîtes du même utilisateur n'ont pas à s'attendre mutuellement.
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

        public async Task<IAsyncDisposable?> TryAcquireAsync(int connectionId, TimeSpan wait, CancellationToken ct)
        {
            var gate = _gates.GetOrAdd(connectionId, _ => new SemaphoreSlim(1, 1));

            if (!await gate.WaitAsync(wait, ct))
                return null;

            return new Releaser(gate);
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private readonly SemaphoreSlim _gate;
            private int _released;

            public Releaser(SemaphoreSlim gate) => _gate = gate;

            public ValueTask DisposeAsync()
            {
                // Dispose peut être appelé deux fois (using + finally) : libérer un
                // SemaphoreSlim déjà libre ferait exploser le compteur pour les autres.
                if (Interlocked.Exchange(ref _released, 1) == 0)
                    _gate.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
