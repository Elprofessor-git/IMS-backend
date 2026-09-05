using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Article
{
    public class CreateArticleDto
    {
        [Required]
        [StringLength(100)]
        public string Designation { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Categorie { get; set; }

        [StringLength(50)]
        public string? SousCategorie { get; set; }

        [StringLength(50)]
        public string? Unite { get; set; }

        [StringLength(100)]
        public string? Marque { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        [StringLength(1000)]
        public string? Caracteristiques { get; set; }

        /// <summary>Largeur du rouleau de tissu en mètres (nullable, non-tissus = null).</summary>
        public decimal? Laize { get; set; }

        public int SeuilAlerte { get; set; } = 0;

        public int SeuilCritique { get; set; } = 0;

        /// <summary>
        /// Prix de référence initial (« dernier prix connu », voir Article.PrixUnitaireMoyen).
        /// Si > 0, une première entrée HistoriquePrixArticle (Source=Manuel) est créée.
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal PrixUnitaireMoyen { get; set; } = 0;

        /// <summary>Devise associée au prix initial (optionnel).</summary>
        [StringLength(10)]
        public string? Devise { get; set; }
    }
}
