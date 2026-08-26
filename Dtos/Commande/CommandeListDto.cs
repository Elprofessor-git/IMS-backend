using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    public class CommandeListDto
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public StatutCommande Statut { get; set; }
        public decimal PourcentageRessourcesCouvertes { get; set; }
        public DateTime? DateLivraisonSouhaitee { get; set; }
        public int ClientId { get; set; }
        public CommandeClientInfoDto? Client { get; set; }
    }

    public class CommandeClientInfoDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public CommandePlateformeInfoDto? Plateforme { get; set; }
    }

    public class CommandePlateformeInfoDto
    {
        public string Nom { get; set; } = string.Empty;
    }
}
