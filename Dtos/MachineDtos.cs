using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    public class MachineDto
    {
        public int Id { get; set; }
        public string CodeMachine { get; set; } = string.Empty;
        public string? Marque { get; set; }
        public string? Modele { get; set; }
        public string? NumeroSerie { get; set; }
        public string TypeMachine { get; set; } = string.Empty;
        public DateTime? DateAcquisition { get; set; }
        public string? Emplacement { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int NombreInterventions { get; set; }
    }

    public class MachineDetailDto : MachineDto
    {
        public ICollection<InterventionMachineDto> Interventions { get; set; } = new List<InterventionMachineDto>();
    }

    public class CreateMachineDto
    {
        [Required]
        [StringLength(50)]
        public string CodeMachine { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Marque { get; set; }

        [StringLength(100)]
        public string? Modele { get; set; }

        [StringLength(100)]
        public string? NumeroSerie { get; set; }

        [Required]
        public string TypeMachine { get; set; } = string.Empty;

        public DateTime? DateAcquisition { get; set; }

        [StringLength(100)]
        public string? Emplacement { get; set; }

        public string Statut { get; set; } = "EnService";

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdateMachineDto
    {
        [StringLength(50)]
        public string? CodeMachine { get; set; }

        [StringLength(100)]
        public string? Marque { get; set; }

        [StringLength(100)]
        public string? Modele { get; set; }

        [StringLength(100)]
        public string? NumeroSerie { get; set; }

        public string? TypeMachine { get; set; }

        public DateTime? DateAcquisition { get; set; }

        [StringLength(100)]
        public string? Emplacement { get; set; }

        public string? Statut { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class InterventionMachineDto
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public string TypeIntervention { get; set; } = string.Empty;
        public DateTime DateIntervention { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PannneConstatee { get; set; }
        public string? PiecesRemplacees { get; set; }
        public decimal? CoutIntervention { get; set; }
        public int? DureeImmobilisationHeures { get; set; }
        public string? EffectuePar { get; set; }
        public DateTime? ProchaineDateMaintenance { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateInterventionMachineDto
    {
        [Required]
        public string TypeIntervention { get; set; } = string.Empty;

        public DateTime? DateIntervention { get; set; }

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