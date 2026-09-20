using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Catalogue « Article ↔ Fournisseur » (§5.2 du document maître) : un même article
    /// physique peut être acheté chez plusieurs fournisseurs, à référence/prix/délai
    /// différents, sans dupliquer la fiche Article. Un seul Article canonique porte
    /// plusieurs ArticleFournisseur (multi-sourcing).
    /// </summary>
    public class ArticleFournisseur
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        [ForeignKey("Fournisseur")]
        public int FournisseurId { get; set; }

        /// <summary>Référence de l'article chez CE fournisseur (différente de la Référence interne).</summary>
        [StringLength(100)]
        public string? ReferenceFournisseur { get; set; }

        /// <summary>Prix habituellement pratiqué par ce fournisseur pour cet article.</summary>
        public decimal PrixHabituel { get; set; } = 0;

        /// <summary>Délai d'approvisionnement habituel (jours) chez ce fournisseur.</summary>
        public int? DelaiApprovisionnementJours { get; set; }

        public bool EstActif { get; set; } = true;

        // Relations
        public virtual Article Article { get; set; } = null!;
        public virtual Fournisseur Fournisseur { get; set; } = null!;
    }
}