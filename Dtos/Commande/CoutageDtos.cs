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

        /// <summary>Coût de la ligne, exprimé en TND (devise de référence système).</summary>
        public decimal CoutLigne { get; set; }

        /// <summary>Taux TND appliqué à la devise de prix (1 si devise TND/nulle).</summary>
        public decimal TauxConvTND { get; set; } = 1m;

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
        /// <summary>Coût matière sommé en TND (devise de référence système).</summary>
        public decimal CoutTotalMatiere { get; set; }
        /// <summary>Coût façon en TND (converti depuis la devise de la commande).</summary>
        public decimal? CoutTotalFacon { get; set; }
        /// <summary>Coût total estimé en TND.</summary>
        public decimal CoutTotalGeneral { get; set; }
        public List<CoutageLigneDto> Lignes { get; set; } = new();
    }
}