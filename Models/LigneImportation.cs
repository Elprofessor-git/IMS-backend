using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeDestinationImportation
    {
        Commande,
        Marque,
        Plateforme,
        StockLibre,
    }

    public enum StatutLigneImportation
    {
        EnAttente,
        PartielleEnCours,
        Complete,
        ClotureeForcee
    }

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
        
        public decimal QuantiteRecue { get; set; } = 0; // quantité déjà reçue (cumulée)
        
        public StatutLigneImportation StatutLigne { get; set; } = StatutLigneImportation.EnAttente;
        
        public decimal PrixUnitaire { get; set; } = 0;
        
        public decimal MontantLigne { get; set; } = 0; // Calcul automatique
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public bool EstAffecteStock { get; set; } = false;

        // Pas de notion d'origine par ligne : l'origine (Fournisseur direct ou Plateforme qui
        // regroupe plusieurs fournisseurs) est un attribut de l'IMPORTATION (en-tête), pas de la
        // ligne. Seule la DESTINATION varie par ligne (elle entre dans le calcul BOM/besoins).
        public TypeDestinationImportation TypeDestination { get; set; } = TypeDestinationImportation.StockLibre;

        [ForeignKey("Client")]
        public int? ClientId { get; set; }

        [ForeignKey("Plateforme")]
        public int? PlateformeId { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        // Relations
        public virtual Importation Importation { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
        public virtual CommandeClient? CommandeClient { get; set; }
        public virtual Client? Client { get; set; }
        public virtual Plateforme? Plateforme { get; set; }
    }
}

