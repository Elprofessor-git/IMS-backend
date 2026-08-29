using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FournisseurController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FournisseurController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("fournisseurs", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Fournisseur>>> GetFournisseurs()
        {
            return await _context.Fournisseurs
                .Where(f => f.EstActif)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("fournisseurs", requireWrite: false)]
        public async Task<ActionResult<Fournisseur>> GetFournisseur(int id)
        {
            var fournisseur = await _context.Fournisseurs
                .Include(f => f.Achats)
                .Include(f => f.Importations)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fournisseur == null)
            {
                return NotFound();
            }

            return fournisseur;
        }

        [HttpGet("{id}/Historique")]
        [RequireModulePermission("fournisseurs", requireWrite: false)]
        public async Task<ActionResult<object>> GetHistoriqueFournisseur(int id)
        {
            var fournisseur = await _context.Fournisseurs.FindAsync(id);
            if (fournisseur == null)
            {
                return NotFound();
            }

            var historique = new
            {
                Fournisseur = fournisseur,
                Achats = await _context.Achats
                    .Include(a => a.CommandeClient)
                    .ThenInclude(c => c.Client)
                    .Where(a => a.FournisseurId == id)
                    .OrderByDescending(a => a.DateAchat)
                    .Select(a => new
                    {
                        a.Id,
                        a.NumeroAchat,
                        a.DateAchat,
                        a.Statut,
                        a.MontantTotal,
                        a.Devise,
                        CommandeClient = a.CommandeClient.NumeroCommande,
                        Client = a.CommandeClient.Client.Nom
                    })
                    .ToListAsync(),
                Importations = await _context.Importations
                    .Where(i => i.FournisseurId == id)
                    .OrderByDescending(i => i.DateImportation)
                    .Select(i => new
                    {
                        i.Id,
                        i.ReferenceImportation,
                        i.DateImportation,
                        i.Statut,
                        i.MontantTotal,
                        i.Devise,
                        i.ModeExpedition
                    })
                    .ToListAsync(),
                Statistiques = new
                {
                    NombreAchats = await _context.Achats.CountAsync(a => a.FournisseurId == id),
                    NombreImportations = await _context.Importations.CountAsync(i => i.FournisseurId == id),
                    MontantTotalAchats = await _context.Achats
                        .Where(a => a.FournisseurId == id)
                        .SumAsync(a => a.MontantTotalTND),
                    MontantTotalImportations = await _context.Importations
                        .Where(i => i.FournisseurId == id)
                        .SumAsync(i => i.MontantTotalTND),
                    DernierAchat = await _context.Achats
                        .Where(a => a.FournisseurId == id)
                        .OrderByDescending(a => a.DateAchat)
                        .Select(a => a.DateAchat)
                        .FirstOrDefaultAsync(),
                    DerniereImportation = await _context.Importations
                        .Where(i => i.FournisseurId == id)
                        .OrderByDescending(i => i.DateImportation)
                        .Select(i => i.DateImportation)
                        .FirstOrDefaultAsync()
                }
            };

            return Ok(historique);
        }

        [HttpGet("Search/{terme}")]
        [RequireModulePermission("fournisseurs", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Fournisseur>>> SearchFournisseurs(string terme)
        {
            return await _context.Fournisseurs
                .Where(f => f.EstActif &&
                           (f.NomEntreprise.Contains(terme) ||
                            f.PersonneContact.Contains(terme) ||
                            f.Email.Contains(terme)))
                .ToListAsync();
        }

        [HttpPost]
        [RequireModulePermission("fournisseurs", requireWrite: true)]
        public async Task<ActionResult<Fournisseur>> PostFournisseur(Fournisseur fournisseur)
        {
            fournisseur.DateCreation = DateTime.Now;
            _context.Fournisseurs.Add(fournisseur);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetFournisseur", new { id = fournisseur.Id }, fournisseur);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("fournisseurs", requireWrite: true)]
        public async Task<IActionResult> PutFournisseur(int id, Fournisseur fournisseur)
        {
            if (id != fournisseur.Id)
            {
                return BadRequest();
            }

            _context.Entry(fournisseur).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FournisseurExists(id))
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

        [HttpPost("{id}/Desactiver")]
        [RequireModulePermission("fournisseurs", requireWrite: true)]
        public async Task<IActionResult> DesactiverFournisseur(int id)
        {
            var fournisseur = await _context.Fournisseurs.FindAsync(id);
            if (fournisseur == null)
            {
                return NotFound();
            }

            fournisseur.EstActif = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Fournisseur désactivé avec succès" });
        }

        [HttpPost("{id}/Activer")]
        [RequireModulePermission("fournisseurs", requireWrite: true)]
        public async Task<IActionResult> ActiverFournisseur(int id)
        {
            var fournisseur = await _context.Fournisseurs.FindAsync(id);
            if (fournisseur == null)
            {
                return NotFound();
            }

            fournisseur.EstActif = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Fournisseur activé avec succès" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("fournisseurs", requireWrite: true)]
        public async Task<IActionResult> DeleteFournisseur(int id)
        {
            var fournisseur = await _context.Fournisseurs.FindAsync(id);
            if (fournisseur == null)
            {
                return NotFound();
            }

            var hasAchats = await _context.Achats.AnyAsync(a => a.FournisseurId == id);
            var hasImportations = await _context.Importations.AnyAsync(i => i.FournisseurId == id);

            if (hasAchats || hasImportations)
            {
                return BadRequest("Impossible de supprimer le fournisseur car il a des achats ou importations associés. Utilisez la désactivation à la place.");
            }

            _context.Fournisseurs.Remove(fournisseur);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool FournisseurExists(int id)
        {
            return _context.Fournisseurs.Any(e => e.Id == id);
        }
    }
}
