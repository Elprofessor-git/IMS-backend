using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class CommandeProjection
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public StatutCommande Statut { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateLivraisonSouhaitee { get; set; }
        public decimal MontantTotal { get; set; }
        public decimal PourcentageRessourcesCouvertes { get; set; }
        public CommandeClientNom? Client { get; set; }
    }

    public class CommandeClientNom
    {
        public string Nom { get; set; } = string.Empty;
        public string? NomEntreprise { get; set; }
        public CommandePlateformeNom? Plateforme { get; set; }
    }

    public class CommandePlateformeNom
    {
        public string Nom { get; set; } = string.Empty;
    }

    public class CommandeService
    {
        private readonly ApplicationDbContext _context;

        public CommandeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CommandeProjection>> GetAllCommandesAsync()
        {
            return await _context.CommandesClients
                .Select(c => new CommandeProjection
                {
                    Id = c.Id,
                    NumeroCommande = c.NumeroCommande,
                    TitreCommande = c.TitreCommande,
                    Statut = c.Statut,
                    DateCreation = c.DateCreation,
                    DateLivraisonSouhaitee = c.DateLivraisonSouhaitee,
                    MontantTotal = c.MontantTotal,
                    PourcentageRessourcesCouvertes = c.PourcentageRessourcesCouvertes,
                    Client = c.Client != null ? new CommandeClientNom
                    {
                        Nom = c.Client.Nom,
                        NomEntreprise = c.Client.NomEntreprise,
                        Plateforme = c.Client.Plateforme != null ? new CommandePlateformeNom
                        {
                            Nom = c.Client.Plateforme.Nom
                        } : null
                    } : null
                })
                .ToListAsync();
        }

        public async Task<CommandeClient?> GetCommandeByIdAsync(int id)
        {
            return await _context.CommandesClients
                .Include(c => c.Client)
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task AddCommandeAsync(CommandeClient commande)
        {
            _context.CommandesClients.Add(commande);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateCommandeAsync(CommandeClient commande)
        {
            _context.CommandesClients.Update(commande);
            int affectedRows = await _context.SaveChangesAsync();
            return affectedRows > 0;
        }

        public async Task<bool> DeleteCommandeAsync(int id)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null) return false;

            _context.CommandesClients.Remove(commande);
            int affectedRows = await _context.SaveChangesAsync();
            return affectedRows > 0;
        }
    }
}

