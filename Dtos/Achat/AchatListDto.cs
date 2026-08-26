using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Achat
{
    public class AchatListDto
    {
        public int Id { get; set; }
        public string NumeroAchat { get; set; } = string.Empty;
        public DateTime DateAchat { get; set; }
        public StatutAchat Statut { get; set; }
        public decimal MontantTotal { get; set; }
        public string? Devise { get; set; }
        public AchatFournisseurDto? Fournisseur { get; set; }
        public AchatCommandeClientDto? CommandeClient { get; set; }
    }

    public class AchatFournisseurDto
    {
        public int Id { get; set; }
        public string NomEntreprise { get; set; } = string.Empty;
    }

    public class AchatCommandeClientDto
    {
        public int Id { get; set; }
        public string? NumeroCommande { get; set; }
        public string? TitreCommande { get; set; }
        public AchatClientDto? Client { get; set; }
    }

    public class AchatClientDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public AchatPlateformeDto? Plateforme { get; set; }
    }

    public class AchatPlateformeDto
    {
        public string Nom { get; set; } = string.Empty;
    }
}
