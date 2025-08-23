using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeBesoin
    {
        MatierePremiere,
        Accessoire,
        Emballage,
        Autre
    }
    
    public class BesoinCommande
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("CommandeClient")]
        public int CommandeClientId { get; set; }
        
        [ForeignKey("Article")]
        public int ArticleId { get; set; }
        
        public TypeBesoin TypeBesoin { get; set; } = TypeBesoin.MatierePremiere;
        
        [StringLength(50)]
        public string? Couleur { get; set; }
        
        [StringLength(50)]
        public string? Taille { get; set; }
        
        [StringLength(100)]
        public string? Dimension { get; set; }
        
        public decimal QuantiteUnitaire { get; set; } = 0; // Besoin par pièce
        
        public int NombrePieces { get; set; } = 0; // Nombre de pièces à produire
        
        public decimal QuantiteTotale { get; set; } = 0; // Calcul automatique
        
        public decimal QuantiteCouverte { get; set; } = 0; // Quantité déjà couverte
        
        public decimal QuantiteStockImporte { get; set; } = 0;
        
        public decimal QuantiteAchatsLocaux { get; set; } = 0;
        
        public decimal QuantiteStockLibre { get; set; } = 0;
        
        public bool EstCompletementCouvert { get; set; } = false;
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        // Relations
        public virtual CommandeClient CommandeClient { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
    }
}

