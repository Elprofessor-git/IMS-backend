using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models.Gmail;

namespace Backend_Gestion_Magasin_API.Dtos.Gmail
{
    /// <summary>
    /// Envoi d'un email via la boîte Gmail connectée de l'utilisateur. Point de passage
    /// unique pour les quatre modes de composition.
    /// <para>
    /// Un <c>EmailAiReply</c> n'est créé que si <see cref="AiReplyId"/> est fourni, c'est-à-dire
    /// si le texte provient d'une proposition de l'assistance IA : l'envoi referme alors cette
    /// trace en y copiant le texte réellement expédié. Un message écrit sans l'IA ne laisse
    /// aucune trace, et rien n'est écrit en base si l'envoi échoue.
    /// </para>
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

        /// <summary>
        /// Mode de composition, pour le contrôle des en-têtes. Une chaîne et non l'enum
        /// lui-même : la sérialisation JSON du projet n'accepte pas les noms d'enum, et
        /// « 2 » dans une requête n'est pas lisible. Une valeur inconnue est REFUSÉE
        /// (voir ParseComposeMode) au lieu de retomber sur « nouveau message ».
        /// </summary>
        public string? Mode { get; set; }

        /// <summary>
        /// Message de référence IMS, obligatoire hors mode <see cref="ComposeMode.New"/>.
        /// <para>
        /// C'est un identifiant IMS et non un identifiant Gmail : le serveur vérifie
        /// qu'il appartient bien à la connexion active avant d'en tirer le fil et
        /// l'en-tête In-Reply-To. Le client ne fournit jamais ces en-têtes lui-même.
        /// </para>
        /// </summary>
        public int? ReplyToMessageId { get; set; }

        /// <summary>
        /// Proposition IA à solder lors de cet envoi (facultatif). Le corps réellement
        /// envoyé est alors conservé en plus du texte généré, et la proposition passe au
        /// statut Envoyé. L'envoi n'a lieu qu'après un clic explicite : l'IA ne déclenche
        /// jamais d'envoi.
        /// </summary>
        public int? AiReplyId { get; set; }
    }

    /// <summary>
    /// Préremplissage du composeur pour une réponse ou un transfert. Calculé côté
    /// serveur : les participants d'un « répondre à tous » et les en-têtes d'objet ne
    /// sont jamais acceptés depuis le client.
    /// </summary>
    public class ComposePrefillDto
    {
        /// <summary>Mode effectif, en toutes lettres : « New », « Reply », « ReplyAll », « Forward ».</summary>
        public string Mode { get; set; } = "New";

        /// <summary>Destinataires proposés. Vide en transfert : l'utilisateur choisit.</summary>
        public List<string> To { get; set; } = new();

        /// <summary>Copies proposées : les autres participants, en « répondre à tous ».</summary>
        public List<string> Cc { get; set; } = new();

        public string Subject { get; set; } = string.Empty;

        /// <summary>Zone de rédaction préremplie. Volontairement VIDE : le texte cité de l'email
        /// d'origine ne la rejoint jamais, sinon une reformulation ou une traduction
        /// s'imposerait dans le texte de l'expéditeur.</summary>
        public string BodyText { get; set; } = string.Empty;

        /// <summary>
        /// Citation du message d'origine, affichée sous la zone de rédaction et JAMAIS
        /// éditable. Elle ne sert qu'à l'affichage : à l'envoi, le serveur reconstruit
        /// lui-même la citation, de sorte qu'elle ne puisse ni être traduite par erreur,
        /// ni être modifiée en cours de route.
        /// </summary>
        public string QuotedText { get; set; } = string.Empty;

        /// <summary>Message de référence à renvoyer au serveur comme <c>ReplyToMessageId</c>.</summary>
        public int ReplyToMessageId { get; set; }

        /// <summary>
        /// Proposition IA déjà générée pour ce message et encore exploitable, s'il en
        /// existe une. Évite à l'utilisateur de régénérer un texte qu'il a déjà payé.
        /// </summary>
        public int? AiReplyId { get; set; }
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
        /// <summary>
        /// Texte courant du brouillon, tel que saisi par l'utilisateur.
        /// <para>
        /// La propriété est obligatoire mais sa longueur ne l'est pas : « Générer » part
        /// d'une zone VIDE, puisque c'est un message neuf. Refuser un texte vide ici
        /// interdirait la seule action utile de ce mode. Ce qui est refusé, en revanche,
        /// l'est dans le contrôleur : reformuler ou traduire un texte vide n'a pas de sens.
        /// </para>
        /// </summary>
        // AllowEmptyStrings : « Required » rejette la chaîne vide, or « Générer » part
        // précisément d'une zone vide. Sans cette option, la validation automatique
        // répond 400 avant même que le contrôleur ne distingue Generate de Rewrite.
        [Required(AllowEmptyStrings = true)]
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
