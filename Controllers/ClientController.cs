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
    public class ClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Client
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Client>>> GetClients()
        {
            return await _context.Clients
                .Include(c => c.Plateforme)
                .Where(c => c.EstActif)
                .ToListAsync();
        }

        // GET: api/Client/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Client>> GetClient(int id)
        {
            var client = await _context.Clients
                .Include(c => c.Plateforme)
                .Include(c => c.Commandes)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            return client;
        }

        // GET: api/Client/ByPlateforme/5
        [HttpGet("ByPlateforme/{plateformeId}")]
        public async Task<ActionResult<IEnumerable<Client>>> GetClientsByPlateforme(int plateformeId)
        {
            return await _context.Clients
                .Include(c => c.Plateforme)
                .Where(c => c.PlateformeId == plateformeId && c.EstActif)
                .ToListAsync();
        }

        // GET: api/Client/5/Historique
        [HttpGet("{id}/Historique")]
        public async Task<ActionResult<object>> GetHistoriqueClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            var historique = new
            {
                Client = client,
                Commandes = await _context.CommandesClients
                    .Where(c => c.ClientId == id)
                    .OrderByDescending(c => c.DateCommande)
                    .Select(c => new
                    {
                        c.Id,
                        c.NumeroCommande,
                        c.TitreCommande,
                        c.DateCommande,
                        c.Statut,
                        c.MontantTotal,
                        c.Devise
                    })
                    .ToListAsync(),
                Statistiques = new
                {
                    NombreCommandes = await _context.CommandesClients.CountAsync(c => c.ClientId == id),
                    MontantTotal = await _context.CommandesClients
                        .Where(c => c.ClientId == id)
                        .SumAsync(c => c.MontantTotal),
                    CommandesTerminees = await _context.CommandesClients
                        .CountAsync(c => c.ClientId == id && c.Statut == StatutCommande.Terminee),
                    DerniereCommande = await _context.CommandesClients
                        .Where(c => c.ClientId == id)
                        .OrderByDescending(c => c.DateCommande)
                        .Select(c => c.DateCommande)
                        .FirstOrDefaultAsync()
                }
            };

            return Ok(historique);
        }

        // POST: api/Client
        [HttpPost]
        public async Task<ActionResult<Client>> PostClient(Client client)
        {
            client.DateCreation = DateTime.Now;
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetClient", new { id = client.Id }, client);
        }

        // PUT: api/Client/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutClient(int id, Client client)
        {
            if (id != client.Id)
            {
                return BadRequest();
            }

            _context.Entry(client).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClientExists(id))
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

        // POST: api/Client/5/Desactiver
        [HttpPost("{id}/Desactiver")]
        public async Task<IActionResult> DesactiverClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            client.EstActif = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Client désactivé avec succès" });
        }

        // POST: api/Client/5/Activer
        [HttpPost("{id}/Activer")]
        public async Task<IActionResult> ActiverClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            client.EstActif = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Client activé avec succès" });
        }

        // DELETE: api/Client/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            // Vérifier s'il y a des commandes liées
            var hasCommandes = await _context.CommandesClients.AnyAsync(c => c.ClientId == id);
            if (hasCommandes)
            {
                return BadRequest("Impossible de supprimer le client car il a des commandes associées. Utilisez la désactivation à la place.");
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.Id == id);
        }
    }
}

