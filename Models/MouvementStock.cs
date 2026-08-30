using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeMouvement
    {
        Entree,
        Sortie,
        Transfert,
        Ajustement,
        Reservation,
        Liberation
    }
    
    public enum OrigineMouvement
    {
        Achat,
        Importation,
        Production,
        Ajustement,
        Transfert,
        Commande,
        Retour,
        CorrectionReception,
        Autre
    }
    
    public class MouvementStock
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Stock")]
        public int StockId { get; set; }
        
        [ForeignKey("TacheProduction")]
        public int? TacheProductionId { get; set; }
        
        public TypeMouvement TypeMouvement { get; set; }
        
        public OrigineMouvement OrigineMouvement { get; set; }
        
        public decimal Quantite { get; set; } = 0;
        
        public decimal QuantiteAvant { get; set; } = 0;
        
        public decimal QuantiteApres { get; set; } = 0;
        
        [StringLength(100)]
        public string? EmplacementSource { get; set; }
        
        [StringLength(100)]
        public string? EmplacementDestination { get; set; }
        
        [StringLength(50)]
        public string? NumeroLot { get; set; }
        
        [StringLength(1000)]
        public string? Motif { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        [StringLength(500)]
        public string? DocumentReference { get; set; }
        
        public DateTime DateMouvement { get; set; } = DateTime.Now;
        
        [StringLength(100)]
        public string? EffectuePar { get; set; }
        
        [StringLength(100)]
        public string? ValidePar { get; set; }
        
        public DateTime? DateValidation { get; set; }
        
        // Relations
        public virtual Stock Stock { get; set; } = null!;
        public virtual TacheProduction? TacheProduction { get; set; }
    }
}

