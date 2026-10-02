namespace Backend_Gestion_Magasin_API.Dtos.Gmail
{
    // ── Connexion ─────────────────────────────────────────────
    public class GmailConnectStartDto
    {
        public string AuthorizationUrl { get; set; } = null!;
    }

    public class GmailStatusDto
    {
        public bool Connected { get; set; }
        public string? GmailAddress { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public DateTime? LastSyncAt { get; set; }

        // Les identifiants OAuth Google sont-ils présents côté serveur (sinon la connexion est impossible).
        public bool Configure { get; set; }
        // L'assistance IA (analyse + brouillon de réponse) est-elle disponible (GROQ_API_KEY).
        public bool AiAvailable { get; set; }
        // Le stockage chiffré des tokens Gmail est-il opérationnel (Google:TokenEncryptionKey).
        public bool TokenStorageReady { get; set; }
        public int MessageCount { get; set; }
        public int UnreadCount { get; set; }
    }

    // ── Liste / détail message ────────────────────────────────
    public class GmailMessageListItemDto
    {
        public int Id { get; set; } // Id interne IMS (pas l'id Gmail brut)
        public string GmailMessageId { get; set; } = null!;
        public string GmailThreadId { get; set; } = null!;
        public string? From { get; set; }
        public string? Subject { get; set; }
        public string? Snippet { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool HasAttachments { get; set; }
        public bool HasTaskSuggestion { get; set; }
        public int? CreatedTaskId { get; set; }
    }

    public class GmailMessageDetailDto
    {
        public int Id { get; set; }
        public string GmailMessageId { get; set; } = null!;
        public string GmailThreadId { get; set; } = null!;
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Cc { get; set; }
        public string? Subject { get; set; }
        public string? BodyText { get; set; }
        /// <summary>HTML ASSAINI, src="cid:" réécrit vers le proxy IMS. À afficher via dangerouslySetInnerHTML.</summary>
        public string? BodyHtml { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool HasAttachments { get; set; }
        public List<GmailAttachmentDto> Attachments { get; set; } = new();
        public int? CreatedTaskId { get; set; }
    }

    /// <summary>Métadonnées d'une pièce jointe (jamais le contenu binaire).</summary>
    public class GmailAttachmentDto
    {
        public string GmailAttachmentId { get; set; } = null!;
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long SizeBytes { get; set; }
        public bool IsInline { get; set; }
        /// <summary>Endpoint de téléchargement relayé par l'API, pour le propriétaire du message.</summary>
        public string Url { get; set; } = null!;
    }

    // ── Suggestion de tâche par IA ────────────────────────────
    public class EmailTaskSuggestionDto
    {
        public int Id { get; set; }
        public int GmailMessageId { get; set; }
        public bool IsTask { get; set; }
        public decimal Confidence { get; set; }
        public string? SuggestedTitle { get; set; }
        public string? SuggestedDescription { get; set; }
        public string? SuggestedPriority { get; set; }
        public DateTime? SuggestedDueDate { get; set; }
        public string? SuggestedAssigneeUserId { get; set; }
        public string Statut { get; set; } = "Pending";
        public int? CreatedTaskId { get; set; }
    }

    // Corps envoyé quand l'utilisateur VALIDE (et peut modifier) la suggestion avant création réelle
    public class CreateTaskFromEmailDto
    {
        public required string Titre { get; set; }
        public string? Description { get; set; }
        public string? Priorite { get; set; }
        public DateTime? DateEcheance { get; set; }
        public string? AssigneUserId { get; set; }
    }

    // ── Réponse générée par IA ────────────────────────────────
    public class GenerateReplyRequestDto
    {
        // instruction libre optionnelle donnée par l'utilisateur ("réponds que le tissu arrive vendredi")
        public string? Instruction { get; set; }
    }

    public class EmailAiReplyDto
    {
        public int Id { get; set; }
        public int GmailMessageId { get; set; }
        public string? Subject { get; set; }
        public string Body { get; set; } = null!;
        public string Statut { get; set; } = "Generated";
        public DateTime GeneratedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string? GmailDraftId { get; set; }
    }

    public class UpdateReplyDto
    {
        public required string Body { get; set; }
        public string? Subject { get; set; }
    }

    /// <summary>
    /// Envoi d'une réponse relue. Le corps et l'objet font foi (A1), et les pièces
    /// jointes sont optionnelles (A4) — la réponse s'attache au fil par le couple
    /// threadId / In-Reply-To, ce qui évite toute pièce jointe automatique.
    /// </summary>
    public class SendReplyDto : UpdateReplyDto
    {
        public List<ComposeAttachmentDto> Attachments { get; set; } = new();
    }

    // ── Actions sur un message (lu, étoile, archive, corbeille) ─────────
    /// <summary>
    /// Champs nuls = action non demandée. Tous les indicateurs sont facultatifs pour
    /// permettre un envoi partiel (« marquer comme lu » seul) sans à-coup.
    /// </summary>
    public class UpdateMessageFlagsDto
    {
        /// <summary>Marquer comme lu (true) ou non lu (false).</summary>
        public bool? IsRead { get; set; }

        /// <summary>Ajouter (true) ou retirer (false) l'étoile.</summary>
        public bool? IsStarred { get; set; }

        /// <summary>Archiver : retire le message de la boîte de réception (Gmail : retrait de INBOX).</summary>
        public bool? Archive { get; set; }

        /// <summary>Mettre à la corbeille Gmail.</summary>
        public bool? Trash { get; set; }
    }

    public class GmailMessageFlagsDto
    {
        public int Id { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        /// <summary>Présence de l'étiquette INBOX côté Gmail après l'action.</summary>
        public bool IsArchived { get; set; }
        /// <summary>Présence de l'étiquette TRASHED côté Gmail après l'action.</summary>
        public bool IsTrashed { get; set; }
    }

    public class GmailThreadFlagsDto
    {
        public string GmailThreadId { get; set; } = null!;
        /// <summary>Nombre de messages du fil sur lesquels l'action a été appliquée.</summary>
        public int MessageCount { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool IsArchived { get; set; }
        public bool IsTrashed { get; set; }
    }

    // ── Fils de discussion ───────────────────────────────────
    // Une ligne = UN fil, positionnée sur son message le plus récent (comme Gmail).
    public class GmailThreadListItemDto
    {
        /// <summary>Identifiant de fil Gmail (GmailThreadId) — clé de regroupement.</summary>
        public string GmailThreadId { get; set; } = null!;
        public string? Subject { get; set; }
        public string? Snippet { get; set; }
        /// <summary>Message servant de référence à la ligne (le plus récent du fil).</summary>
        public int LastMessageId { get; set; }
        /// <summary>
        /// Identifiant Gmail (<c>messageId</c>) du message le plus récent, et non sa clé IMS.
        /// <para>
        /// ReplyPanel et la composition ont besoin de l'identifiant Gmail du parent, parce que
        /// c'est ce dernier que l'API Gmail accepte dans <c>threadId</c>/<c>In-Reply-To</c>.
        /// Envoyer <see cref="LastMessageId"/> (clé IMS) ferait échouer la résolution.
        /// </para>
        /// </summary>
        public string LastGmailMessageId { get; set; } = null!;
        public DateTime LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public int UnreadCount { get; set; }
        public bool IsStarred { get; set; }
        public bool HasAttachments { get; set; }
        public bool HasTaskSuggestion { get; set; }
        public List<string> Participants { get; set; } = new();
    }

    public class GmailThreadPageDto
    {
        public List<GmailThreadListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class GmailThreadDetailDto
    {
        public string GmailThreadId { get; set; } = null!;
        public string? Subject { get; set; }
        /// <summary>Messages du fil, du plus ancien au plus récent (ordre de lecture).</summary>
        public List<GmailMessageDetailDto> Messages { get; set; } = new();
    }

    // ── Liste paginée + résultat de synchronisation ────────────
    public class GmailMessagePageDto
    {
        public List<GmailMessageListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class GmailSyncResultDto
    {
        public int MessagesRecus { get; set; }
        public int MessagesNouveaux { get; set; }
        public int MessagesMisAJour { get; set; }
        public DateTime? LastSyncAt { get; set; }
    }
}
