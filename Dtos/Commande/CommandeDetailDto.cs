using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    public class CommandeDetailDto
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? DescriptionCommande { get; set; }
        public StatutCommande Statut { get; set; }
        public DateTime DateCommande { get; set; }
        public DateTime? DateLivraisonSouhaitee { get; set; }
        public int ClientId { get; set; }
        public decimal MontantTotal { get; set; }
        public string? Devise { get; set; }
        public decimal PourcentageRessourcesCouvertes { get; set; }
        public decimal? PrixFacon { get; set; }
        public string? NotesSpeciales { get; set; }
        public string? SpecificationsClient { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateMiseAJour { get; set; }
        public string? CreePar { get; set; }
        public string? ModifiePar { get; set; }
        public CommandeDetailClientDto? Client { get; set; }
        public List<BesoinCommandeDto> Besoins { get; set; } = new();
        public List<ConfigTailleItemDto> ConfigTailles { get; set; } = new();
        public List<BomLigneItemDto> BomLignes { get; set; } = new();
    }

    public class CommandeDetailClientDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Prenom { get; set; }
        public CommandePlateformeInfoDto? Plateforme { get; set; }
    }

    public class BesoinCommandeDto
    {
        public int Id { get; set; }
        public int CommandeClientId { get; set; }
        public int ArticleId { get; set; }
        public TypeBesoin TypeBesoin { get; set; }
        public string? Couleur { get; set; }
        public string? Taille { get; set; }
        public string? Dimension { get; set; }
        public decimal QuantiteUnitaire { get; set; }
        public int NombrePieces { get; set; }
        public decimal QuantiteTotale { get; set; }
        public decimal QuantiteCouverte { get; set; }
        public decimal QuantiteStockImporte { get; set; }
        public decimal QuantiteAchatsLocaux { get; set; }
        public decimal QuantiteStockLibre { get; set; }
        public bool EstCompletementCouvert { get; set; }
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; }
        public BesoinArticleDto? Article { get; set; }
    }

    public class BesoinArticleDto
    {
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal? Laize { get; set; }
    }

    public class ConfigTailleItemDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int Quantite { get; set; }
    }

    public class BomLigneItemDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public int ArticleId { get; set; }
        public decimal QuantiteParPiece { get; set; }
        public string? Unite { get; set; }
        public bool EstConsommableTissu { get; set; }
        public BesoinArticleDto? Article { get; set; }
    }
}
