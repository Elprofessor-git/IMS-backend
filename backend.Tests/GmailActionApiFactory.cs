using System.Collections.Concurrent;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backend.Tests;

/// <summary>
/// Enregistre les appels d'étiquettes que l'API émulées reçoit, sans jamais sortir sur
/// le réseau.
/// <para>
/// Le vrai <c>GmailApiService</c>-appellerait <c>gmail.googleapis.com</c> avec un jeton de
/// test : la requête échouerait (502) pour une raison étrangère à ce qu'on veut vérifier,
/// et le test ne pourrait plus distinguer « l'action est correcte » de « l'action a
/// seulement été tentée ».
/// </para>
/// </summary>
public sealed class RecordingGmailApiService : IGmailApiService
{
    public sealed record ModifyCall(string MessageId, string[] Added, string[] Removed);

    public sealed record TrashCall(string MessageId);

    public ConcurrentQueue<ModifyCall> Modifies { get; } = new();
    public ConcurrentQueue<TrashCall> Trashes { get; } = new();

    /// <summary>Fil plutôt que message : les appels au niveau conversation.</summary>
    public ConcurrentQueue<ModifyCall> ThreadModifies { get; } = new();
    public ConcurrentQueue<TrashCall> ThreadTrashes { get; } = new();

    /// <summary>Message ou fil qui doit faire échouer l'appel, pour tester le 502.</summary>
    public string? FailingMessageId { get; set; }

    public Task ModifyMessageLabelsAsync(
        GmailConnection connection, string gmailMessageId,
        IReadOnlyCollection<string> addLabelIds, IReadOnlyCollection<string> removeLabelIds)
    {
        if (gmailMessageId == FailingMessageId)
            throw new InvalidOperationException("Erreur Gmail simulée sur messages.modify.");

        Modifies.Enqueue(new ModifyCall(gmailMessageId, addLabelIds.ToArray(), removeLabelIds.ToArray()));
        return Task.CompletedTask;
    }

    public Task TrashMessageAsync(GmailConnection connection, string gmailMessageId)
    {
        if (gmailMessageId == FailingMessageId)
            throw new InvalidOperationException("Erreur Gmail simulée sur messages.trash.");

        Trashes.Enqueue(new TrashCall(gmailMessageId));
        return Task.CompletedTask;
    }

    public Task ModifyThreadLabelsAsync(
        GmailConnection connection, string gmailThreadId,
        IReadOnlyCollection<string> addLabelIds, IReadOnlyCollection<string> removeLabelIds)
    {
        if (gmailThreadId == FailingMessageId)
            throw new InvalidOperationException("Erreur Gmail simulée sur threads.modify.");

        ThreadModifies.Enqueue(new ModifyCall(gmailThreadId, addLabelIds.ToArray(), removeLabelIds.ToArray()));
        return Task.CompletedTask;
    }

    public Task TrashThreadAsync(GmailConnection connection, string gmailThreadId)
    {
        if (gmailThreadId == FailingMessageId)
            throw new InvalidOperationException("Erreur Gmail simulée sur threads.trash.");

        ThreadTrashes.Enqueue(new TrashCall(gmailThreadId));
        return Task.CompletedTask;
    }

    // Les autres opérations ne sont pas concernées par ces tests : on les fait échouer
    // explicitement plutôt que de laisser une appel vers Gmail se faire par surprise.
    private static InvalidOperationException Unexpected() =>
        new("Opération Gmail non simulée : elle ne doit pas être appelée par ce test.");

    public Task<List<string>> ListMessageIdsAsync(GmailConnection c, string? q, int m = 30, string? p = null) =>
        throw Unexpected();
    // Qualification A2 et rattrapage A2b : la maintenance relit le message ENTIER chez
    // Gmail (c'est la seule façon de savoir quels cid: le corps référence). Le double sert
    // donc des messages complets, déjà qualifiés, et compte les lectures pour prouver qu'un
    // appel refusé ne consomme aucun quota.
    public ConcurrentDictionary<string, GmailApiMessage> MessageCatalog { get; } = new();
    public ConcurrentQueue<string> MessageListings { get; } = new();

    // Rattrapage A2b : l'endpoint recense d'abord les messages que Gmail déclare porteurs
    // d'une pièce jointe (« has:attachment »), parce que recenser coûte 5 unités pour 100
    // identifiants quand une lecture complète en coûte 5 pour un seul message.
    public ConcurrentDictionary<string, List<string>> ListedMessageIds { get; } = new();
    public ConcurrentQueue<string> Searches { get; } = new();

    /// <summary>Requêtes « messages.list » qui doivent échouer (quota, réseau).</summary>
    public ConcurrentBag<string> FailingSearchQueries { get; } = new();

    /// <summary>Identifiants dont la lecture doit échouer (message supprimé, quota, réseau).</summary>
    public ConcurrentBag<string> FailingMessageIds { get; } = new();

    public Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(
        GmailConnection c, string? q, int m = 30, string? p = null)
    {
        var query = string.IsNullOrWhiteSpace(q) ? "in:inbox" : q!;
        Searches.Enqueue(query);
        if (FailingSearchQueries.Contains(query))
            throw new InvalidOperationException($"Erreur Gmail simulée sur messages.list({query}).");

        // Une seule page : le double n'a pas besoin de simuler une boîte de plusieurs
        // milliers de messages, et les tests n'ont jamais plus de quelques dizaines de messages.
        var ids = ListedMessageIds.TryGetValue(query, out var known) ? known : new List<string>();
        return Task.FromResult((ids, (string?)null));
    }

    /// <summary>Déclare les identifiants qu'une recherche Gmail doit renvoyer.</summary>
    public void DeclareSearchResults(string query, params string[] ids) =>
        ListedMessageIds[query] = ids.ToList();

    /// <summary>
    /// Oublie les pannes simulées. Les tests d'une classe partagent le même double : une
    /// panne laissée active fausserait silencieusement le test suivant, qui lirait
    /// « aucun candidat » alors qu'il a lui-même préparé son catalogue.
    /// </summary>
    public void ResetSimulatedFailures()
    {
        while (FailingSearchQueries.TryTake(out _)) { }
        while (FailingMessageIds.TryTake(out _)) { }
    }

    public Task<GmailApiMessage> GetMessageAsync(GmailConnection c, string id)
    {
        MessageListings.Enqueue(id);
        if (FailingMessageIds.Contains(id))
            throw new InvalidOperationException($"Erreur Gmail simulée sur messages.get({id}).");

        return Task.FromResult(
            MessageCatalog.TryGetValue(id, out var message) ? message : MessageSansPiece(id));
    }

    /// <summary>Message sans corps ni pièce : le cas par défaut d'un identifiant non préparé.</summary>
    public static GmailApiMessage MessageSansPiece(string id) => new(
        Id: id,
        ThreadId: "thread-" + id,
        From: "client@exemple.fr",
        To: "destinataire@ims.test",
        Cc: null,
        Subject: "Sans pièce",
        Rfc822MessageId: id + "@exemple.fr",
        BodyText: "Contenu",
        BodyHtml: "<p>Contenu</p>",
        Snippet: "Contenu",
        ReceivedAt: DateTime.UtcNow,
        IsRead: true,
        IsStarred: false,
        HasAttachments: false,
        LabelIds: new List<string> { "INBOX" },
        Attachments: new List<GmailApiAttachment>());

    /// <summary>Construit un message simulé dont la qualification est celle que Gmail renverrait.</summary>
    public static GmailApiMessage Message(
        string id, List<GmailApiAttachment> pieces, string? html = null) => new(
        Id: id,
        ThreadId: "thread-" + id,
        From: "client@exemple.fr",
        To: "destinataire@ims.test",
        Cc: null,
        Subject: "Avec pièces",
        Rfc822MessageId: id + "@exemple.fr",
        BodyText: "Contenu",
        BodyHtml: html,
        Snippet: "Contenu",
        ReceivedAt: DateTime.UtcNow,
        IsRead: true,
        IsStarred: false,
        HasAttachments: pieces.Any(p => !p.IsInline),
        LabelIds: new List<string> { "INBOX" },
        Attachments: pieces);

    // Brouillons et envois : enregistrés pour que le test puisse asserter le texte
    // RÉELLEMENT transmis à Gmail. C'est ce que la régression A1 vérifie.
    public sealed record DraftCall(
        string? DraftId, string To, string Subject, string Body,
        string? ThreadId, string? InReplyTo,
        IReadOnlyList<(string FileName, string MimeType, byte[] Content)> Attachments);

    public ConcurrentQueue<DraftCall> DraftCreates { get; } = new();
    public ConcurrentQueue<DraftCall> DraftUpdates { get; } = new();
    public ConcurrentQueue<string> DraftSends { get; } = new();

    /// <summary>Identifiant renvoyé par CreateDraftAsync (null = ne pas simuler de création).</summary>
    public string? NextDraftId { get; set; } = "draft-cree-1";

    /// <summary>Message (drafts.update / drafts.send) qui doit faire échouer l'appel, pour tester le 502.</summary>
    public string? FailingDraftId { get; set; }

    public Task<string> CreateDraftAsync(
        GmailConnection c, string to, string subject, string body, string? threadId, string? inReplyTo,
        IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null)
    {
        var id = NextDraftId;
        if (id == null) throw Unexpected();
        DraftCreates.Enqueue(new DraftCall(null, to, subject, body, threadId, inReplyTo,
            attachments ?? Array.Empty<(string, string, byte[])>()));
        return Task.FromResult(id);
    }

    public Task UpdateDraftAsync(
        GmailConnection c, string draftId, string to, string subject, string body,
        string? threadId, string? inReplyTo,
        IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null)
    {
        if (draftId == FailingDraftId)
            throw new InvalidOperationException("Erreur Gmail simulée sur drafts.update.");

        DraftUpdates.Enqueue(new DraftCall(draftId, to, subject, body, threadId, inReplyTo,
            attachments ?? Array.Empty<(string, string, byte[])>()));
        return Task.CompletedTask;
    }

    public Task<string> SendDraftAsync(GmailConnection c, string draftId)
    {
        if (draftId == FailingDraftId)
            throw new InvalidOperationException("Erreur Gmail simulée sur drafts.send.");

        DraftSends.Enqueue(draftId);
        return Task.FromResult("sent-" + draftId);
    }
    public Task DeleteDraftAsync(GmailConnection c, string draftId) => throw Unexpected();
    public Task<(string FileName, string MimeType, byte[] Content)> GetAttachmentAsync(
        GmailConnection c, string messageId, string attachmentId) => throw Unexpected();
    public Task<(string MessageId, string ThreadId)> SendMessageAsync(
        GmailConnection c, string to, string? cc, string? bcc, string? subject, string? bodyText,
        string? bodyHtml, string? threadId, string? inReplyTo,
        IReadOnlyList<(string FileName, string MimeType, byte[] Content)> attachments) => throw Unexpected();
}

/// <summary>
/// Hôte de test qui substitue <see cref="IGmailApiService"/> par un double enregistreur.
/// Utilisé par les tests d'actions (lu, étoile, archive, corbeille) qui ont besoin de
/// savoir ce qui a été demandé à Gmail sans l'appeler.
/// </summary>
public class GmailActionApiFactory : TacheApiFactory
{
    public RecordingGmailApiService Recorder { get; } = new();

    /// <summary>
    /// Remet le double à zéro pour les pannes simulées. Appelé en début de chaque test :
    /// les tests d'une classe partagent la même instance, et une panne oubliée rendrait
    /// le suivant vert ou rouge pour la mauvaise raison.
    /// </summary>
    public void ResetSimulatedFailures() => Recorder.ResetSimulatedFailures();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGmailApiService>();
            services.AddSingleton<IGmailApiService>(Recorder);
        });
    }
}
