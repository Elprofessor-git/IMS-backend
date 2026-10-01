using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Partage
{
    /// <summary>
    /// Filtres figés d'un lien partagé.
    /// </summary>
    /// <remarks>
    /// LISTE BLANCHE STRICTE. Ce type est le SEUL ensemble de clés accepté : tout ce
    /// qui n'est pas une de ces propriétés est rejeté à la création (ShareLinkFilters),
    /// jamais sérialisé, jamais interprété. C'est ce qui empêche un champ inconnu
    /// d'atteindre une expression LINQ arbitraire.
    /// </remarks>
    public class ShareLinkFiltersDto
    {
        public string? Article { get; set; }
        public string? Couleur { get; set; }
        public string? Taille { get; set; }
        public string? Statut { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
    }

    public class CreateShareLinkDto
    {
        public ShareLinkScopeType ScopeType { get; set; }
        public int ScopeId { get; set; }

        /// <summary>Sections exposées. Au moins une exigée par le controller.</summary>
        public ShareLinkSection Sections { get; set; }

        /// <summary>
        /// Durée de validité en heures. Plafonnée à 168 h (7 jours) côté service :
        /// une valeur plus grande est ramenée au plafond, jamais refusée en bloc (le
        /// client n'a pas à connaître la constante).
        /// </summary>
        public int DureeHeures { get; set; } = 24;

        /// <summary>Nombre maximal d'ouvertures. Null = illimité.</summary>
        public int? MaxUses { get; set; }

        /// <summary>
        /// Exposer aussi le stock non rattaché (libre). Opt-in explicite.
        /// </summary>
        public bool IsStockLibreAllowed { get; set; }

        public ShareLinkFiltersDto? Filters { get; set; }

        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string? Label { get; set; }
    }

    /// <summary>
    /// Réponse à la création : le token en clair est ici, et NULLE PART ailleurs.
    /// </summary>
    public class CreateShareLinkResponseDto
    {
        public ShareLinkDto Link { get; set; } = null!;

        /// <summary>
        /// Token en clair, retourné UNE SEULE FOIS. Jamais persisté, jamais relisté,
        /// jamais journalisé. La base ne connaît que son empreinte SHA-256.
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>Vue administrateur d'un lien. Ne contient NI token ni empreinte.</summary>
    public class ShareLinkDto
    {
        public int Id { get; set; }
        public string? Label { get; set; }
        public ShareLinkScopeType ScopeType { get; set; }
        public int ScopeId { get; set; }
        public string ScopeLibelle { get; set; } = string.Empty;
        public ShareLinkSection Sections { get; set; }
        public bool IsStockLibreAllowed { get; set; }
        public ShareLinkFiltersDto? Filters { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public int UseCount { get; set; }
        public int? MaxUses { get; set; }
        public bool Actif { get; set; }
    }

    /// <summary>
    /// Corps de POST /api/Partage/Public. Le token vient dans le CORPS, jamais dans
    /// l'URL : l'URL finit dans les logs d'accès Vercel/Render, un corps de requête
    /// non.
    /// </summary>
    public class PublicShareRequestDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Token { get; set; } = string.Empty;
    }

    public class PartagePublicDto
    {
        public string? Label { get; set; }
        public string ScopeType { get; set; } = string.Empty;
        public string ScopeLibelle { get; set; } = string.Empty;
        public DateTime DonneesAu { get; set; }
        public string[] Sections { get; set; } = [];

        /// <summary>
        /// Les sections absentes de la réponse sont NULLES (jamais des tableaux vides) :
        /// le client distingue « section non partagée » de « section partagée mais vide ».
        /// </summary>
        public List<PartageStockDto>? Stock { get; set; }
        public List<PartageCommandeDto>? Commandes { get; set; }
        public List<PartageImportationDto>? Importations { get; set; }
    }

    /// <summary>
    /// Projection stock publique. AUCUN prix, AUCUN coût, AUCUNE note interne.
    /// </summary>
    public class PartageStockDto
    {
        public int Id { get; set; }
        public string? ArticleDesignation { get; set; }
        public string? ArticleReference { get; set; }
        public string? Couleur { get; set; }
        public string? Taille { get; set; }
        public string? Dimension { get; set; }
        public string? EmplacementPhysique { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteReservee { get; set; }
        public string TypeStock { get; set; } = string.Empty;
    }

    /// <summary>Projection commande publique. Pas de prix de façon, pas de notes.</summary>
    public class PartageCommandeDto
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string Statut { get; set; } = string.Empty;
        public DateTime? DateLivraisonSouhaitee { get; set; }
        public string? ClientNom { get; set; }
    }

    /// <summary>
    /// Projection ligne d'importation publique. Ni prix unitaire, ni montant, ni
    /// fournisseur, ni chemin de document.
    /// </summary>
    public class PartageImportationDto
    {
        public int Id { get; set; }
        public string ReferenceImportation { get; set; } = string.Empty;
        public string? ArticleDesignation { get; set; }
        public string? Designation { get; set; }
        public string? Couleur { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteRecue { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string TypeDestination { get; set; } = string.Empty;
    }
}