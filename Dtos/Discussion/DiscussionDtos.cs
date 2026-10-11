using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Discussion
{
    /// <summary>Corps de POST /api/Discussion : création d'un DM, d'un groupe ou d'un canal.</summary>
    public class CreerConversationDto
    {
        /// <summary>Nom du type EN CHAÎNE : "Direct", "Groupe" ou "Canal".</summary>
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = string.Empty;

        /// <summary>Nom affiché. Obligatoire pour Groupe/Canal, ignoré pour un DM.</summary>
        [StringLength(100)]
        public string? Nom { get; set; }

        /// <summary>
        /// Groupe/Canal : privé (visible des seuls membres) ou public (visible/modérable
        /// via PeutGererDiscussion). Un DM est toujours privé.
        /// </summary>
        public bool EstPrive { get; set; } = true;

        /// <summary>
        /// Participants à ajouter à la création. Pour un DM : exactement un autre
        /// utilisateur (l'appelant en est toujours membre). Pour un Groupe/Canal :
        /// au moins un, l'appelant devient admin.
        /// </summary>
        public List<string> MembreIds { get; set; } = new();
    }

    /// <summary>Corps de POST /api/Discussion/{id}/messages.</summary>
    public class EnvoyerMessageDto
    {
        /// <summary>Texte BRUT, 1 à 4000 caractères. Jamais interprété en HTML.</summary>
        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Contenu { get; set; } = string.Empty;

        /// <summary>Message auquel celui-ci répond (optionnel).</summary>
        public long? ReponseAId { get; set; }
    }

    /// <summary>Corps de PUT /api/Discussion/{id}/messages/{msgId}.</summary>
    public class ModifierMessageDto
    {
        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Contenu { get; set; } = string.Empty;
    }

    /// <summary>Corps de POST /api/Discussion/{id}/lire.</summary>
    public class MarquerLuDto
    {
        public long JusquaMessageId { get; set; }
    }

    /// <summary>Corps de POST /api/Discussion/{id}/membres.</summary>
    public class AjouterMembresDto
    {
        public List<string> MembreIds { get; set; } = new();
    }

    // ── Projections ──────────────────────────────────────────────────────────

    /// <summary>Vue d'une conversation dans la liste de l'utilisateur.</summary>
    public class ConversationDto
    {
        public int Id { get; set; }
        /// <summary>Type EN CHAÎNE ("Direct" | "Groupe" | "Canal").</summary>
        public string Type { get; set; } = string.Empty;
        public string? Nom { get; set; }
        public bool EstPrive { get; set; }
        public bool EstAdmin { get; set; }
        public DateTime CreeLe { get; set; }
        public DateTime? ArchiveLe { get; set; }
        /// <summary>Nombre de non-lus pour MOI (SQL : Id &gt; DernierMessageLuId et auteur != moi).</summary>
        public int NonLus { get; set; }
        public DateTime? DernierMessageLe { get; set; }
        public string? DernierMessageApercu { get; set; }
        public List<MembreDto> Membres { get; set; } = new();
    }

    /// <summary>Vue d'un membre de conversation.</summary>
    public class MembreDto
    {
        public string UserId { get; set; } = string.Empty;
        public string NomAffichage { get; set; } = string.Empty;
        public bool EstAdmin { get; set; }
        public bool Muet { get; set; }
        public DateTime RejointLe { get; set; }
        public DateTime? AQuitteLe { get; set; }
    }

    /// <summary>Vue d'un message.</summary>
    public class MessageDto
    {
        public long Id { get; set; }
        public int ConversationId { get; set; }
        public string AuteurUserId { get; set; } = string.Empty;
        public string AuteurNom { get; set; } = string.Empty;
        public string Contenu { get; set; } = string.Empty;
        public DateTime EnvoyeLe { get; set; }
        public DateTime? ModifieLe { get; set; }
        public bool EstSupprime { get; set; }
        public long? ReponseAId { get; set; }
        public long Revision { get; set; }

        /// <summary>
        /// Cartes des références (#CMD-123, #OF-…, #TAC-…, #ACH-…) présentes dans le
        /// contenu, calculées CÔTÉ SERVEUR pour le LECTEUR courant (E3). Un objet hors
        /// des permissions du lecteur n'a AUCUNE carte ; un lecteur sans droit
        /// d'écriture sur le module ne reçoit jamais de montant ni de tiers.
        /// </summary>
        public List<ReferenceDto> References { get; set; } = new();
    }

    /// <summary>
    /// Carte d'une référence métier dans un message (E3). Toujours calculée pour le
    /// LECTEUR : le serveur n'envoie que ce que ses droits autorisent.
    /// </summary>
    public class ReferenceDto
    {
        /// <summary>Type de l'objet EN CHAÎNE ("Commande" | "OrdreFabrication" | "TacheProduction" | "Achat").</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Jeton canonique inséré dans le texte, ex. "CMD-123".</summary>
        public string Reference { get; set; } = string.Empty;

        public int Id { get; set; }

        /// <summary>Libellé toujours lisible (n° de commande, titre de tâche…).</summary>
        public string Libelle { get; set; } = string.Empty;

        /// <summary>Statut de l'objet, quand il en a un (nullation pour un OF).</summary>
        public string? Statut { get; set; }

        /// <summary>Date significative de l'objet (création / commande / achat).</summary>
        public DateTime? Date { get; set; }

        // ── Champs SENSIBLES : renseignés UNIQUEMENT si le lecteur a le droit
        //    d'ÉCRITURE du module de l'objet. Sinon ils restent null (jamais de
        //    prix ni de tiers « masqué côté client » : le serveur ne les émet pas). ──

        /// <summary>Montant total (commande, achat). Null si le lecteur n'a pas le droit d'écriture.</summary>
        public decimal? Montant { get; set; }

        /// <summary>Devise du montant. Null si pas de droit d'écriture ou pas de montant.</summary>
        public string? Devise { get; set; }

        /// <summary>Tiers identifiant (client d'une commande, fournisseur d'un achat). Null si pas de droit d'écriture.</summary>
        public string? Tiers { get; set; }
    }

    /// <summary>Entrée d'autocomplétion d'une référence (E3) : uniquement des objets accessibles au lecteur.</summary>
    public class ResultatReferenceDto
    {
        /// <summary>Type de l'objet EN CHAÎNE.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Jeton à insérer, ex. "CMD-123".</summary>
        public string Reference { get; set; } = string.Empty;

        public int Id { get; set; }

        /// <summary>Libellé affiché sous la suggestion.</summary>
        public string Libelle { get; set; } = string.Empty;
    }

    /// <summary>Vue d'un utilisateur de l'annuaire de la Discussion (actif et interne).</summary>
    public class UtilisateurDiscussionDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string? Poste { get; set; }
        public string NomAffichage { get; set; } = string.Empty;
    }

    /// <summary>Réponse de GET /api/Discussion/sync : alimente le polling du frontend E2.</summary>
    public class SyncDto
    {
        /// <summary>Nouveau curseur à repasser au prochain appel (max de revision renvoyé, ou l'entrant).</summary>
        public long Curseur { get; set; }

        /// <summary>Messages créés/modifiés/supprimés depuis le curseur, dans MES conversations.</summary>
        public List<MessageDto> Messages { get; set; } = new();

        /// <summary>Snapshot de MES conversations et de leurs membres (appartenances à jour).</summary>
        public List<ConversationDto> Conversations { get; set; } = new();
    }
}
