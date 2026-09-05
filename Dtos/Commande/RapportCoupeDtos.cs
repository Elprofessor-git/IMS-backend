namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    /// <summary>Création d'un lot de coupe ou d'export.</summary>
    public class CreateLotDto
    {
        public string Taille { get; set; } = string.Empty;
        public int QuantiteCoupee { get; set; }
        public int QuantiteExportee { get; set; }
        public bool ForcerDepassement { get; set; } = false;
        public string? Notes { get; set; }
    }

    public class LotCoupeDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int QuantiteCoupee { get; set; }
        public DateTime DateCoupe { get; set; }
        public string? EffectuePar { get; set; }
        public bool ForcerDepassement { get; set; }
        public string? Notes { get; set; }
    }

    public class LotExportDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int QuantiteExportee { get; set; }
        public DateTime DateExport { get; set; }
        public string? EffectuePar { get; set; }
        public bool ForcerDepassement { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Ligne taille du rapport de coupe.</summary>
    public class RapportCoupeTailleDto
    {
        public string Taille { get; set; } = string.Empty;
        public int QuantiteCommande { get; set; }
        public int QuantiteCoupee { get; set; }
        public int QuantiteExportee { get; set; }
        public bool DepassementCoupe { get; set; }
        public bool DepassementExport { get; set; }
    }

    /// <summary>Ligne tissu du rapport de coupe (calculée à la volée).</summary>
    public class RapportCoupeTissuDto
    {
        public int ArticleId { get; set; }
        public string Designation { get; set; } = string.Empty;
        public decimal? Laize { get; set; }
        /// <summary>Métrage annoncé = stock importé scopé commande/groupe (m).</summary>
        public decimal MetrageAnnonce { get; set; }
        /// <summary>Total pièces coupées toutes tailles.</summary>
        public int QuantiteCoupee { get; set; }
        /// <summary>Consommation réelle par pièce (BOM, m).</summary>
        public decimal ConsoReelle { get; set; }
        /// <summary>Métrage réel consommé = ConsoReelle × QuantiteCoupee.</summary>
        public decimal MetrageReelle { get; set; }
        /// <summary>Stock restant = MetrageAnnonce − MetrageReelle.</summary>
        public decimal StockRestant { get; set; }
    }

    public class RapportCoupeDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? ClientNom { get; set; }
        public int TotalQuantiteCommande { get; set; }
        public int TotalQuantiteCoupee { get; set; }
        public int TotalQuantiteExportee { get; set; }
        public List<RapportCoupeTailleDto> Tailles { get; set; } = new();
        public List<RapportCoupeTissuDto> Tissus { get; set; } = new();
    }
}