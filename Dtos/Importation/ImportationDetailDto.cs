using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    public class ImportationDetailDto
    {
        public int Id { get; set; }
        public string ReferenceImportation { get; set; } = string.Empty;
        public int? FournisseurId { get; set; }
        public int? PlateformeId { get; set; }
        public StatutImportation Statut { get; set; }
        public DateTime DateImportation { get; set; }
        public DateTime? DateReceptionPrevue { get; set; }
        public DateTime? DateReceptionReelle { get; set; }
        public ModeExpedition ModeExpedition { get; set; }
        public decimal MontantTotal { get; set; }
        public string? Devise { get; set; }
        public string? NotesImportation { get; set; }
        public string? CreePar { get; set; }
        public string? ModifiePar { get; set; }
        public ImportationFournisseurDto? Fournisseur { get; set; }
        public ImportationPlateformeDto? Plateforme { get; set; }
        public List<LigneImportationDto> LignesImportation { get; set; } = new();
    }

    public class ImportationFournisseurDto
    {
        public int Id { get; set; }
        public string NomEntreprise { get; set; } = string.Empty;
    }

    public class ImportationPlateformeDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
    }

    public class LigneImportationDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public int? CommandeClientId { get; set; }
        public int? ClientId { get; set; }
        public int? PlateformeId { get; set; }
        public int? GroupeCommandeId { get; set; }
        public List<int> GroupeCommandeMembres { get; set; } = new();
        public TypeDestinationImportation TypeDestination { get; set; }
        public string? Designation { get; set; }
        public string? Couleur { get; set; }
        public string? CodeCouleur { get; set; }
        public string? Dimension { get; set; }
        public string? Nature { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteRecue { get; set; }
        public StatutLigneImportation StatutLigne { get; set; }
        public decimal PrixUnitaire { get; set; }
        public decimal MontantLigne { get; set; }
        public string? Devise { get; set; }
        public string? Unite { get; set; }
        public bool EstAffecteStock { get; set; }
        public ImportationLigneArticleDto? Article { get; set; }
    }

    public class ImportationLigneArticleDto
    {
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }
}
