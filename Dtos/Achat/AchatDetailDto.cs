using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Achat
{
    public class AchatDetailDto
    {
        public int Id { get; set; }
        public string NumeroAchat { get; set; } = string.Empty;
        public DateTime DateAchat { get; set; }
        public DateTime? DateLivraisonPrevue { get; set; }
        public DateTime? DateLivraisonReelle { get; set; }
        public StatutAchat Statut { get; set; }
        public decimal MontantTotal { get; set; }
        public decimal MontantTotalTND { get; set; }
        public string? Devise { get; set; }
        public string? ConditionsPaiement { get; set; }
        public TypePaiement? TypePaiement { get; set; }
        public string? NotesAchat { get; set; }
        public string? CreePar { get; set; }
        public int FournisseurId { get; set; }
        public int? CommandeClientId { get; set; }
        public AchatFournisseurDto? Fournisseur { get; set; }
        public AchatCommandeClientDto? CommandeClient { get; set; }
        public List<LigneAchatDto> LignesAchat { get; set; } = new();
    }

    public class LigneAchatDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteRecue { get; set; }
        public StatutLigneAchat StatutLigne { get; set; }
        public decimal PrixUnitaire { get; set; }
        public decimal MontantLigne { get; set; }
        public decimal MontantLigneTND { get; set; }
        public string? Devise { get; set; }
        public string? Unite { get; set; }
        public string? Couleur { get; set; }
        public string? CodeCouleur { get; set; }
        public string? Taille { get; set; }
        public string? Dimension { get; set; }
        public string? DescriptionSpecifique { get; set; }
        public string? Notes { get; set; }
        public TypeDestinationAchat TypeDestination { get; set; }
        public int? CommandeClientId { get; set; }
        public int? ClientId { get; set; }
        public int? PlateformeId { get; set; }
        public int? GroupeCommandeId { get; set; }
        public List<int> GroupeCommandeMembres { get; set; } = new();
        public bool EstAffecteStock { get; set; }
        public LigneAchatArticleDto? Article { get; set; }
    }

    public class LigneAchatArticleDto
    {
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }
}
