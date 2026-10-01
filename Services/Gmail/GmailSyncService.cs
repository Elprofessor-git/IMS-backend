using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Gmail;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    public interface IGmailSyncService
    {
        /// <summary>
        /// <paramref name="gateWait"/> : combien de temps attendre si une synchronisation est
        /// déjà en cours sur la même boîte. <see cref="TimeSpan.Zero"/> = ne pas attendre et
        /// rendre un résultat vide si la boîte est occupée (service de fond).
        /// </summary>
        Task<GmailSyncResultDto> SyncAsync(
            GmailConnection connection, string? query, int maxResults,
            CancellationToken ct = default, TimeSpan? gateWait = null);
    }

    // Copie les métadonnées + corps des messages Gmail dans la base IMS (lecture Gmail -> tables).
    // Idempotent : un GmailMessageId déjà présent est ignoré, la synchronisation peut donc être
    // rejouée sans créer de doublon.
    public class GmailSyncService : IGmailSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGmailApiService _gmailApi;
        private readonly IGmailSyncGate _gate;
        private readonly ILogger<GmailSyncService> _logger;

        private const int MaxMessages = 100;   // plafond par synchronisation
        private const int FetchConcurrency = 5; // appels Gmail simultanés (quota API : 250 unités/min)

        // Attente par défaut quand l'appelant ne précise rien (synchronisation manuelle) :
        // assez longue pour laisser finir un passage automatique en cours, et renvoyer une
        // erreur lisible plutôt que de_le laisser croire à un échec Gmail.
        private static readonly TimeSpan DefaultGateWait = TimeSpan.FromSeconds(60);

        public GmailSyncService(
            ApplicationDbContext context,
            IGmailApiService gmailApi,
            IGmailSyncGate gate,
            ILogger<GmailSyncService> logger)
        {
            _context = context;
            _gmailApi = gmailApi;
            _gate = gate;
            _logger = logger;
        }

        public async Task<GmailSyncResultDto> SyncAsync(
            GmailConnection connection, string? query, int maxResults,
            CancellationToken ct = default, TimeSpan? gateWait = null)
        {
            var wait = gateWait ?? DefaultGateWait;
            var releaser = await _gate.TryAcquireAsync(connection.Id, wait, ct);
            if (releaser == null)
            {
                // Boîte déjà en cours de synchronisation. Avec une attente nulle c'est le
                // service de fond qui préfère sauter son passage ; avec une attente longue
                // l'attente a expiré, et l'appelant reçoit un résultat vide explicite.
                _logger.LogDebug(
                    "Synchronisation Gmail ignorée pour la connexion {ConnectionId} : une synchronisation est déjà en cours.",
                    connection.Id);
                return new GmailSyncResultDto { MessagesRecus = 0, MessagesNouveaux = 0, LastSyncAt = connection.LastSyncAt };
            }

            // Le verrou est tenu jusqu'à la fin : c'est lui qui empêche deux exécutions de
            // lire les mêmes messages et d'insérer deux fois les mêmes lignes.
            await using (releaser)
            {
                return await RunSyncAsync(connection, query, maxResults, ct);
            }
        }

        private async Task<GmailSyncResultDto> RunSyncAsync(
            GmailConnection connection, string? query, int maxResults, CancellationToken ct)
        {
            var capped = Math.Clamp(maxResults, 1, MaxMessages);

            // 1) Lister les identifiants Gmail (pagination par lots de 25, comme l'API le recommande).
            var remoteIds = new List<string>();
            string? pageToken = null;
            do
            {
                var (ids, next) = await _gmailApi.ListMessageIdsPageAsync(connection, query, 25, pageToken);
                remoteIds.AddRange(ids);
                pageToken = next;

                if (remoteIds.Count >= capped) break;
            }
            while (!string.IsNullOrEmpty(pageToken));

            if (remoteIds.Count > capped)
                remoteIds = remoteIds.Take(capped).ToList();

            // 2) Identifier ce que l'IMS connaît DÉJÀ, avant tout appel de détail : c'est ce
            //    qui évite de re-télécharger des centaines de messages à chaque passage.
            var knownIds = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && remoteIds.Contains(m.GmailMessageId))
                .Select(m => m.GmailMessageId)
                .ToListAsync(ct);
            var knownSet = knownIds.ToHashSet();

            // Une synchronisation pilotée par une recherche se comporte comme les autres :
            // Gmail a renvoyé les identifiants, on importe ceux que l'IMS ne connaît pas.
            // Aucun recoupement local n'est appliqué — il rendrait la recherche incapable
            // d'importer quoi que ce soit de neuf, puisque le recoupement ne peut porter
            // que sur des messages déjà en base. Le risque de réinjection n'existe pas :
            // `toFetch` exclut par construction tout GmailMessageId déjà connu, et
            // l'insertion re-vérifie l'unicité juste avant.

            if (remoteIds.Count == 0)
            {
                connection.LastSyncAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return new GmailSyncResultDto { MessagesRecus = 0, MessagesNouveaux = 0, LastSyncAt = connection.LastSyncAt };
            }

            // 3) Ne récupérer que le détail des messages inconnus de la base.
            var toFetch = remoteIds.Where(id => !knownSet.Contains(id)).ToList();

            var fetched = await FetchDetailsAsync(connection, toFetch, ct);

            // 3) Persistance. Un message peut avoir disparu côté Gmail entre le list et le get (404)
            //    → on l'ignore silencieusement pour ne pas faire échouer toute la synchronisation.
            var messages = new List<GmailMessage>(fetched.Count);
            foreach (var apiMessage in fetched)
            {
                if (apiMessage == null) continue;

                var existing = await _context.GmailMessages
                    .FirstOrDefaultAsync(m => m.GmailConnectionId == connection.Id && m.GmailMessageId == apiMessage.Id, ct);
                if (existing != null) continue;

                messages.Add(Map(apiMessage, connection.Id));
            }

            if (messages.Count > 0)
            {
                await _context.GmailMessages.AddRangeAsync(messages, ct);
                await _context.SaveChangesAsync(ct);

                // Les src="cid:" du HTML ne sont réécrits qu'une fois l'Id IMS connu :
                // l'URL du proxy porte cet Id. L'assainissement précède la réécriture,
                // pour que le rewrite ne puisse pas réintroduire quoi que ce soit.
                var attachments = new List<GmailAttachment>();
                foreach (var message in messages)
                {
                    message.BodyHtml = GmailInlineImageRewriter.Rewrite(message.BodyHtml, message.Id);
                    message.BodyHtml = HtmlSanitizer.Sanitize(message.BodyHtml);

                    foreach (var part in message._PendingAttachments)
                    {
                        attachments.Add(new GmailAttachment
                        {
                            GmailMessageId = message.Id,
                            GmailAttachmentId = part.GmailAttachmentId,
                            FileName = part.FileName,
                            MimeType = part.MimeType,
                            SizeBytes = part.SizeBytes,
                            IsInline = part.IsInline,
                            ContentId = part.ContentId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (attachments.Count > 0)
                    await _context.GmailAttachments.AddRangeAsync(attachments, ct);
            }

            connection.LastSyncAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Synchronisation Gmail : {Recus} message(s) listés, {Nouveaux} nouveau(x) enregistrés pour la connexion {ConnectionId}.",
                remoteIds.Count, messages.Count, connection.Id);

            return new GmailSyncResultDto
            {
                MessagesRecus = remoteIds.Count,
                MessagesNouveaux = messages.Count,
                MessagesMisAJour = 0,
                LastSyncAt = connection.LastSyncAt
            };
        }

        private async Task<List<GmailApiMessage?>> FetchDetailsAsync(GmailConnection connection, List<string> ids, CancellationToken ct)
        {
            var results = new GmailApiMessage?[ids.Count];
            using var semaphore = new SemaphoreSlim(FetchConcurrency);

            var tasks = ids.Select(async (id, index) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    results[index] = await _gmailApi.GetMessageAsync(connection, id);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Un message supprimé/inal accessible ne doit pas casser la synchronisation globale.
                    _logger.LogWarning(ex, "Message Gmail {MessageId} illisible, ignoré.", id);
                    results[index] = null;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.ToList();
        }

        private static GmailMessage Map(GmailApiMessage source, int connectionId)
        {
            var message = new GmailMessage
            {
                GmailConnectionId = connectionId,
                GmailMessageId = source.Id,
                GmailThreadId = source.ThreadId,
                From = Truncate(source.From, 255),
                To = Truncate(source.To, 255),
                Cc = Truncate(source.Cc, 255),
                Subject = Truncate(source.Subject, 500),
                Rfc822MessageId = Truncate(source.Rfc822MessageId, 255),
                BodyText = source.BodyText,
                BodyHtml = source.BodyHtml,
                Snippet = Truncate(source.Snippet, 500),
                ReceivedAt = source.ReceivedAt,
                IsRead = source.IsRead,
                IsStarred = source.IsStarred,
                HasAttachments = source.HasAttachments,
                LabelsJson = JsonSerializer.Serialize(source.LabelIds),
                IsSynchronized = true,
                LastSyncedAt = DateTime.UtcNow
            };

            // Les pièces ne sont pas encore persistables : l'Id IMS du message n'est connu
            // qu'après le SaveChanges. On les transporte le temps de l'affectation.
            message._PendingAttachments = source.Attachments;
            return message;
        }

        private static string? Truncate(string? value, int max) =>
            value != null && value.Length > max ? value[..max] : value;
    }
}
