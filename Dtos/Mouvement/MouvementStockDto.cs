using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Mouvement
{
    public class MouvementArticleDto
    {
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }

    // DTO de réponse : forme aplatie que le frontend attend réellement.
    // (L'entité MouvementStock expose l'article imbriqué sous Stock ; le frontend lit article/articleId au niveau racine.)
    public class MouvementStockDto
    {
        public int Id { get; set; }

        public int StockId { get; set; }

        public int ArticleId { get; set; }

        public TypeMouvement TypeMouvement { get; set; }

        public OrigineMouvement OrigineMouvement { get; set; }

        public decimal Quantite { get; set; }

        public decimal StockAvant { get; set; }

        public decimal StockApres { get; set; }

        public string? Motif { get; set; }

        public string? NumeroReference { get; set; }

        public string? EmplacementSource { get; set; }

        public string? EmplacementDestination { get; set; }

        public string? EffectuePar { get; set; }

        public DateTime DateMouvement { get; set; }

        public MouvementArticleDto? Article { get; set; }
    }
}
