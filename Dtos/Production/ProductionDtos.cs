using Backend_Gestion_Magasin_API.Models;
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

    internal class CommandeInfo
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public DateTime DateCommande { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string? ClientNom { get; set; }
        public string? PlateformeNom { get; set; }
    }

    internal class EtapeInfo
    {
        public int CommandeId { get; set; }
        public TypeEtapeProduction TypeEtape { get; set; }
        public StatutTache Statut { get; set; }
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public DateTime DateCreation { get; set; }
    }

    internal class ExportInfo
    {
        public int CommandeId { get; set; }
        public int ChaineProductionId { get; set; }
        public int TotalExports { get; set; }
        public int PiecesExportees { get; set; }
    }

    public class ProductionDashboardDto
    {
        public DateTime Date { get; set; }
        public int NombreCommandes { get; set; }
        public int TotalEtapes { get; set; }
        public int EtapesEnCours { get; set; }
        public int EtapesBloquees { get; set; }
        public int EtapesTerminees { get; set; }
        public int CommandesEnRetard { get; set; }
        public List<ProductionDashboardCommandeDto> Commandes { get; set; } = new();
    }

    public class ProductionDashboardCommandeDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? ClientNom { get; set; }
        public string Statut { get; set; } = string.Empty;
        public DateTime DateCommande { get; set; }
        public int NombreEtapes { get; set; }
        public int EtapesEnCours { get; set; }
        public int EtapesBloquees { get; set; }
        public int EtapesTerminees { get; set; }
        public int EtapesAPlanifier { get; set; }
        public string? EtapeCouranteType { get; set; }
        public string? EtapeCouranteStatut { get; set; }
        public string? EtapeCouranteChaine { get; set; }
        public DateTime? EtapeCouranteDateFinPrevue { get; set; }
        public bool EnRetard { get; set; }
        public int Avancement { get; set; }
        public string? ChainePrincipaleNom { get; set; }
    }

    public class ProductionExportParChaineDto
    {
        public int ChaineProductionId { get; set; }
        public string ChaineNom { get; set; } = string.Empty;
        public int TotalExports { get; set; }
        public int PiecesExportees { get; set; }
    }

    public class ProductionJournalLigneDto
    {
        public int Id { get; set; }
        public int EtapeId { get; set; }
        public string? NumeroOF { get; set; }
        public int CommandeId { get; set; }
        public string? NumeroCommande { get; set; }
        public string TypeEtape { get; set; } = string.Empty;
        public string? AncienStatut { get; set; }
        public string NouveauStatut { get; set; } = string.Empty;
        public string? ChaineNom { get; set; }
        public DateTime DateTransition { get; set; }
        public string? EffectuePar { get; set; }
    }
}
