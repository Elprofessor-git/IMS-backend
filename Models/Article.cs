using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        
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
        public string? Unite { get; set; } // mètre, pièce, kg, etc.
        
        [StringLength(100)]
        public string? Marque { get; set; }
        
        [StringLength(100)]
        public string? Reference { get; set; }
        
        [StringLength(1000)]
        public string? Caracteristiques { get; set; }
        
        /// <summary>
        /// DERNIER prix unitaire connu de l'article (prix de référence).
        /// ATTENTION : le nom « Moyen » est historique — ce champ n'a jamais réellement
        /// stocké une moyenne calculée ; il contient le dernier prix saisi (création,
        /// ligne d'achat, ligne d'importation ou mise à jour manuelle via l'endpoint dédié).
        /// La colonne DB garde son nom d'origine (pas de migration destructive) ;
        /// chaque changement est tracé dans HistoriquePrixArticle.
        /// Ce prix n'intervient PAS dans le calcul de couverture BOM ni dans aucun workflow.
        /// </summary>
        public decimal PrixUnitaireMoyen { get; set; } = 0;
        
        public int SeuilAlerte { get; set; } = 0;
        
        public int SeuilCritique { get; set; } = 0;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;

        [StringLength(2048)]
        public string? ImageUrl { get; set; }
        
        /// <summary>
        /// Largeur du rouleau de tissu en mètres (uniquement pertinent pour les
        /// articles tissus, utilisée dans le module Rapport de coupe).
        /// Nullable : les non-tissus (boutons, fils…) n'ont pas de laize.
        /// </summary>
        public decimal? Laize { get; set; }

        /// <summary>
        /// Article parent (variante parent/enfant — module Fournitures).
        /// Null pour un article « racine » ; renseigné pour une variante
        /// (ex. même tissu décliné par taille/coloris).
        /// </summary>
        [ForeignKey("ArticleParent")]
        public int? ArticleParentId { get; set; }

        /// <summary>
        /// Taille de la variante (uniquement pertinent quand ArticleParentId
        /// est renseigné). Additive et nullable : aucun impact sur les
        /// articles existants.
        /// </summary>
        [StringLength(50)]
        public string? Taille { get; set; }

        // Relations
        public virtual Article? ArticleParent { get; set; }
        public virtual ICollection<Article> Variantes { get; set; } = new List<Article>();
        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public virtual ICollection<LigneAchat> LignesAchat { get; set; } = new List<LigneAchat>();
        public virtual ICollection<LigneImportation> LignesImportation { get; set; } = new List<LigneImportation>();
        public virtual ICollection<BesoinCommande> BesoinsCommande { get; set; } = new List<BesoinCommande>();
    }
}

