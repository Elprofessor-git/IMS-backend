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

    /// <summary>
    /// Rapport de la maintenance de requalification des pièces jointes (A2).
    /// <para>
    /// <see cref="Examines"/> compte les messages réellement relus auprès de Gmail ;
    /// <see cref="Reclasses"/> compte les PIÈCES dont la qualification a changé (une
    /// pièce déjà correcte n'est pas comptée). Relancer l'endpoint est sans effet : c'est
    /// ce qui le rend idempotent et donc sûr à rejouer.
    /// </para>
    /// </summary>
    public class AttachmentRequalificationDto
    {
        public int Examines { get; set; }
        public int Reclasses { get; set; }
        public int Erreurs { get; set; }

        /// <summary>Détail par message — nécessaire pour un lancement à la demande supervisé.</summary>
        public List<string> Messages { get; set; } = new();
    }

    /// <summary>
    /// Rapport du rattrapage des pièces jointes manquantes (A2b).
    /// <para>
    /// <see cref="Examines"/> compte les messages réellement relus auprès de Gmail et
    /// <see cref="Creees"/> les lignes de pièces créées. Il n'y a pas de compteur de
    /// reclassification ici : le rattrapage ne traite que des messages qui n'ont AUCUNE
    /// ligne, donc la qualification issue du parseur s'applique à la création. Les lignes
    /// déjà présentes sont le ressort du rapport de requalification (A2).
    /// <see cref="Restants"/> indique le nombre de messages encore candidats APRÈS ce
    /// passage : tant qu'il est positif, une relance reprend exactement où celle-ci s'est
    /// arrêtée, sans curseur à transporter.
    /// </para>
    /// </summary>
    public class AttachmentBackfillDto
    {
        public int Examines { get; set; }
        public int Creees { get; set; }
        public int Erreurs { get; set; }

        /// <summary>Messages candidats non couverts par le budget de ce passage.</summary>
        public int Restants { get; set; }

        /// <summary>Détail par message — nécessaire pour un lancement à la demande supervisé.</summary>
        public List<string> Messages { get; set; } = new();
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
