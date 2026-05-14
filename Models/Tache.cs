using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Tache
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Titre { get; set; } = string.Empty;
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public DateTime? DateEcheance { get; set; }
        
        public bool EstTerminee { get; set; } = false;
        
        [StringLength(100)]
        public string? Assignee { get; set; }
        
        public int Priorite { get; set; } = 1; // 1=Faible, 2=Moyenne, 3=Élevée
    }
}
