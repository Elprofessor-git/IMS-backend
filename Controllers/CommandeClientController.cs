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
                .Include(c => c.Marque)
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
                .Include(c => c.Marque)
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

            if (commande.Statut == StatutCommande.EnProduction ||
                commande.Statut == StatutCommande.Terminee)
            {
                return BadRequest(new { message = "Impossible de supprimer une commande en production ou terminée." });
            }

            _context.CommandesClients.Remove(commande);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/CommandeClient/5/Tailles
        [HttpGet("{id}/Tailles")]
        public async Task<ActionResult<IEnumerable<ConfigTaille>>> GetTailles(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            return await _context.ConfigTailles
                .Where(ct => ct.CommandeId == id)
                .OrderBy(ct => ct.Taille)
                .ToListAsync();
        }

        // POST: api/CommandeClient/5/Tailles
        [HttpPost("{id}/Tailles")]
        public async Task<ActionResult> SetTailles(int id, [FromBody] List<ConfigTailleDto> dtos)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            var existants = _context.ConfigTailles.Where(ct => ct.CommandeId == id);
            _context.ConfigTailles.RemoveRange(existants);

            foreach (var dto in dtos)
            {
                _context.ConfigTailles.Add(new ConfigTaille
                {
                    CommandeId = id,
                    Taille = dto.Taille,
                    Quantite = dto.Quantite
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuration des tailles enregistrée", count = dtos.Count });
        }

        // GET: api/CommandeClient/5/Bom
        [HttpGet("{id}/Bom")]
        public async Task<ActionResult<IEnumerable<BomLigne>>> GetBom(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            return await _context.BomLignes
                .Include(b => b.Article)
                .Where(b => b.CommandeId == id)
                .ToListAsync();
        }

        // POST: api/CommandeClient/5/Bom
        [HttpPost("{id}/Bom")]
        public async Task<ActionResult> SetBom(int id, [FromBody] List<BomLigneDto> dtos)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            var existants = _context.BomLignes.Where(b => b.CommandeId == id);
            _context.BomLignes.RemoveRange(existants);

            foreach (var dto in dtos)
            {
                _context.BomLignes.Add(new BomLigne
                {
                    CommandeId = id,
                    ArticleId = dto.ArticleId,
                    QuantiteParPiece = dto.QuantiteParPiece,
                    Unite = dto.Unite
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "BOM enregistrée", count = dtos.Count });
        }

        // POST: api/CommandeClient/5/Calculer
        [HttpPost("{id}/Calculer")]
        public async Task<ActionResult> Calculer(int id, [FromBody] CalculerRequest request)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null)
                return NotFound();

            var totalPieces = await _context.ConfigTailles
                .Where(ct => ct.CommandeId == id)
                .SumAsync(ct => (decimal)ct.Quantite);

            if (totalPieces <= 0)
                return BadRequest("Aucune configuration de tailles définie pour cette commande.");

            var bomLignes = await _context.BomLignes
                .Where(b => b.CommandeId == id)
                .ToListAsync();

            if (!bomLignes.Any())
                return BadRequest("Aucune ligne BOM définie pour cette commande.");

            var existants = _context.ResultatsCalcul.Where(r => r.CommandeId == id);
            _context.ResultatsCalcul.RemoveRange(existants);

            var resultats = new List<ResultatCalcul>();
            decimal marge = request.MargeAppliquee;

            foreach (var ligne in bomLignes)
            {
                var besoinBrut = ligne.QuantiteParPiece * totalPieces;
                var besoinFinal = besoinBrut * (1 + marge / 100);

                var qteStockReserve = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId && s.CommandeClientId == id)
                    .SumAsync(s => s.Quantite);

                var qteAchat = await _context.LignesAchat
                    .Include(la => la.Achat)
                    .Where(la => la.ArticleId == ligne.ArticleId
                              && la.Achat.CommandeClientId == id
                              && la.Achat.Statut == StatutAchat.Confirme)
                    .SumAsync(la => la.Quantite);

                var qteImport = await _context.LignesImportation
                    .Where(li => li.ArticleId == ligne.ArticleId && li.CommandeClientId == id)
                    .SumAsync(li => li.Quantite);

                var qteDisponible = qteStockReserve + qteAchat + qteImport;
                var manque = Math.Max(0, besoinFinal - qteDisponible);

                resultats.Add(new ResultatCalcul
                {
                    CommandeId = id,
                    ArticleId = ligne.ArticleId,
                    BesoinBrut = besoinBrut,
                    MargeAppliquee = marge,
                    BesoinFinal = besoinFinal,
                    QteAchat = qteAchat,
                    QteImport = qteImport,
                    QteStockReserve = qteStockReserve,
                    QteDisponible = qteDisponible,
                    Manque = manque,
                    EstSuffisant = qteDisponible >= besoinFinal,
                    DateCalcul = DateTime.UtcNow
                });
            }

            _context.ResultatsCalcul.AddRange(resultats);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Calcul BOM terminé",
                totalPieces,
                lignesCalculees = resultats.Count,
                toutSuffisant = resultats.All(r => r.EstSuffisant)
            });
        }

        // GET: api/CommandeClient/5/ResultatCalcul
        [HttpGet("{id}/ResultatCalcul")]
        public async Task<ActionResult<IEnumerable<ResultatCalcul>>> GetResultatCalcul(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            return await _context.ResultatsCalcul
                .Include(r => r.Article)
                .Where(r => r.CommandeId == id)
                .OrderBy(r => r.Article.Designation)
                .ToListAsync();
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

    public record ConfigTailleDto(string Taille, int Quantite);
    public record BomLigneDto(int ArticleId, decimal QuantiteParPiece, string? Unite);
    public record CalculerRequest(decimal MargeAppliquee);
}

