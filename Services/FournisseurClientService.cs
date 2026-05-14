using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class FournisseurClientService
    {
        private readonly ApplicationDbContext _context;

        public FournisseurClientService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<FournisseurClient>> GetAllAsync()
        {
            return await _context.FournisseurClients.ToListAsync();
        }

        public async Task<FournisseurClient?> GetByIdAsync(int id)
        {
            return await _context.FournisseurClients.FindAsync(id);
        }

        public async Task<FournisseurClient> AddAsync(FournisseurClient fournisseurClient)
        {
            _context.FournisseurClients.Add(fournisseurClient);
            await _context.SaveChangesAsync();
            return fournisseurClient;
        }

        public async Task<bool> UpdateAsync(int id, FournisseurClient fournisseurClient)
        {
            var existing = await _context.FournisseurClients.FindAsync(id);
            if (existing == null) return false;

            _context.Entry(existing).CurrentValues.SetValues(fournisseurClient);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.FournisseurClients.FindAsync(id);
            if (existing == null) return false;

            _context.FournisseurClients.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

