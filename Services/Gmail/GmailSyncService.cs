using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Gmail;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    public interface IGmailSyncService
    {
        Task<GmailSyncResultDto> SyncAsync(GmailConnection connection, string? query, int maxResults, CancellationToken ct = default);
    }

    // Copie les métadonnées + corps des messages Gmail dans la base IMS (lecture Gmail -> tables).
    // Idempotent : un GmailMessageId déjà présent est ignoré, la synchronisation peut donc être
    // rejouée sans créer de doublon.
    public class GmailSyncService : IGmailSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGmailApiService _gmailApi;
        private readonly ILogger<GmailSyncService> _logger;

        private const int MaxMessages = 100;   // plafond par synchronisation
        private const int FetchConcurrency = 5; // appels Gmail simultanés (quota API : 250 unités/min)

        public GmailSyncService(
            ApplicationDbContext context,
            IGmailApiService gmailApi,
            ILogger<GmailSyncService> logger)
        {
            _context = context;
            _gmailApi = gmailApi;
            _logger = logger;
        }

        public async Task<GmailSyncResultDto> SyncAsync(GmailConnection connection, string? query, int maxResults, CancellationToken ct = default)
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

            if (remoteIds.Count == 0)
            {
                connection.LastSyncAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return new GmailSyncResultDto { MessagesRecus = 0, MessagesNouveaux = 0, LastSyncAt = connection.LastSyncAt };
            }

            // 2) Ne récupérer que le détail des messages inconnus de la base.
            var remoteIdSet = remoteIds.ToHashSet();
            var knownIds = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && remoteIdSet.Contains(m.GmailMessageId))
                .Select(m => m.GmailMessageId)
                .ToListAsync(ct);

            var knownSet = knownIds.ToHashSet();
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
                await _context.GmailMessages.AddRangeAsync(messages, ct);

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

        private static GmailMessage Map(GmailApiMessage source, int connectionId) => new()
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
            Snippet = Truncate(source.Snippet, 500),
            ReceivedAt = source.ReceivedAt,
            IsRead = source.IsRead,
            IsStarred = source.IsStarred,
            HasAttachments = source.HasAttachments,
            LabelsJson = JsonSerializer.Serialize(source.LabelIds),
            IsSynchronized = true,
            LastSyncedAt = DateTime.UtcNow
        };

        private static string? Truncate(string? value, int max) =>
            value != null && value.Length > max ? value[..max] : value;
    }
}
