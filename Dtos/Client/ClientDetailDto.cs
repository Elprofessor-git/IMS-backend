namespace Backend_Gestion_Magasin_API.Dtos.Client
{
    public class ClientDetailDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Prenom { get; set; }
        public string? NomEntreprise { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public string? CodePostal { get; set; }
        public string? Pays { get; set; }
        public string? PreferencesTissus { get; set; }
        public string? NotesHistorique { get; set; }
        public int PlateformeId { get; set; }
        public bool EstActif { get; set; }
        public DateTime DateCreation { get; set; }
    }
}
