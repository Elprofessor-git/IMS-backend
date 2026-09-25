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
        /// <summary>OF dont relève la coupe (module OF — Phase 1, optionnel).</summary>
        public int? OrdreFabricationId { get; set; }
        /// <summary>Matelas dont relève la coupe (coupe uniquement, optionnel).</summary>
        public int? MatelasId { get; set; }
        /// <summary>Chaîne de production exportatrice (export uniquement, optionnel).</summary>
        public int? ChaineProductionId { get; set; }
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
        /// <summary>OF dont relève la coupe (module OF — Phase 1, optionnel).</summary>
        public int? OrdreFabricationId { get; set; }
        public string? OrdreFabricationNumero { get; set; }
        /// <summary>Matelas dont relève la coupe (optionnel).</summary>
        public int? MatelasId { get; set; }
        public string? MatelasNumero { get; set; }
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
        /// <summary>Chaîne de production exportatrice (optionnel, SetNull).</summary>
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
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

    // ── Ordre de coupe document (L2) : le plan de coupe agrégé devient la
    //    référence atelier. Vue calculée, aucune table de planning. ──

    /// <summary>Ligne de plan d'un matelas, vue document (+ théorique = Occ × Plis).</summary>
    public class OrdreCoupePlanLigneDto
    {
        public int MatelasId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public int Theorique { get; set; }
    }

    /// <summary>Récapitulatif par taille d'un matelas (reste si coupes déjà faites).</summary>
    public class OrdreCoupeMatelasDto
    {
        public int MatelasId { get; set; }
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int OrdreDeCoupe { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Laize { get; set; }
        /// <summary>Total théorique du matelas = Σ Occurrences × PiecePliage.</summary>
        public int TotalTheorique { get; set; }
        /// <summary>Coupes réelles déjà rattachées à ce matelas (toutes tailles).</summary>
        public int TotalCoupeReelle { get; set; }
        public List<OrdreCoupePlanLigneDto> Lignes { get; set; } = new();
    }

    /// <summary>Couverture par taille : Σ plans vs demande ConfigTaille (± marge).</summary>
    public class OrdreCoupeTailleDto
    {
        public string Taille { get; set; } = string.Empty;
        public int QuantiteCommande { get; set; }
        /// <summary>Seuil = QuantiteCommande × (1 + MargeSecuriteDefaut/100).</summary>
        public decimal Seuil { get; set; }
        public int PlanTheorique { get; set; }
        public int CoupeReelle { get; set; }
        /// <summary>true si PlanTheorique &gt; Seuil — le plan déborde de la demande autorisée.</summary>
        public bool DepassePlan { get; set; }
        /// <summary>true si CoupeReelle &gt; Seuil — dépassement réel (garde=ForcerDepassement actif).</summary>
        public bool DepasseCoupe { get; set; }
        /// <summary>Coupes enregistrées sans matelas rattaché (alerte atelier).</summary>
        public int CoupesSansMatelas { get; set; }
    }

    public class OrdreDeCoupeDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public decimal MargeSecuriteDefaut { get; set; }
        public int TotalPlanTheorique { get; set; }
        public int TotalCoupeReelle { get; set; }
        public int TotalCoupesSansMatelas { get; set; }
        public List<OrdreCoupeMatelasDto> Matelas { get; set; } = new();
        public List<OrdreCoupeTailleDto> Tailles { get; set; } = new();
    }
}