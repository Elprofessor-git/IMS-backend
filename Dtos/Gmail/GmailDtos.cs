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
        public DateTime ReceivedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public int? CreatedTaskId { get; set; }
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
