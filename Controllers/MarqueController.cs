using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MarqueController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MarqueController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Marque?plateformeId=1
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marque>>> GetMarques([FromQuery] int? plateformeId)
        {
            var query = _context.Marques
                .Include(m => m.Plateforme)
                .AsQueryable();

            if (plateformeId.HasValue)
                query = query.Where(m => m.PlateformeId == plateformeId.Value);

            return await query.OrderBy(m => m.Nom).ToListAsync();
        }

        // GET: api/Marque/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Marque>> GetMarque(int id)
        {
            var marque = await _context.Marques
                .Include(m => m.Plateforme)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marque == null)
                return NotFound();

            return marque;
        }

        // POST: api/Marque
        [HttpPost]
        public async Task<ActionResult<Marque>> PostMarque(Marque marque)
        {
            marque.DateCreation = DateTime.UtcNow;
            _context.Marques.Add(marque);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMarque), new { id = marque.Id }, marque);
        }

        // PUT: api/Marque/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMarque(int id, Marque marque)
        {
            if (id != marque.Id)
                return BadRequest();

            _context.Entry(marque).State = EntityState.Modified;
            _context.Entry(marque).Property(m => m.DateCreation).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Marques.Any(m => m.Id == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Marque/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);
            if (marque == null)
                return NotFound();

            _context.Marques.Remove(marque);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
