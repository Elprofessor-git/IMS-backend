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
    public class AchatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AchatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Achat
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Achat>>> GetAchats()
        {
            return await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .ThenInclude(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .ToListAsync();
        }

        // GET: api/Achat/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Achat>> GetAchat(int id)
        {
            var achat = await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .ThenInclude(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (achat == null)
            {
                return NotFound();
            }

            return achat;
        }

        // GET: api/Achat/ByCommande/5
        [HttpGet("ByCommande/{commandeId}")]
        public async Task<ActionResult<IEnumerable<Achat>>> GetAchatsByCommande(int commandeId)
        {
            return await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .Where(a => a.CommandeClientId == commandeId)
                .ToListAsync();
        }

        // GET: api/Achat/Statut/Confirme
        [HttpGet("Statut/{statut}")]
        public async Task<ActionResult<IEnumerable<Achat>>> GetAchatsByStatut(StatutAchat statut)
        {
            return await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(a => a.Statut == statut)
                .ToListAsync();
        }

        // POST: api/Achat
        [HttpPost]
        public async Task<ActionResult<Achat>> PostAchat(Achat achat)
        {
            achat.DateCreation = DateTime.Now;
            achat.NumeroAchat = GenerateNumeroAchat();
            
            _context.Achats.Add(achat);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAchat", new { id = achat.Id }, achat);
        }

        // POST: api/Achat/5/LignesAchat
        [HttpPost("{id}/LignesAchat")]
        public async Task<ActionResult<LigneAchat>> AjouterLigneAchat(int id, LigneAchat ligneAchat)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            ligneAchat.AchatId = id;
            ligneAchat.MontantLigne = ligneAchat.Quantite * ligneAchat.PrixUnitaire;
            ligneAchat.DateCreation = DateTime.Now;

            _context.LignesAchat.Add(ligneAchat);
            
            // Mettre à jour le montant total de l'achat
            await RecalculerMontantAchat(id);
            
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAchat", new { id = achat.Id }, ligneAchat);
        }

        // POST: api/Achat/5/Soumettre
        [HttpPost("{id}/Soumettre")]
        public async Task<ActionResult> SoumettreAchat(int id)
        {
            var achat = await _context.Achats
                .Include(a => a.LignesAchat)
                .Include(a => a.CommandeClient)
                .ThenInclude(c => c.Besoins)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Brouillon)
            {
                return BadRequest("Seuls les achats en brouillon peuvent être soumis");
            }

            // Vérification des cohérences avec les besoins de la commande
            var erreurs = new List<string>();
            
            foreach (var ligne in achat.LignesAchat)
            {
                var besoin = achat.CommandeClient.Besoins
                    .FirstOrDefault(b => b.ArticleId == ligne.ArticleId);
                
                if (besoin == null)
                {
                    erreurs.Add($"L'article {ligne.Article?.Designation} n'est pas requis pour cette commande");
                }
                else if (ligne.Quantite > besoin.QuantiteTotale)
                {
                    erreurs.Add($"Quantité excessive pour {ligne.Article?.Designation}: {ligne.Quantite} > {besoin.QuantiteTotale} requis");
                }
            }

            if (erreurs.Any())
            {
                return BadRequest(new { message = "Erreurs de cohérence détectées", erreurs });
            }

            achat.Statut = StatutAchat.Soumis;
            achat.DateMiseAJour = DateTime.Now;
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Achat soumis avec succès" });
        }

        // POST: api/Achat/5/Confirmer
        [HttpPost("{id}/Confirmer")]
        public async Task<ActionResult> ConfirmerAchat(int id)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Soumis)
            {
                return BadRequest("Seuls les achats soumis peuvent être confirmés");
            }

            achat.Statut = StatutAchat.Confirme;
            achat.DateMiseAJour = DateTime.Now;

            // Créer une tâche de réception
            var tacheReception = new TacheProduction
            {
                Titre = $"Réception Achat {achat.NumeroAchat}",
                Description = $"Réception et contrôle des articles de l'achat {achat.NumeroAchat}",
                CommandeClientId = achat.CommandeClientId,
                Statut = StatutTache.NonCommence,
                Priorite = PrioriteTache.Normale,
                DateCreation = DateTime.Now,
                DateDebutPrevue = achat.DateLivraisonPrevue?.AddDays(-1) ?? DateTime.Now.AddDays(1),
                DateFinPrevue = achat.DateLivraisonPrevue ?? DateTime.Now.AddDays(7)
            };

            _context.TachesProduction.Add(tacheReception);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Achat confirmé avec succès", tacheReceptionId = tacheReception.Id });
        }

        // POST: api/Achat/5/Livrer
        [HttpPost("{id}/Livrer")]
        public async Task<ActionResult> LivrerAchat(int id)
        {
            var achat = await _context.Achats
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Confirme)
            {
                return BadRequest("Seuls les achats confirmés peuvent être livrés");
            }

            achat.Statut = StatutAchat.Livre;
            achat.DateLivraisonReelle = DateTime.Now;
            achat.DateMiseAJour = DateTime.Now;

            // Mettre à jour le stock pour chaque ligne d'achat
            foreach (var ligne in achat.LignesAchat)
            {
                var stock = new Stock
                {
                    ArticleId = ligne.ArticleId,
                    Couleur = ligne.Couleur,
                    CodeCouleur = ligne.CodeCouleur,
                    Taille = ligne.Taille,
                    Dimension = ligne.Dimension,
                    Quantite = ligne.Quantite,
                    TypeStock = TypeStock.Reserve, // Stock réservé pour la commande
                    CommandeClientId = achat.CommandeClientId,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = DateTime.Now,
                    EstValide = true,
                    ValidePar = "Système - Réception Achat"
                };

                _context.Stocks.Add(stock);

                // Créer un mouvement de stock
                var mouvement = new MouvementStock
                {
                    Stock = stock,
                    TypeMouvement = TypeMouvement.Entree,
                    OrigineMouvement = OrigineMouvement.Achat,
                    Quantite = ligne.Quantite,
                    QuantiteAvant = 0,
                    QuantiteApres = ligne.Quantite,
                    Motif = $"Réception achat {achat.NumeroAchat}",
                    DocumentReference = achat.NumeroAchat,
                    DateMouvement = DateTime.Now,
                    EffectuePar = "Système"
                };

                _context.MouvementsStock.Add(mouvement);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Achat livré et stock mis à jour avec succès" });
        }

        // PUT: api/Achat/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAchat(int id, Achat achat)
        {
            if (id != achat.Id)
            {
                return BadRequest();
            }

            achat.DateMiseAJour = DateTime.Now;
            _context.Entry(achat).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AchatExists(id))
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

        // DELETE: api/Achat/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAchat(int id)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut == StatutAchat.Confirme || achat.Statut == StatutAchat.Livre)
            {
                return BadRequest("Impossible de supprimer un achat confirmé ou livré");
            }

            _context.Achats.Remove(achat);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task RecalculerMontantAchat(int achatId)
        {
            var achat = await _context.Achats
                .Include(a => a.LignesAchat)
                .FirstOrDefaultAsync(a => a.Id == achatId);

            if (achat != null)
            {
                achat.MontantTotal = achat.LignesAchat.Sum(la => la.MontantLigne);
            }
        }

        private bool AchatExists(int id)
        {
            return _context.Achats.Any(e => e.Id == id);
        }

        private string GenerateNumeroAchat()
        {
            var today = DateTime.Now;
            var prefix = $"ACH{today:yyyyMM}";
            var count = _context.Achats.Count(a => a.NumeroAchat.StartsWith(prefix)) + 1;
            return $"{prefix}{count:D4}";
        }
    }
}

