using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Gmail
{
    /// <summary>
    /// Envoi d'un email via la boîte Gmail connectée de l'utilisateur.
    /// Aucun enregistrement <c>EmailAiReply</c> n'est créé : la composition est un
    /// brouillon éphémère, et rien n'est écrit en base si l'envoi succeeds ou échoue.
    /// </summary>
    public class ComposeEmailDto
    {
        public required List<string> To { get; set; }
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }

        public string? Subject { get; set; }

        /// <summary>Corps en texte brut. Toujours envoyé : c'est la version qui reste lisible partout.</summary>
        public string? BodyText { get; set; }

        /// <summary>Corps HTML facultatif. Envoyé en text/html à côté du texte brut.</summary>
        public string? BodyHtml { get; set; }

        public List<ComposeAttachmentDto> Attachments { get; set; } = new();

        /// <summary>Identifiant Gmail du message auquel on répond (facultatif).</summary>
        public string? InReplyTo { get; set; }
    }

    public class ComposeAttachmentDto
    {
        [Required]
        public string FileName { get; set; } = null!;

        [Required]
        public string MimeType { get; set; } = "application/octet-stream";

        /// <summary>Contenu encodé en Base64.</summary>
        [Required]
        public string ContentBase64 { get; set; } = null!;
    }

    public class ComposeEmailResultDto
    {
        public string GmailMessageId { get; set; } = null!;
        public string GmailThreadId { get; set; } = null!;
        public int AttachmentCount { get; set; }
        public long TotalBytes { get; set; }
    }

    // ── Édition du brouillon par IA ──────────────────────────────────
    // Ni email, ni fil, ni donnée IMS ne sont transmis : le modèle ne voit QUE le texte
    // que l'utilisateur a lui-même saisi dans la zone de composition.

    public class EditDraftRequestDto
    {
        /// <summary>Texte courant du brouillon, tel que saisi par l'utilisateur.</summary>
        [Required]
        [MinLength(1)]
        public string Text { get; set; } = null!;

        /// <summary>
        /// Reformulation (consigne libre) ou traduction. Toute autre valeur est refusée.
        /// </summary>
        public string Action { get; set; } = "Rewrite";

        /// <summary>Consigne libre, uniquement pour l'action Rewrite.</summary>
        public string? Instruction { get; set; }

        /// <summary>Langue cible, uniquement pour l'action Translate. Valeurs : FR, EN, AR.</summary>
        public string? TargetLanguage { get; set; }
    }

    public class EditDraftResponseDto
    {
        public string Text { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? TargetLanguage { get; set; }
    }
}
