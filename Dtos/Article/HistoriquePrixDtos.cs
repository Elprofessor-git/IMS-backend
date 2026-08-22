using Backend_Gestion_Magasin_API.Models;
using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Article
{
    /// <summary>
    /// Corps du endpoint dédié PUT /api/Article/{id}/PrixUnitaire — mise à jour manuelle
    /// du prix de référence. Chaque changement crée une entrée HistoriquePrixArticle (Source=Manuel).
    /// </summary>
    public class UpdatePrixArticleDto
    {
        [Range(0, double.MaxValue)]
        public decimal PrixUnitaire { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; }
    }

    /// <summary>Lecture d'une entrée d'historique de prix (GET /api/Article/{id}/HistoriquePrix).</summary>
    public class ReadHistoriquePrixDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public decimal PrixUnitaire { get; set; }
        public string? Devise { get; set; }
        public DateTime DateEffective { get; set; }

        /// <summary>Source du prix, sérialisée en string (« Manuel », « LigneAchat », « LigneImportation »).</summary>
        public string Source { get; set; } = SourcePrix.Manuel.ToString();

        public int? LigneAchatId { get; set; }
        public int? LigneImportationId { get; set; }

        /// <summary>Numéro de l'achat d'origine si applicable (résolu côté serveur).</summary>
        public string? NumeroAchat { get; set; }

        /// <summary>Référence de l'importation d'origine si applicable (résolue côté serveur).</summary>
        public string? ReferenceImportation { get; set; }
    }
}
