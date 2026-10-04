using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Stock
{
    /// <summary>
    /// Filtres de GET /api/Stock/Liste. Tous optionnels, tous combinés en ET.
    /// </summary>
    public class StockListeFiltresDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int Taille { get; set; } = 50;

        /// <summary>Portée d'une commande client.</summary>
        public int? CommandeClientId { get; set; }

        public int? ClientId { get; set; }
        public int? PlateformeId { get; set; }

        /// <summary>Libre, Reserve ou Importe. Valeur inconnue =&gt; 400.</summary>
        public string? TypeStock { get; set; }

        /// <summary>Catégorie d'article, correspondance exacte.</summary>
        public string? Categorie { get; set; }

        /// <summary>Recherche libre sur désignation et référence.</summary>
        [StringLength(100)]
        public string? Q { get; set; }

        /// <summary>Uniquement les lignes sous le seuil d'alerte de leur article.</summary>
        public bool AlertesOnly { get; set; }
    }

    /// <summary>
    /// Une ligne de la liste stock.
    /// </summary>
    /// <remarks>
    /// PROJECTION, PAS L'ENTITÉ. Ni <c>PrixUnitaire</c>, ni <c>PrixUnitaireTND</c>, ni
    /// <c>Devise</c>, ni <c>Notes</c>, ni <c>ValidePar</c> : la liste est destinée à
    /// être partagée, et un prix n'a rien à y faire. <c>GET /api/Stock</c> expose
    /// encore l'entité complète — dette connue, hors périmètre ici.
    /// </remarks>
    public class StockListeDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleDesignation { get; set; }
        public string? ArticleReference { get; set; }
        public string? ArticleCategorie { get; set; }
        public int SeuilAlerte { get; set; }

        public string? Couleur { get; set; }
        public string? CodeCouleur { get; set; }
        public string? Taille { get; set; }
        public string? Dimension { get; set; }
        public string? EmplacementPhysique { get; set; }
        public string? NumeroLot { get; set; }

        public decimal Quantite { get; set; }
        public decimal QuantiteReservee { get; set; }

        /// <summary>Quantité réellement disponible : quantité − réservée.</summary>
        public decimal QuantiteDisponible { get; set; }

        public string TypeStock { get; set; } = string.Empty;
        public bool EstValide { get; set; }

        /// <summary>Quantité ≤ seuil d'alerte de l'article.</summary>
        public bool EnAlerte { get; set; }

        public bool EstCritique { get; set; }
        public DateTime DateEntree { get; set; }
        public DateTime? DatePeremption { get; set; }

        public int? CommandeClientId { get; set; }
        public string? CommandeLibelle { get; set; }
        public int? ClientId { get; set; }
        public string? ClientLibelle { get; set; }
        public int? PlateformeId { get; set; }
        public string? PlateformeLibelle { get; set; }
    }

    /// <summary>
    /// Enveloppe paginée. <c>Total</c> porte le nombre de lignes correspondant aux
    /// filtres, pas celui de la page : le client peut calculer le nombre de pages.
    /// </summary>
    public class StockListeReponseDto
    {
        public List<StockListeDto> Items { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int Taille { get; set; }
        public int Pages => Taille <= 0 ? 0 : (int)Math.Ceiling(Total / (double)Taille);
    }
}
