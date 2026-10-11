using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Type d'une conversation. Les valeurs sont stockées en texte (conversion
    /// string du DbContext), convention du projet : lisibles en base et
    /// insensibles à un réordonnancement de l'énumération.
    /// </summary>
    public enum TypeConversation
    {
        /// <summary>Messagerie privée entre exactement deux comptes.</summary>
        Direct = 0,

        /// <summary>Groupe privé : membres ajoutés par un admin de la conversation.</summary>
        Groupe = 1,

        /// <summary>Canal, public ou privé. Un canal public se modère avec PeutGererDiscussion.</summary>
        Canal = 2,

        /// <summary>
        /// Conversation RATTACHÉE à un objet métier (E3) : commande, OF, tâche de
        /// production ou achat. Une seule par objet, garantie par l'index unique
        /// filtré (ContexteType, ContexteId). L'appartenance n'y régit PAS l'accès :
        /// seule la permission du module de l'objet ouvre la conversation (voir le
        /// contrôleur). Les membres ne sont que la liste des participants.
        /// </summary>
        Contexte = 3
    }

    /// <summary>
    /// Nature de l'objet métier rattaché à une conversation contextuelle (E3).
    /// Stockée en texte dans <see cref="Conversation.ContexteType"/> (conversion
    /// string) : lisible en base et insensible à un réordonnancement.
    /// </summary>
    public enum TypeContexte
    {
        /// <summary>Commande client (#CMD-&lt;id&gt;), module « commandes ».</summary>
        Commande = 0,

        /// <summary>Ordre de fabrication (#OF-&lt;id&gt;), module « production ».</summary>
        OrdreFabrication = 1,

        /// <summary>Tâche de production (#TAC-&lt;id&gt;), module « taches ».</summary>
        TacheProduction = 2,

        /// <summary>Achat (#ACH-&lt;id&gt;), module « achats ».</summary>
        Achat = 3
    }

    /// <summary>
    /// Conversation de messagerie interne (module Discussion, E1).
    ///
    /// Un DM est dédupliqué par <see cref="CleDirecte"/> ("idMin:idMax" des deux
    /// participants, index unique) : deux requêtes simultanées ne peuvent pas
    /// créer deux conversations distinctes pour la même paire.
    ///
    /// <see cref="ContexteType"/> et <see cref="ContexteId"/> rattachent, en E3, une
    /// conversation à un objet métier (commande / OF / tâche / achat). Index unique
    /// filtré pour qu'un objet métier n'ait jamais deux conversations. Null pour
    /// toutes les conversations libres (DM, groupe, canal).
    /// </summary>
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        public TypeConversation Type { get; set; } = TypeConversation.Direct;

        /// <summary>Nom affiché (groupes et canaux). Null pour un DM.</summary>
        [StringLength(100)]
        public string? Nom { get; set; }

        /// <summary>Canal privé / groupe : visible uniquement de ses membres. Les DM sont toujours privés.</summary>
        public bool EstPrive { get; set; } = true;

        /// <summary>Clé de déduplication d'un DM : "idMin:idMax" triés. Null pour Groupe/Canal.</summary>
        [StringLength(80)]
        public string? CleDirecte { get; set; }

        // Rattachement futur (E3) — jamais renseigné en E1.
        [StringLength(50)]
        public string? ContexteType { get; set; }

        public int? ContexteId { get; set; }

        [Required]
        public string CreeParUserId { get; set; } = string.Empty;

        public DateTime CreeLe { get; set; } = DateTime.Now;

        /// <summary>Archive logique : la conversation quitte les listes mais reste consultable par ses membres.</summary>
        public DateTime? ArchiveLe { get; set; }

        // Relations
        public virtual ICollection<ConversationMembre> Membres { get; set; } = new List<ConversationMembre>();
        public virtual ICollection<MessageDiscussion> Messages { get; set; } = new List<MessageDiscussion>();
    }

    /// <summary>
    /// Appartenance d'un utilisateur à une conversation.
    ///
    /// <see cref="DernierMessageLuId"/> est un pointeur par ID (pas par date) :
    /// les non-lus se calculent en SQL sur "Id > DernierMessageLuId et auteur
    /// différent de moi". <see cref="AQuitteLe"/> rend l'appartenance inactive
    /// sans supprimer la ligne, pour conserver l'historique des lectures.
    /// </summary>
    public class ConversationMembre
    {
        [Key]
        public int Id { get; set; }

        public int ConversationId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Admin de la conversation : ajoute des membres, modère. Distinct de PeutGererDiscussion (module).</summary>
        public bool EstAdmin { get; set; }

        public DateTime RejointLe { get; set; } = DateTime.Now;

        /// <summary>ID du dernier message lu. 0 = rien de lu.</summary>
        public long DernierMessageLuId { get; set; }

        /// <summary>Coupe les notifications de mentions pour cette conversation.</summary>
        public bool Muet { get; set; }

        /// <summary>Moment du départ (congédié ou démission du canal). Null = membre actif.</summary>
        public DateTime? AQuitteLe { get; set; }

        // Relations
        public virtual Conversation Conversation { get; set; } = null!;
        public virtual ApplicationUser User { get; set; } = null!;
    }

    /// <summary>
    /// Message de discussion. Texte BRUT, jamais interprété en HTML.
    ///
    /// <see cref="Revision"/> : séquence PostgreSQL globale (partagée par toutes
    /// les lignes de la table), renouvelée par trigger à chaque insertion,
    /// modification OU suppression logique. C'est le curseur de GET /sync :
    /// "tout ce dont la revision dépasse mon curseur m'intéresse".
    ///
    /// La suppression est LOGIQUE : le contenu est vidé (rien à lire pour un
    /// futur écran), <see cref="SupprimeLe"/> renseigné, nouvelle revision
    /// émise pour que les clients le propagent.
    /// </summary>
    public class MessageDiscussion
    {
        [Key]
        public long Id { get; set; }

        public int ConversationId { get; set; }

        [Required]
        public string AuteurUserId { get; set; } = string.Empty;

        /// <summary>Texte brut, 1 à 4000 caractères. vidé à la suppression logique.</summary>
        [StringLength(4000)]
        public string Contenu { get; set; } = string.Empty;

        public DateTime EnvoyeLe { get; set; } = DateTime.Now;

        public DateTime? ModifieLe { get; set; }

        public DateTime? SupprimeLe { get; set; }

        /// <summary>Message auquel celui-ci répond (optionnel).</summary>
        public long? ReponseAId { get; set; }

        /// <summary>Curseur de synchronisation, renouvelé par trigger PostgreSQL à chaque écriture.</summary>
        public long Revision { get; set; }

        // Relations
        public virtual Conversation Conversation { get; set; } = null!;
        // Pas de navigation vers ApplicationUser : AuteurUserId est un simple string
        // SANS FK (précédent Notification.UtilisateurId). Une navigation produirait une
        // FK vers AspNetUsers et, à cause du hard delete de UserController, effacerait
        // l'historique de discussion avec le compte. Le nom de l'auteur est résolu par
        // sous-requête dans les projections.
    }
}
