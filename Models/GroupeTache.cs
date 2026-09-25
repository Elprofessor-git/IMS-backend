using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Groupe de tâches répétitif : un modèle réutilisable de tâches de production,
    /// applicable à une commande pour générer une TacheProduction par ligne.
    /// (Collection Gebo — module « Tâches ».)
    /// </summary>
    public class GroupeTache
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool EstActif { get; set; } = true;

        public DateTime DateCreation { get; set; } = DateTime.Now;

        // Relations
        public virtual ICollection<GroupeTacheLigne> Lignes { get; set; } = new List<GroupeTacheLigne>();
        public virtual ICollection<TacheProduction> Taches { get; set; } = new List<TacheProduction>();
    }
}