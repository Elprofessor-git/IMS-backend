using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeIntervention
    {
        Preventive,
        Corrective
    }

    /// <summary>
    /// Intervention de maintenance sur une machine du parc (journal d'intervention).
    /// Champ « PannneConstatee » : nom issu du cahier des charges fourni (sic).
    /// </summary>
    public class InterventionMachine
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Machine")]
        public int MachineId { get; set; }

        public virtual Machine Machine { get; set; } = null!;

        public TypeIntervention TypeIntervention { get; set; }

        public DateTime DateIntervention { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? PannneConstatee { get; set; }

        [StringLength(500)]
        public string? PiecesRemplacees { get; set; }

        public decimal? CoutIntervention { get; set; }

        public int? DureeImmobilisationHeures { get; set; }

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        public DateTime? ProchaineDateMaintenance { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}