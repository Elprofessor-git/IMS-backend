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
    public class CommandeClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CommandeClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CommandeClient
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommandeClient>>> GetCommandes()
        {
            return await _context.CommandesClients
                .Include(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
                .ToListAsync();
        }

        // GET: api/CommandeClient/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CommandeClient>> GetCommandeClient(int id)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
                .Include(c => c.Taches)
                .Include(c => c.Achats)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (commande == null)
            {
                return NotFound();
            }

            return commande;
        }

        // GET: api/CommandeClient/Statut/Prete
        [HttpGet("Statut/{statut}")]
        public async Task<ActionResult<IEnumerable<CommandeClient>>> GetCommandesByStatut(StatutCommande statut)
        {
            return await _context.CommandesClients
                .Include(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Where(c => c.Statut == statut)
                .ToListAsync();
        }

        // POST: api/CommandeClient
        [HttpPost]
        public async Task<ActionResult<CommandeClient>> PostCommandeClient(CommandeClient commande)
        {
            commande.DateCreation = DateTime.Now;
            commande.NumeroCommande = GenerateNumeroCommande();
            
            _context.CommandesClients.Add(commande);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCommandeClient", new { id = commande.Id }, commande);
        }

        // POST: api/CommandeClient/5/Besoins
        [HttpPost("{id}/Besoins")]
        public async Task<ActionResult<BesoinCommande>> AjouterBesoin(int id, BesoinCommande besoin)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null)
            {
                return NotFound();
            }

            besoin.CommandeClientId = id;
            besoin.QuantiteTotale = besoin.QuantiteUnitaire * besoin.NombrePieces;
            besoin.DateCreation = DateTime.Now;

            _context.BesoinsCommandes.Add(besoin);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCommandeClient", new { id = commande.Id }, besoin);
        }

        // POST: api/CommandeClient/5/ValiderRessources
        [HttpPost("{id}/ValiderRessources")]
        public async Task<ActionResult> ValiderRessources(int id)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (commande == null)
            {
                return NotFound();
            }

            decimal totalCouverture = 0;
            int besoinsTraites = 0;

            foreach (var besoin in commande.Besoins)
            {
                // 1. Vérifier stock importé
                var stockImporte = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId && 
                               s.TypeStock == TypeStock.Importe &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                besoin.QuantiteStockImporte = Math.Min(stockImporte, besoin.QuantiteTotale);

                // 2. Vérifier les ressources réservées (Achats locaux ET Stock déjà réservé pour cette commande)
                var achatsEnCours = await _context.LignesAchat
                    .Include(la => la.Achat)
                    .Where(la => la.ArticleId == besoin.ArticleId &&
                                la.Achat.CommandeClientId == commande.Id &&
                                la.Achat.Statut == StatutAchat.Confirme)
                    .SumAsync(la => la.Quantite);

                var stockDejaReserve = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId &&
                               s.CommandeClientId == commande.Id)
                    .SumAsync(s => s.Quantite);

                besoin.QuantiteAchatsLocaux = achatsEnCours + stockDejaReserve;

                // 3. Calculer le reste nécessaire depuis stock libre
                var quantiteRestante = besoin.QuantiteTotale - besoin.QuantiteStockImporte - besoin.QuantiteAchatsLocaux;
                
                if (quantiteRestante > 0)
                {
                    var stockLibre = await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId && 
                                   s.TypeStock == TypeStock.Libre &&
                                   s.Quantite > s.QuantiteReservee)
                        .SumAsync(s => s.Quantite - s.QuantiteReservee);

                    besoin.QuantiteStockLibre = Math.Min(stockLibre, quantiteRestante);
                }

                besoin.QuantiteCouverte = besoin.QuantiteStockImporte + besoin.QuantiteAchatsLocaux + besoin.QuantiteStockLibre;
                besoin.EstCompletementCouvert = besoin.QuantiteCouverte >= besoin.QuantiteTotale;

                totalCouverture += (besoin.QuantiteCouverte / besoin.QuantiteTotale) * 100;
                besoinsTraites++;
            }

            commande.PourcentageRessourcesCouvertes = besoinsTraites > 0 ? totalCouverture / besoinsTraites : 0;
            
            if (commande.PourcentageRessourcesCouvertes >= 100)
            {
                commande.Statut = StatutCommande.Prete;
            }
            else
            {
                commande.Statut = StatutCommande.EnAttente;
            }

            commande.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Validation des ressources terminée",
                pourcentageCouverture = commande.PourcentageRessourcesCouvertes,
                statut = commande.Statut.ToString()
            });
        }

        // POST: api/CommandeClient/5/GenererTaches
        [HttpPost("{id}/GenererTaches")]
        public async Task<ActionResult> GenererTaches(int id)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (commande == null)
            {
                return NotFound();
            }

            if (commande.Statut != StatutCommande.Prete)
            {
                return BadRequest("La commande doit être au statut 'Prête' pour générer les tâches");
            }

            var tache = new TacheProduction
            {
                Titre = $"Production Commande {commande.NumeroCommande}",
                Description = $"Production pour {commande.Client.Nom} - {commande.TitreCommande}",
                CommandeClientId = commande.Id,
                Statut = StatutTache.NonCommence,
                Priorite = PrioriteTache.Normale,
                DateCreation = DateTime.Now,
                DateDebutPrevue = DateTime.Now.AddDays(1),
                DateFinPrevue = commande.DateLivraisonSouhaitee?.AddDays(-2) ?? DateTime.Now.AddDays(7)
            };

            _context.TachesProduction.Add(tache);
            commande.Statut = StatutCommande.EnProduction;
            commande.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâches de production générées avec succès", tacheId = tache.Id });
        }

        // PUT: api/CommandeClient/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCommandeClient(int id, CommandeClient commande)
        {
            if (id != commande.Id)
            {
                return BadRequest();
            }

            commande.DateMiseAJour = DateTime.Now;
            _context.Entry(commande).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CommandeClientExists(id))
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

        // DELETE: api/CommandeClient/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommandeClient(int id)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null)
            {
                return NotFound();
            }

            _context.CommandesClients.Remove(commande);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CommandeClientExists(int id)
        {
            return _context.CommandesClients.Any(e => e.Id == id);
        }

        private string GenerateNumeroCommande()
        {
            var today = DateTime.Now;
            var prefix = $"CMD{today:yyyyMM}";
            var count = _context.CommandesClients.Count(c => c.NumeroCommande.StartsWith(prefix)) + 1;
            return $"{prefix}{count:D4}";
        }
    }
}

