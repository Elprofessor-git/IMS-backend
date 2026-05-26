using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ModeleBomController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ModeleBomController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ModeleBom
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ModeleBom>>> GetModeleBoms()
        {
            return await _context.ModeleBoms
                .Include(m => m.Fournitures)
                    .ThenInclude(f => f.Article)
                .ToListAsync();
        }

        // GET: api/ModeleBom/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ModeleBom>> GetModeleBom(int id)
        {
            var modele = await _context.ModeleBoms
                .Include(m => m.Fournitures)
                    .ThenInclude(f => f.Article)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (modele == null)
                return NotFound();

            return modele;
        }

        // POST: api/ModeleBom
        [HttpPost]
        public async Task<ActionResult<ModeleBom>> CreateModeleBom(ModeleBom modele)
        {
            _context.ModeleBoms.Add(modele);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetModeleBom), new { id = modele.Id }, modele);
        }

        // PUT: api/ModeleBom/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateModeleBom(int id, ModeleBom modele)
        {
            if (id != modele.Id)
                return BadRequest();

            var existing = await _context.ModeleBoms
                .Include(m => m.Fournitures)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existing == null)
                return NotFound();

            existing.Nom = modele.Nom;
            existing.Description = modele.Description;

            // Remplacer les fournitures
            _context.FournituresBom.RemoveRange(existing.Fournitures);
            existing.Fournitures = modele.Fournitures;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ModeleBomExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/ModeleBom/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteModeleBom(int id)
        {
            var modele = await _context.ModeleBoms.FindAsync(id);
            if (modele == null)
                return NotFound();

            _context.ModeleBoms.Remove(modele);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ModeleBomExists(int id) =>
            _context.ModeleBoms.Any(e => e.Id == id);
    }
}
