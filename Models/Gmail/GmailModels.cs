using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models.Gmail
{
    // ── Connexion Gmail d'un utilisateur IMS ─────────────────────────────
    // 1 utilisateur IMS -> 0..N comptes Gmail connectés (unicité UserId+GoogleUserId).
    // Le RefreshToken n'est JAMAIS stocké en clair : voir TokenEncryptionService.
    public class GmailConnection
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!; // FK -> AspNetUsers.Id (string, Identity)

        [Required, StringLength(255)]
        public string GmailAddress { get; set; } = null!;

        [Required, StringLength(255)]
        public string GoogleUserId { get; set; } = null!;

        [Required]
        public string RefreshTokenEncrypted { get; set; } = null!;

        [Required, StringLength(500)]
        public string GrantedScopes { get; set; } = null!;

        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastSyncAt { get; set; }

        // Phase 5 (Pub/Sub, non implémentée ici) : id d'historique Gmail + expiration du watch
        public string? HistoryId { get; set; }
        public DateTime? WatchExpiration { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? DisconnectedAt { get; set; }

        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ICollection<GmailMessage> Messages { get; set; } = new List<GmailMessage>();
    }

    // ── Email synchronisé depuis Gmail (métadonnées + corps, pas de pièces jointes en V1) ──
    public class GmailMessage
    {
        public int Id { get; set; }

        [Required]
        public int GmailConnectionId { get; set; }

        [Required, StringLength(100)]
        public string GmailMessageId { get; set; } = null!;

        [Required, StringLength(100)]
        public string GmailThreadId { get; set; } = null!;

        [StringLength(255)]
        public string? From { get; set; }
        [StringLength(255)]
        public string? To { get; set; }
        [StringLength(255)]
        public string? Cc { get; set; }
        [StringLength(500)]
        public string? Subject { get; set; }

        // En-tête RFC822 « Message-Id » d'origine : indispensable pour répondre dans le fil
        // (In-Reply-To/References) sans re-appeler l'API Gmail.
        [StringLength(255)]
        public string? Rfc822MessageId { get; set; }

        public string? BodyText { get; set; }

        // Version HTML du corps, ASSAINIE à la réception (scripts, handlers on*, javascript:
        // et iframes retirés) et dont les src="cid:" ont été réécrites vers le proxy IMS.
        // BodyText reste la référence : c'est lui qui alimente l'analyse IA et la recherche.
        public string? BodyHtml { get; set; }
        [StringLength(500)]
        public string? Snippet { get; set; }

        public DateTime ReceivedAt { get; set; }

        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool HasAttachments { get; set; }

        // liste de labels Gmail (INBOX, UNREAD, ...) sérialisée en JSON, lecture seule
        public string? LabelsJson { get; set; }

        public bool IsSynchronized { get; set; } = true;
        public DateTime? LastSyncedAt { get; set; }

        // Provenance côté IMS : renseigné si une TacheProduction a été créée depuis ce message.
        // Seule FK entre GmailMessage et TacheProduction (évite deux chemins de FK ambigus pour EF).
        public int? CreatedTaskId { get; set; }

        public virtual GmailConnection GmailConnection { get; set; } = null!;
        public virtual TacheProduction? TacheCreee { get; set; }
        public virtual ICollection<EmailAiAnalysis> Analyses { get; set; } = new List<EmailAiAnalysis>();
        public virtual ICollection<EmailAiReply> Reponses { get; set; } = new List<EmailAiReply>();
        public virtual ICollection<GmailAttachment> Attachments { get; set; } = new List<GmailAttachment>();

        /// <summary>
        /// Pièces jointes en attente d'enregistrement, le temps que l'Id IMS du message soit
        /// attribué (les lignes <c>GmailAttachment</c> en ont besoin comme clé étrangère).
        /// Jamais persistée ni exposée : c'est un tampon de synchronisation, d'où le nom
        /// intracellular et l'absence de mapping EF.
        /// </summary>
        [NotMapped]
        public List<Services.Gmail.GmailApiAttachment> _PendingAttachments { get; set; } = new();
    }

    /// <summary>
    /// Métadonnées d'une pièce jointe d'un email. On ne stocke QUE les métadonnées :
    /// le contenu binaire est relu à la demande via l'API Gmail (endpoint proxy), ce qui
    /// évite de dupliquer des mégaoctets en base et garde la source de vérité Gmail.
    /// </summary>
    public class GmailAttachment
    {
        public int Id { get; set; }

        // FK -> GmailMessage.Id (la propriété Images, clé composite)
        public int GmailMessageId { get; set; }

        // Identifiant de pièce renvoyé par l'API Gmail (payload.parts[].body.attachmentId).
        [StringLength(255)]
        public string GmailAttachmentId { get; set; } = null!;

        [StringLength(255)]
        public string? FileName { get; set; }

        [StringLength(255)]
        public string? MimeType { get; set; }

        public long SizeBytes { get; set; }

        /// <summary>Image intégrée au corps HTML (src="cid:...") plutôt que pièce jointe classique.</summary>
        public bool IsInline { get; set; }

        // En-tête « Content-ID » (sans les chevrons) pour les images intégrées.
        [StringLength(255)]
        public string? ContentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual GmailMessage GmailMessage { get; set; } = null!;
    }

    public enum StatutAnalyse
    {
        Pending,
        Approved,
        Rejected
    }

    // ── Suggestion IA "ceci est une tâche" — jamais créée automatiquement ──
    public class EmailAiAnalysis
    {
        public int Id { get; set; }

        [Required]
        public int GmailMessageId { get; set; }

        public bool IsTask { get; set; }

        [Column(TypeName = "numeric(5,4)")]
        public decimal Confidence { get; set; }

        [StringLength(255)]
        public string? SuggestedTitle { get; set; }
        public string? SuggestedDescription { get; set; }
        [StringLength(50)]
        public string? SuggestedPriority { get; set; }
        public DateTime? SuggestedDueDate { get; set; }

        // FK optionnelle -> ApplicationUser (Id string chez Identity)
        [StringLength(450)]
        public string? SuggestedAssigneeUserId { get; set; }

        // Réponse brute du modèle (débogage / audit)
        public string? AnalysisJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public StatutAnalyse Statut { get; set; } = StatutAnalyse.Pending;

        public virtual GmailMessage GmailMessage { get; set; } = null!;
    }

    public enum StatutReponseIa
    {
        Generated,
        Edited,
        Approved,
        Rejected,
        Sent
    }

    // ── Réponse générée par IA à un email — jamais envoyée sans validation humaine ──
    public class EmailAiReply
    {
        public int Id { get; set; }

        [Required]
        public int GmailMessageId { get; set; }

        [StringLength(500)]
        public string? Subject { get; set; }

        [Required]
        public string Body { get; set; } = null!;

        public StatutReponseIa Statut { get; set; } = StatutReponseIa.Generated;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? SentAt { get; set; }

        [StringLength(100)]
        public string? GmailDraftId { get; set; }
        [StringLength(100)]
        public string? GmailSentMessageId { get; set; }

        public virtual GmailMessage GmailMessage { get; set; } = null!;
    }
}
