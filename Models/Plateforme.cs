using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Plateforme
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [StringLength(200)]
        public string? SiteWeb { get; set; }
        
        [StringLength(100)]
        public string? ContactEmail { get; set; }
        
        [StringLength(20)]
        public string? Telephone { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
        public virtual ICollection<Marque> Marques { get; set; } = new List<Marque>();
    }
}

