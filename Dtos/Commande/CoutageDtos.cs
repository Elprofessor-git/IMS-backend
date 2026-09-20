namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    /// <summary>Ligne de coûtage par article de la BOM (coût matière).</summary>
    public class CoutageLigneDto
    {
        public int ArticleId { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal QuantiteParPiece { get; set; }
        public decimal QuantiteTotale { get; set; }
        public decimal PrixUnitaire { get; set; }
        public string? Devise { get; set; }
        public decimal CoutLigne { get; set; }

        /// <summary>Origine du prix retenu : "Historique" (dernier prix connu) ou "Article" (prix de référence).</summary>
        public string SourcePrix { get; set; } = "Historique";
    }

    /// <summary>Coûtage d'une commande par style (matière via BOM + façon).</summary>
    public class CoutageCommandeDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? DeviseCommande { get; set; }
        public decimal TotalPieces { get; set; }
        public decimal? PrixFacon { get; set; }
        public decimal CoutTotalMatiere { get; set; }
        public decimal? CoutTotalFacon { get; set; }
        public decimal CoutTotalGeneral { get; set; }
        public List<CoutageLigneDto> Lignes { get; set; } = new();
    }
}