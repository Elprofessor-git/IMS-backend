namespace Backend_Gestion_Magasin_API.Dtos.Mouvement
{
    // DTO de réponse : noms de champs alignés sur ce que le frontend lit
    // (totalEntrees/totalSorties/... au lieu de Entrees/Sorties/...).
    public class MouvementStatistiquesDto
    {
        public int TotalMouvements { get; set; }

        public int TotalEntrees { get; set; }

        public int TotalSorties { get; set; }

        public int TotalTransferts { get; set; }

        public int TotalAjustements { get; set; }

        public int TotalReservations { get; set; }

        public int TotalLiberations { get; set; }

        public decimal QuantiteTotaleEntree { get; set; }

        public decimal QuantiteTotaleSortie { get; set; }

        public decimal StockValeurTotale { get; set; }

        public List<MouvementParOrigineDto> MouvementsParOrigine { get; set; } = new();
    }

    public class MouvementParOrigineDto
    {
        public string Origine { get; set; } = string.Empty;
        public int Nombre { get; set; }
    }
}
