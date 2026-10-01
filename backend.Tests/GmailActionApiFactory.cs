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
    public Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(
        GmailConnection c, string? q, int m = 30, string? p = null) => throw Unexpected();
    public Task<GmailApiMessage> GetMessageAsync(GmailConnection c, string id) => throw Unexpected();
    public Task<string> CreateDraftAsync(GmailConnection c, string to, string subject, string body, string? threadId, string? inReplyTo) =>
        throw Unexpected();
    public Task<string> SendDraftAsync(GmailConnection c, string draftId) => throw Unexpected();
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
