using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    // Génération d'une tâche d'application : titre + description des lignes du groupe
    // copiés, avec CommandeClientId renseigné. groupKey : GroupeTacheId d'origine.
    public class GroupeTacheLigneDto
    {
        public int Id { get; set; }
        public int GroupeTacheId { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? EquipeAssignee { get; set; }
        public string? ResponsableAssigne { get; set; }
        public int Priorite { get; set; }
        public int Ordre { get; set; }
        public int DureeEstimeeHeures { get; set; }
    }

    public class GroupeTacheDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool EstActif { get; set; }
        public DateTime DateCreation { get; set; }
        public List<GroupeTacheLigneDto> Lignes { get; set; } = new();
        /// <summary>Nombre de commandes distinctes ayant reçu ce groupe (via tâches générées).</summary>
        public int NombreCommandesAppliquees { get; set; }
        /// <summary>Nombre total de tâches générées depuis ce groupe.</summary>
        public int NombreTachesGenerees { get; set; }
    }

    public class CreateGroupeTacheDto
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Description { get; set; }
    }

    public class UpdateGroupeTacheDto
    {
        [StringLength(100)]
        public string? Nom { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        public bool? EstActif { get; set; }
    }

    public class CreateGroupeTacheLigneDto
    {
        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Description { get; set; }
        [StringLength(100)]
        public string? EquipeAssignee { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        public int Priorite { get; set; }
        public int Ordre { get; set; }
        public int DureeEstimeeHeures { get; set; }
    }

    public class UpdateGroupeTacheLigneDto
    {
        [StringLength(100)]
        public string? Titre { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        [StringLength(100)]
        public string? EquipeAssignee { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        public int? Priorite { get; set; }
        public int? Ordre { get; set; }
        public int? DureeEstimeeHeures { get; set; }
    }

    public class AppliquerGroupeTacheDto
    {
        public int CommandeId { get; set; }
    }
}