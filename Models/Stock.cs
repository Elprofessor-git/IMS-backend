using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeStock
    {
        Libre,
        Reserve,
        Importe
    }
    
    public class Stock
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Article")]
        public int ArticleId { get; set; }
        
        [StringLength(50)]
        public string? Couleur { get; set; }
        
        [StringLength(50)]
        public string? CodeCouleur { get; set; }
        
        [StringLength(50)]
        public string? Taille { get; set; }
        
        [StringLength(100)]
        public string? Dimension { get; set; }
        
        [StringLength(100)]
        public string? EmplacementPhysique { get; set; }
        
        [StringLength(50)]
        public string? NumeroLot { get; set; }
        
        public decimal Quantite { get; set; } = 0;
        
        public decimal QuantiteReservee { get; set; } = 0;
        
        public TypeStock TypeStock { get; set; } = TypeStock.Libre;
        
        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }
        
        public decimal PrixUnitaire { get; set; } = 0;
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        public DateTime DateEntree { get; set; } = DateTime.Now;
        
        public DateTime? DatePeremption { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public bool ValidationManuelleRequise { get; set; } = false;
        
        public bool EstValide { get; set; } = true;
        
        [StringLength(100)]
        public string? ValidePar { get; set; }
        
        public DateTime? DateValidation { get; set; }
        
        [ForeignKey("Client")]
        public int? ClientId { get; set; }

        [ForeignKey("Plateforme")]
        public int? PlateformeId { get; set; }

        // Relations
        public virtual Article Article { get; set; } = null!;
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual Client? Client { get; set; }
        public virtual Plateforme? Plateforme { get; set; }
        public virtual ICollection<MouvementStock> Mouvements { get; set; } = new List<MouvementStock>();
    }
}

