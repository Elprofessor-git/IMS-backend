using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Production
{
    public class CreateOrdreFabricationEtapeDto
    {
        [Required]
        public int OrdreFabricationId { get; set; }
        [Required]
        public string? TypeEtape { get; set; }      // Coupe | Production | ControleQualite | Expedition
        public string Statut { get; set; } = "NonCommence";  // vocabulaire StatutTache
        public int? ChaineProductionId { get; set; }
        public int? PlanningEntryId { get; set; }
        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateDebutReelle { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public decimal TempsTheoriqueHeures { get; set; }
        public decimal TempsReelHeures { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdateOrdreFabricationEtapeDto
    {
        public string? TypeEtape { get; set; }
        public string? Statut { get; set; }
        public int? ChaineProductionId { get; set; }
        public int? PlanningEntryId { get; set; }
        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateDebutReelle { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public decimal? TempsTheoriqueHeures { get; set; }
        public decimal? TempsReelHeures { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class OrdreFabricationEtapeDto
    {
        public int Id { get; set; }
        public int OrdreFabricationId { get; set; }
        public string? NumeroOF { get; set; }
        public int? CommandeId { get; set; }
        public string TypeEtape { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        public int? PlanningEntryId { get; set; }
        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateDebutReelle { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public decimal TempsTheoriqueHeures { get; set; }
        public decimal TempsReelHeures { get; set; }
        public string? ResponsableAssigne { get; set; }
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; }
    }
}