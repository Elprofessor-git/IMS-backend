namespace Backend_Gestion_Magasin_API.Dtos.Stock
{
    // Lecture seule — consultation des réservations existantes avant une création
    // manuelle de stock (Bug 28). Scope résolu côté backend (lecture seule), aucun
    // impact sur PostStock / ReserverStock.
    public class ReservationStockDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleDesignation { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteReservee { get; set; }
        public int TypeStock { get; set; }

        // Scope résolu en lecture seule (null si non applicable)
        public string? EmplacementPhysique { get; set; }
        public int? CommandeClientId { get; set; }
        public string? CommandeLibelle { get; set; }
        public int? ClientId { get; set; }
        public string? ClientLibelle { get; set; }
        public int? PlateformeId { get; set; }
        public string? PlateformeLibelle { get; set; }
        public int? GroupeCommandeId { get; set; }
        public string? GroupeLibelle { get; set; }
    }
}
