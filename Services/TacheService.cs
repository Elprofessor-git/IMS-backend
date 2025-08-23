using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class TacheService
    {
        private readonly ApplicationDbContext _context;

        public TacheService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tache>> GetAllTaches()
        {
            return await _context.Taches.ToListAsync();
        }

        public async Task<Tache?> GetTacheById(int id)
        {
            return await _context.Taches.FindAsync(id);
        }

        public async Task AddTache(Tache tache)
        {
            _context.Taches.Add(tache);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTache(Tache tache)
        {
            _context.Taches.Update(tache);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTache(int id)
        {
            var tache = await _context.Taches.FindAsync(id);
            if (tache != null)
            {
                _context.Taches.Remove(tache);
                await _context.SaveChangesAsync();
            }
        }
    }
}

