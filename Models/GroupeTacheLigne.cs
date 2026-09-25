using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ligne d'un GroupeTache : gabarit d'une tâche à générer lors de l'application
    /// du groupe à une commande. L'ordre de définition est conservé (séquence de la gamme).
    /// </summary>
    public class GroupeTacheLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("GroupeTache")]
        public int GroupeTacheId { get; set; }

        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? EquipeAssignee { get; set; }

        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }

        public PrioriteTache Priorite { get; set; } = PrioriteTache.Normale;

        public int Ordre { get; set; }

        public int DureeEstimeeHeures { get; set; } = 0;

        // Relation
        public virtual GroupeTache? GroupeTache { get; set; }
    }
}