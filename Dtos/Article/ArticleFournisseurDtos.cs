using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Article
{
    /// <summary>Lecture d'un lien Article ↔ Fournisseur (catalogue multi-sourcing §5.2).</summary>
    public class ArticleFournisseurDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public int FournisseurId { get; set; }
        public string? ReferenceFournisseur { get; set; }
        public decimal PrixHabituel { get; set; }
        public int? DelaiApprovisionnementJours { get; set; }
        public bool EstActif { get; set; }
        public FournisseurInfoDto? Fournisseur { get; set; }
    }

    public class FournisseurInfoDto
    {
        public int Id { get; set; }
        public string NomEntreprise { get; set; } = string.Empty;
    }

    /// <summary>Création d'un lien Article ↔ Fournisseur.</summary>
    public class CreateArticleFournisseurDto
    {
        [Required]
        public int FournisseurId { get; set; }

        [StringLength(100)]
        public string? ReferenceFournisseur { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PrixHabituel { get; set; }

        [Range(0, 3650)]
        public int? DelaiApprovisionnementJours { get; set; }

        public bool EstActif { get; set; } = true;
    }

    /// <summary>Mise à jour d'un lien Article ↔ Fournisseur.</summary>
    public class UpdateArticleFournisseurDto
    {
        [StringLength(100)]
        public string? ReferenceFournisseur { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PrixHabituel { get; set; }

        [Range(0, 3650)]
        public int? DelaiApprovisionnementJours { get; set; }

        public bool EstActif { get; set; } = true;
    }
}