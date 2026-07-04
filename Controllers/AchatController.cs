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
    public class AchatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AchatController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("achats", requireWrite: false)]
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

        [HttpGet("{id}")]
        [RequireModulePermission("achats", requireWrite: false)]
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

        [HttpGet("ByCommande/{commandeId}")]
        [RequireModulePermission("achats", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Achat>>> GetAchatsByCommande(int commandeId)
        {
            return await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .Where(a => a.CommandeClientId == commandeId)
                .ToListAsync();
        }

        [HttpGet("Statut/{statut}")]
        [RequireModulePermission("achats", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Achat>>> GetAchatsByStatut(StatutAchat statut)
        {
            return await _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(a => a.Statut == statut)
                .ToListAsync();
        }

        [HttpPost]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<ActionResult<Achat>> PostAchat(Achat achat)
        {
            achat.DateCreation = DateTime.Now;
            achat.NumeroAchat = GenerateNumeroAchat();

            _context.Achats.Add(achat);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAchat", new { id = achat.Id }, achat);
        }

        [HttpPost("{id}/LignesAchat")]
        [RequireModulePermission("achats", requireWrite: true)]
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

            switch (ligneAchat.TypeDestination)
            {
                case TypeDestinationAchat.Commande when !ligneAchat.CommandeClientId.HasValue:
                    return BadRequest("TypeDestination=Commande requiert un CommandeClientId.");
                case TypeDestinationAchat.Marque when !ligneAchat.ClientId.HasValue:
                    return BadRequest("TypeDestination=Marque requiert un ClientId.");
                case TypeDestinationAchat.Plateforme when !ligneAchat.PlateformeId.HasValue:
                    return BadRequest("TypeDestination=Plateforme requiert un PlateformeId.");
                case TypeDestinationAchat.StockLibre:
                    ligneAchat.CommandeClientId = null;
                    ligneAchat.ClientId = null;
                    ligneAchat.PlateformeId = null;
                    break;
            }

            _context.LignesAchat.Add(ligneAchat);

            await RecalculerMontantAchat(id);

            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAchat", new { id = achat.Id }, ligneAchat);
        }

        [HttpPost("{id}/Soumettre")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<ActionResult> SoumettreAchat(int id)
        {
            var achat = await _context.Achats
                .Include(a => a.LignesAchat)
                .ThenInclude(la => la.Article)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Brouillon)
            {
                return BadRequest("Seuls les achats en brouillon peuvent être soumis");
            }

            var erreurs = new List<string>();

            var lignesParCommande = achat.LignesAchat
                .Where(l => l.TypeDestination == TypeDestinationAchat.Commande && l.CommandeClientId.HasValue)
                .GroupBy(l => l.CommandeClientId!.Value);

            foreach (var groupe in lignesParCommande)
            {
                var commande = await _context.CommandesClients
                    .Include(c => c.Besoins)
                    .FirstOrDefaultAsync(c => c.Id == groupe.Key);

                if (commande == null) continue;

                foreach (var ligne in groupe)
                {
                    var besoin = commande.Besoins.FirstOrDefault(b => b.ArticleId == ligne.ArticleId);
                    if (besoin == null)
                    {
                        erreurs.Add($"L'article {ligne.Article?.Designation} n'est pas requis pour la commande #{groupe.Key}");
                    }
                    else if (ligne.Quantite > besoin.QuantiteTotale)
                    {
                        erreurs.Add($"Quantité excessive pour {ligne.Article?.Designation}: {ligne.Quantite} > {besoin.QuantiteTotale} requis (commande #{groupe.Key})");
                    }
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

        [HttpPost("{id}/Confirmer")]
        [RequireModulePermission("achats", requireWrite: true)]
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

        [HttpPost("{id}/Livrer")]
        [RequireModulePermission("achats", requireWrite: true)]
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
                    TypeStock = TypeStock.Reserve,
                    CommandeClientId = ligne.TypeDestination == TypeDestinationAchat.Commande ? ligne.CommandeClientId : null,
                    ClientId = ligne.TypeDestination == TypeDestinationAchat.Marque ? ligne.ClientId : null,
                    PlateformeId = ligne.TypeDestination == TypeDestinationAchat.Plateforme ? ligne.PlateformeId : null,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = DateTime.Now,
                    EstValide = true,
                    ValidePar = "Système - Réception Achat"
                };

                _context.Stocks.Add(stock);

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

        [HttpPut("{id}")]
        [RequireModulePermission("achats", requireWrite: true)]
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

        [HttpDelete("{id}")]
        [RequireModulePermission("achats", requireWrite: true)]
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
