using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Sections qu'un lien partagé est autorisé à exposer.
    /// </summary>
    /// <remarks>
    /// Flags, pas une collection : le périmètre est un ENSEMBLE fermé que le backend
    /// connaît par avance. Une liste libre (ou un JSON de sections) laisserait passer
    /// une section inconnue et il faudrait décider à l'exécution quoi en faire — donc
    /// l'exposer. Ici un bit absent est un refus par défaut.
    /// </remarks>
    [Flags]
    public enum ShareLinkSection
    {
        Aucune = 0,
        Stock = 1,
        Commandes = 2,
        Importations = 4,

        Tout = Stock | Commandes | Importations
    }

    /// <summary>
    /// Portée logique du lien. Volontairement limitée à trois valeurs : ce sont les
    /// seules portées qui existent côté plateforme (Plateforme → ses Marques → ses
    /// Commandes). « Stock libre » n'en est PAS une : c'est un FILTRE, pas une portée —
    /// sans quoi un lien de portée Plateforme pourrait pénétrer le stock non rattaché.
    /// </summary>
    public enum ShareLinkScopeType
    {
        Plateforme = 1,
        Marque = 2,
        Commande = 3
    }

    /// <summary>
    /// Issue d'une tentative d'ouverture d'un lien partagé.
    /// </summary>
    /// <remarks>
    /// Volontairement COARSE. Token inconnu, expiré, révoqué ou saturé produisent tous
    /// <see cref="Refuse"/> : distinguer ces cas dans le journal aiderait un attaquant
    /// à savoir directement qu'un lien existe (énumération de tokens). Le journal sert à
    /// l'exploitation, pas à l'attaquant.
    /// </remarks>
    public enum ShareLinkAccessResult
    {
        Autorise = 1,
        Refuse = 2
    }

    public class ShareLink
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// SHA-256 (hex, 64 caractères) du token, JAMAIS le token lui-même.
        /// </summary>
        /// <remarks>
        /// Le token n'est retourné qu'une seule fois, à la création, et n'est jamais
        /// stocké. Une fuite de la base ne donne donc aucun lien exploitable : le
        /// secret est à usage unique et la table ne contient que son empreinte.
        /// Index unique : deux empreintes identiques sont impossibles, et la
        /// résolution par token reste une égalité sur index au lieu d'un balayage.
        /// </remarks>
        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [ForeignKey("CreatedByUser")]
        public string CreatedByUserId { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedByUser { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Expiration. Plafonnée à 7 jours à la création (ShareLinkService).</summary>
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>Révocation anticipée. Null = lien actif (jusqu'à expiration).</summary>
        public DateTime? RevokedAtUtc { get; set; }

        /// <summary>Ouvertures déjà servies. Incrémenté de façon atomique (cf. service).</summary>
        public int UseCount { get; set; } = 0;

        /// <summary>Nombre maximal d'ouvertures. Null = illimité.</summary>
        public int? MaxUses { get; set; }

        public ShareLinkScopeType ScopeType { get; set; }

        /// <summary>
        /// Id de l'entité de portée. Unicité vérifiée À LA CRÉATION : un ScopeId
        /// peut être supprimé entre-temps, d'où le contrôle applicatif (le lien
        /// devient alors inerte et renvoie un 404 comme tout lien invalide).
        /// </summary>
        public int ScopeId { get; set; }

        /// <summary>
        /// Le stock non rattaché à un scope est-il exposable ?
        /// </summary>
        /// <remarks>
        /// Faux par défaut : le stock libre est la réserve non affectée, hors
        /// plateforme/marque/commande de quiconque que ce soit. L'inclure par défaut
        /// ouvrirait à un lien de portée Étroite l'intégralité du stock non
        /// attribué.
        /// </remarks>
        public bool IsStockLibreAllowed { get; set; } = false;

        /// <summary>Drapeaux de sections exposées.</summary>
        public ShareLinkSection Sections { get; set; } = ShareLinkSection.Aucune;

        /// <summary>
        /// Filtres figés, sérialisés en JSON, validés par liste blanche stricte
        /// (ShareLinkFilters.Validate). Aucune clé inconnue n'est conservée, donc
        /// aucun champ non prévu ne peut finir dans une requête SQL.
        /// </summary>
        public string? FiltersJson { get; set; }

        /// <summary>
        /// Étiquette libre, pour que l'auteur retrouve son lien (« Audit Q3 »).
        /// Affichée au lecteur public : c'est une donnée disclosed volontairement,
        /// donc elle ne doit contenir aucun secret.
        /// </summary>
        [StringLength(100)]
        public string? Label { get; set; }

        public virtual ICollection<ShareLinkAccess> Acces { get; set; } = new List<ShareLinkAccess>();
    }

    /// <summary>
    /// Trace d'une ouverture d'un lien partagé.
    /// </summary>
    /// <remarks>
    /// On journalise l'IP et l'issue, JAMAIS le token (ni clair ni empreinte : même
    /// l'empreinte n'apporte rien ici et circulerait dans les exports). Le token
    /// n'est pas un champ de cette trace : il n'a nulle part où être écrit.
    /// </remarks>
    public class ShareLinkAccess
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ShareLink")]
        public int ShareLinkId { get; set; }
        public virtual ShareLink ShareLink { get; set; } = null!;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public ShareLinkAccessResult Result { get; set; }
    }
}