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
    public class PlateformeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PlateformeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Plateforme
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Plateforme>>> GetPlateformes()
        {
            return await _context.Plateformes
                .Where(p => p.EstActif)
                .Include(p => p.Clients)
                .ToListAsync();
        }

        // GET: api/Plateforme/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Plateforme>> GetPlateforme(int id)
        {
            var plateforme = await _context.Plateformes
                .Include(p => p.Clients)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (plateforme == null)
            {
                return NotFound();
            }

            return plateforme;
        }

        // POST: api/Plateforme
        [HttpPost]
        public async Task<ActionResult<Plateforme>> PostPlateforme(Plateforme plateforme)
        {
            plateforme.DateCreation = DateTime.Now;
            _context.Plateformes.Add(plateforme);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPlateforme", new { id = plateforme.Id }, plateforme);
        }

        // PUT: api/Plateforme/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPlateforme(int id, Plateforme plateforme)
        {
            if (id != plateforme.Id)
            {
                return BadRequest();
            }

            _context.Entry(plateforme).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PlateformeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Plateforme/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlateforme(int id)
        {
            var plateforme = await _context.Plateformes.FindAsync(id);
            if (plateforme == null)
            {
                return NotFound();
            }

            // Vérifier s'il y a des clients liés
            var hasClients = await _context.Clients.AnyAsync(c => c.PlateformeId == id);
            if (hasClients)
            {
                return BadRequest("Impossible de supprimer la plateforme car elle a des clients associés.");
            }

            _context.Plateformes.Remove(plateforme);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PlateformeExists(int id)
        {
            return _context.Plateformes.Any(e => e.Id == id);
        }
    }
}

