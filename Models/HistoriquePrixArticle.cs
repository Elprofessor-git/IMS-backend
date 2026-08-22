using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Origine d'un prix enregistré dans HistoriquePrixArticle.
    /// Stocké en string en base (HasConversion&lt;string&gt;), sérialisé en string vers le frontend.
    /// </summary>
    public enum SourcePrix
    {
        /// <summary>Saisie manuelle (création d'article, endpoint dédié de mise à jour du prix).</summary>
        Manuel = 0,

        /// <summary>Prix issu d'une ligne d'achat (AjouterLigneAchat).</summary>
        LigneAchat = 1,

        /// <summary>Prix issu d'une ligne d'importation (AjouterLigneImportation).</summary>
        LigneImportation = 2
    }

    /// <summary>
    /// Entrée d'historique du prix unitaire d'un article. Chaque changement du prix de
    /// référence (Article.PrixUnitaireMoyen) crée une entrée ; la table conserve ainsi
    /// la trace des prix d'achat ET des prix d'importation dans le temps.
    /// </summary>
    public class HistoriquePrixArticle
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }
        public virtual Article Article { get; set; } = null!;

        /// <summary>Prix unitaire constaté à cette date.</summary>
        public decimal PrixUnitaire { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; }

        /// <summary>Date d'entrée en vigueur du prix (UTC, auto).</summary>
        public DateTime DateEffective { get; set; } = DateTime.UtcNow;

        public SourcePrix Source { get; set; } = SourcePrix.Manuel;

        /// <summary>Ligne d'achat à l'origine du prix (null si Source=Manuel ou Importation).</summary>
        public int? LigneAchatId { get; set; }
        public virtual LigneAchat? LigneAchat { get; set; }

        /// <summary>Ligne d'importation à l'origine du prix (null si Source=Manuel ou Achat).</summary>
        public int? LigneImportationId { get; set; }
        public virtual LigneImportation? LigneImportation { get; set; }
    }
}
