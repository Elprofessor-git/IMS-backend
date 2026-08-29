namespace Backend_Gestion_Magasin_API.Dtos.TauxChange
{
    public class TauxChangeDto
    {
        public int Id { get; set; }
        public string DeviseCode { get; set; } = string.Empty;
        public string? DeviseNom { get; set; }
        public string? DeviseSymbole { get; set; }
        public DateTime DateEffective { get; set; }
        public decimal Taux { get; set; }
    }
}
