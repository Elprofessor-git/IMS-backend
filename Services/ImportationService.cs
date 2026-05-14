using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Services
{
    public class ImportationService
    {
        private readonly ApplicationDbContext _context;

        public ImportationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Importation>> GetAllAsync()
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .ToListAsync();
        }

        public async Task<Importation?> GetByIdAsync(int id)
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Importation> AddAsync(Importation importation)
        {
            _context.Importations.Add(importation);
            await _context.SaveChangesAsync();
            return importation;
        }

        public async Task<bool> UpdateAsync(int id, Importation importation)
        {
            var existing = await _context.Importations.FindAsync(id);
            if (existing == null) return false;

            _context.Entry(existing).CurrentValues.SetValues(importation);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.Importations.FindAsync(id);
            if (existing == null) return false;

            _context.Importations.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
