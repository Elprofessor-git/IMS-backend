using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class CommandeService
    {
        private readonly ApplicationDbContext _context;

        public CommandeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CommandeClient>> GetAllCommandesAsync()
        {
            return await _context.CommandesClients
                .Include(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Include(c => c.Marque)
                .ThenInclude(m => m.Plateforme)
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
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

