using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Designation { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [StringLength(50)]
        public string? Categorie { get; set; }
        
        [StringLength(50)]
        public string? SousCategorie { get; set; }
        
        [StringLength(50)]
        public string? Unite { get; set; } // mètre, pièce, kg, etc.
        
        [StringLength(100)]
        public string? Marque { get; set; }
        
        [StringLength(100)]
        public string? Reference { get; set; }
        
        [StringLength(1000)]
        public string? Caracteristiques { get; set; }
        
        public decimal PrixUnitaireMoyen { get; set; } = 0;
        
        public int SeuilAlerte { get; set; } = 0;
        
        public int SeuilCritique { get; set; } = 0;
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;

        [StringLength(2048)]
        public string? ImageUrl { get; set; }
        
        // Relations
        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public virtual ICollection<LigneAchat> LignesAchat { get; set; } = new List<LigneAchat>();
        public virtual ICollection<LigneImportation> LignesImportation { get; set; } = new List<LigneImportation>();
        public virtual ICollection<BesoinCommande> BesoinsCommande { get; set; } = new List<BesoinCommande>();
    }
}

