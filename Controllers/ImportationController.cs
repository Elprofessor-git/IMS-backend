using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImportationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ImportationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Importation
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Importation>>> GetImportations()
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .ToListAsync();
        }

        // GET: api/Importation/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Importation>> GetImportation(int id)
        {
            var importation = await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.CommandeClient)
                .ThenInclude(c => c.Client)
                .ThenInclude(cl => cl.Plateforme)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (importation == null)
            {
                return NotFound();
            }

            return importation;
        }

        // GET: api/Importation/Statut/Validee
        [HttpGet("Statut/{statut}")]
        public async Task<ActionResult<IEnumerable<Importation>>> GetImportationsByStatut(StatutImportation statut)
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Where(i => i.Statut == statut)
                .ToListAsync();
        }

        // GET: api/Importation/Filtrer
        [HttpGet("Filtrer")]
        public async Task<ActionResult<IEnumerable<Importation>>> FiltrerImportations(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? fournisseurId,
            [FromQuery] StatutImportation? statut)
        {
            var query = _context.Importations
                .Include(i => i.Fournisseur)
                .AsQueryable();

            if (dateDebut.HasValue)
                query = query.Where(i => i.DateImportation >= dateDebut.Value);

            if (dateFin.HasValue)
                query = query.Where(i => i.DateImportation <= dateFin.Value);

            if (fournisseurId.HasValue)
                query = query.Where(i => i.FournisseurId == fournisseurId.Value);

            if (statut.HasValue)
                query = query.Where(i => i.Statut == statut.Value);

            return await query.OrderByDescending(i => i.DateImportation).ToListAsync();
        }

        // POST: api/Importation
        [HttpPost]
        public async Task<ActionResult<Importation>> PostImportation(Importation importation)
        {
            importation.DateCreation = DateTime.Now;
            importation.ReferenceImportation = GenerateReferenceImportation();
            
            _context.Importations.Add(importation);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetImportation", new { id = importation.Id }, importation);
        }

        // POST: api/Importation/5/LignesImportation
        [HttpPost("{id}/LignesImportation")]
        public async Task<ActionResult<LigneImportation>> AjouterLigneImportation(int id, LigneImportation ligneImportation)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            ligneImportation.ImportationId = id;
            ligneImportation.MontantLigne = ligneImportation.Quantite * ligneImportation.PrixUnitaire;
            ligneImportation.DateCreation = DateTime.Now;

            _context.LignesImportation.Add(ligneImportation);
            
            // Mettre à jour le montant total de l'importation
            await RecalculerMontantImportation(id);
            
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetImportation", new { id = importation.Id }, ligneImportation);
        }

        // POST: api/Importation/5/Soumettre
        [HttpPost("{id}/Soumettre")]
        public async Task<ActionResult> SoumettreImportation(int id)
        {
            var importation = await _context.Importations
                .Include(i => i.LignesImportation)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Brouillon)
            {
                return BadRequest("Seules les importations en brouillon peuvent être soumises");
            }

            if (!importation.LignesImportation.Any())
            {
                return BadRequest("L'importation doit contenir au moins une ligne de produit");
            }

            importation.Statut = StatutImportation.Soumise;
            importation.DateMiseAJour = DateTime.Now;
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Importation soumise avec succès" });
        }

        // POST: api/Importation/5/Valider
        [HttpPost("{id}/Valider")]
        public async Task<ActionResult> ValiderImportation(int id, [FromBody] string validePar)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Soumise)
            {
                return BadRequest("Seules les importations soumises peuvent être validées");
            }

            importation.Statut = StatutImportation.Validee;
            importation.DateMiseAJour = DateTime.Now;
            importation.ModifiePar = validePar;
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Importation validée avec succès" });
        }

        // POST: api/Importation/5/Recevoir
        [HttpPost("{id}/Recevoir")]
        public async Task<ActionResult> RecevoirImportation(int id)
        {
            var importation = await _context.Importations
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Validee)
            {
                return BadRequest("Seules les importations validées peuvent être reçues");
            }

            importation.Statut = StatutImportation.Recue;
            importation.DateReceptionReelle = DateTime.Now;
            importation.DateMiseAJour = DateTime.Now;

            // Mettre à jour le stock pour chaque ligne d'importation
            foreach (var ligne in importation.LignesImportation)
            {
                var stock = new Stock
                {
                    ArticleId = ligne.ArticleId,
                    Couleur = ligne.Couleur,
                    CodeCouleur = ligne.CodeCouleur,
                    Dimension = ligne.Dimension,
                    Quantite = ligne.Quantite,
                    TypeStock = TypeStock.Importe, // Stock importé
                    CommandeClientId = ligne.CommandeClientId,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = DateTime.Now,
                    EstValide = true,
                    ValidePar = "Système - Réception Importation"
                };

                _context.Stocks.Add(stock);

                // Créer un mouvement de stock
                var mouvement = new MouvementStock
                {
                    Stock = stock,
                    TypeMouvement = TypeMouvement.Entree,
                    OrigineMouvement = OrigineMouvement.Importation,
                    Quantite = ligne.Quantite,
                    QuantiteAvant = 0,
                    QuantiteApres = ligne.Quantite,
                    Motif = $"Réception importation {importation.ReferenceImportation}",
                    DocumentReference = importation.ReferenceImportation,
                    DateMouvement = DateTime.Now,
                    EffectuePar = "Système"
                };

                _context.MouvementsStock.Add(mouvement);

                // Marquer la ligne comme affectée au stock
                ligne.EstAffecteStock = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Importation reçue et stock mis à jour avec succès" });
        }

        // POST: api/Importation/5/AffecterCommandes
        [HttpPost("{id}/AffecterCommandes")]
        public async Task<ActionResult> AffecterAuxCommandes(int id)
        {
            var importation = await _context.Importations
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.CommandeClient)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Recue)
            {
                return BadRequest("Seules les importations reçues peuvent être affectées aux commandes");
            }

            var affectations = new List<object>();

            foreach (var ligne in importation.LignesImportation.Where(li => li.CommandeClientId.HasValue))
            {
                // Trouver le stock correspondant
                var stocks = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId && 
                               s.TypeStock == TypeStock.Importe &&
                               s.Quantite > 0)
                    .ToListAsync();

                var quantiteAAffecter = ligne.Quantite;

                foreach (var stock in stocks)
                {
                    if (quantiteAAffecter <= 0) break;

                    var quantiteDisponible = stock.Quantite - stock.QuantiteReservee;
                    var quantiteAReserver = Math.Min(quantiteAAffecter, quantiteDisponible);

                    if (quantiteAReserver > 0)
                    {
                        stock.QuantiteReservee += quantiteAReserver;
                        stock.CommandeClientId = ligne.CommandeClientId; // Lier le stock à la commande
                        quantiteAAffecter -= quantiteAReserver;

                        affectations.Add(new
                        {
                            CommandeId = ligne.CommandeClientId,
                            ArticleId = ligne.ArticleId,
                            QuantiteAffectee = quantiteAReserver
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Affectation aux commandes terminée", 
                affectations 
            });
        }

        // PUT: api/Importation/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutImportation(int id, Importation importation)
        {
            if (id != importation.Id)
            {
                return BadRequest();
            }

            importation.DateMiseAJour = DateTime.Now;
            _context.Entry(importation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ImportationExists(id))
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

        // DELETE: api/Importation/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImportation(int id)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut == StatutImportation.Validee || importation.Statut == StatutImportation.Recue)
            {
                return BadRequest("Impossible de supprimer une importation validée ou reçue");
            }

            _context.Importations.Remove(importation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Importation/{id}/Documents
        [HttpPost("{id}/Documents")]
        public async Task<ActionResult<DocumentImportation>> UploadDocument(int id, IFormFile file)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null) return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Aucun fichier fourni." });

            var dossier = Path.Combine("wwwroot", "uploads", "importations", id.ToString());
            Directory.CreateDirectory(dossier);

            var nomFichier = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var chemin = Path.Combine(dossier, nomFichier);

            using (var stream = new FileStream(chemin, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new DocumentImportation
            {
                ImportationId = id,
                NomFichier = file.FileName,
                CheminFichier = $"/uploads/importations/{id}/{nomFichier}",
                TypeFichier = file.ContentType,
                TailleOctets = file.Length,
                DateAjout = DateTime.UtcNow,
                AjoutePar = User.Identity?.Name
            };

            _context.DocumentsImportation.Add(doc);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetImportation), new { id }, doc);
        }

        // GET: api/Importation/{id}/Documents
        [HttpGet("{id}/Documents")]
        public async Task<ActionResult<IEnumerable<DocumentImportation>>> GetDocuments(int id)
        {
            if (!await _context.Importations.AnyAsync(i => i.Id == id))
                return NotFound();

            var docs = await _context.DocumentsImportation
                .Where(d => d.ImportationId == id)
                .OrderByDescending(d => d.DateAjout)
                .ToListAsync();

            return Ok(docs);
        }

        // GET: api/Importation/Documents/{docId}/Telecharger
        [HttpGet("Documents/{docId}/Telecharger")]
        public async Task<IActionResult> TelechargerDocument(int docId)
        {
            var doc = await _context.DocumentsImportation.FindAsync(docId);
            if (doc == null) return NotFound();

            var chemin = Path.Combine("wwwroot", doc.CheminFichier.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(chemin))
                return NotFound(new { message = "Fichier introuvable sur le serveur." });

            var bytes = await System.IO.File.ReadAllBytesAsync(chemin);
            var contentType = doc.TypeFichier ?? "application/octet-stream";
            return File(bytes, contentType, doc.NomFichier);
        }

        // DELETE: api/Importation/Documents/{docId}
        [HttpDelete("Documents/{docId}")]
        public async Task<IActionResult> SupprimerDocument(int docId)
        {
            var doc = await _context.DocumentsImportation.FindAsync(docId);
            if (doc == null) return NotFound();

            var chemin = Path.Combine("wwwroot", doc.CheminFichier.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(chemin))
                System.IO.File.Delete(chemin);

            _context.DocumentsImportation.Remove(doc);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task RecalculerMontantImportation(int importationId)
        {
            var importation = await _context.Importations
                .Include(i => i.LignesImportation)
                .FirstOrDefaultAsync(i => i.Id == importationId);

            if (importation != null)
            {
                importation.MontantTotal = importation.LignesImportation.Sum(li => li.MontantLigne);
            }
        }

        private bool ImportationExists(int id)
        {
            return _context.Importations.Any(e => e.Id == id);
        }

        private string GenerateReferenceImportation()
        {
            var today = DateTime.Now;
            var prefix = $"IMP{today:yyyyMM}";
            var count = _context.Importations.Count(i => i.ReferenceImportation.StartsWith(prefix)) + 1;
            return $"{prefix}{count:D4}";
        }
    }
}

