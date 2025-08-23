using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class LigneImportation
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Importation")]
        public int ImportationId { get; set; }
        
        [ForeignKey("Article")]
        public int ArticleId { get; set; }
        
        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }
        
        [StringLength(200)]
        public string? Designation { get; set; }
        
        [StringLength(50)]
        public string? Couleur { get; set; }
        
        [StringLength(50)]
        public string? CodeCouleur { get; set; }
        
        [StringLength(100)]
        public string? Dimension { get; set; }
        
        [StringLength(100)]
        public string? Nature { get; set; }
        
        public decimal Quantite { get; set; } = 0;
        
        public decimal PrixUnitaire { get; set; } = 0;
        
        public decimal MontantLigne { get; set; } = 0; // Calcul automatique
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public bool EstAffecteStock { get; set; } = false;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        // Relations
        public virtual Importation Importation { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
        public virtual CommandeClient? CommandeClient { get; set; }
    }
}

