using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeDestinationAchat
    {
        Commande,
        Marque,
        Plateforme,
        StockLibre,
    }

    public enum StatutLigneAchat
    {
        EnAttente,
        PartielleEnCours,
        Complete,
        ClotureeForcee
    }

    public class LigneAchat
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Achat")]
        public int AchatId { get; set; }
        
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
        
        public decimal Quantite { get; set; } = 0;
        
        public decimal QuantiteRecue { get; set; } = 0; // quantité déjà reçue (cumulée)
        
        public StatutLigneAchat StatutLigne { get; set; } = StatutLigneAchat.EnAttente;
        
        public decimal PrixUnitaire { get; set; } = 0;
        
        public decimal MontantLigne { get; set; } = 0; // Calcul automatique
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        [StringLength(500)]
        public string? DescriptionSpecifique { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public TypeDestinationAchat TypeDestination { get; set; } = TypeDestinationAchat.StockLibre;

        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }

        [ForeignKey("Client")]
        public int? ClientId { get; set; }

        [ForeignKey("Plateforme")]
        public int? PlateformeId { get; set; }

        // Relations
        public virtual Achat Achat { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual Client? Client { get; set; }
        public virtual Plateforme? Plateforme { get; set; }
    }
}

