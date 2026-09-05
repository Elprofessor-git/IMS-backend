using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Facture
{
    public class FactureLigneInputDto
    {
        public int CommandeId { get; set; }
        public int Quantite { get; set; } = 1;
        public decimal PrixUnitaireFacon { get; set; }
    }

    public class FactureCommandeLigneDto
    {
        public int Id { get; set; }
        public int FactureId { get; set; }
        public int CommandeId { get; set; }
        public string? Modele { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public int Quantite { get; set; }
        public decimal PrixUnitaireFacon { get; set; }
        public decimal MontantLigne { get; set; }
    }

    public class FactureListDto
    {
        public int Id { get; set; }
        public string NumeroFacture { get; set; } = string.Empty;
        public DateTime DateFacture { get; set; }
        public int ClientId { get; set; }
        public string? ClientNom { get; set; }
        public string? Devise { get; set; }
        public decimal MontantTotal { get; set; }
        public StatutFacture Statut { get; set; }
    }

    public class FactureDetailDto
    {
        public int Id { get; set; }
        public string NumeroFacture { get; set; } = string.Empty;
        public DateTime DateFacture { get; set; }
        public int ClientId { get; set; }
        public string? ClientNom { get; set; }
        public string? ClientAdresse { get; set; }
        public string? Devise { get; set; }
        public string? ModePaiement { get; set; }
        public string? Rib { get; set; }
        public string? Iban { get; set; }
        public string? ModeLivraison { get; set; }
        public int? NombreColis { get; set; }
        public decimal? PoidsNetKg { get; set; }
        public decimal? PoidsBrutKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public string? Notes { get; set; }
        public StatutFacture Statut { get; set; }
        public DateTime DateCreation { get; set; }
        public string? CreePar { get; set; }
        public decimal MontantTotal { get; set; }
        public List<FactureCommandeLigneDto> Lignes { get; set; } = new();
    }

    public class CreateFactureDto
    {
        public DateTime? DateFacture { get; set; }
        public int ClientId { get; set; }
        public string? Devise { get; set; } = "EUR";
        public string? ModePaiement { get; set; }
        public string? Rib { get; set; }
        public string? Iban { get; set; }
        public string? ModeLivraison { get; set; }
        public int? NombreColis { get; set; }
        public decimal? PoidsNetKg { get; set; }
        public decimal? PoidsBrutKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public string? Notes { get; set; }
        public List<FactureLigneInputDto> Lignes { get; set; } = new();
    }

    public class UpdateFactureDto
    {
        public DateTime DateFacture { get; set; }
        public string? Devise { get; set; } = "EUR";
        public string? ModePaiement { get; set; }
        public string? Rib { get; set; }
        public string? Iban { get; set; }
        public string? ModeLivraison { get; set; }
        public int? NombreColis { get; set; }
        public decimal? PoidsNetKg { get; set; }
        public decimal? PoidsBrutKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public string? Notes { get; set; }
        public List<FactureLigneInputDto> Lignes { get; set; } = new();
    }
}