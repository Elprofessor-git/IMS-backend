using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Importation;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImportationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImportationController> _logger;

        public ImportationController(ApplicationDbContext context, ILogger<ImportationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [RequireModulePermission("importations", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Importation>>> GetImportations()
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.Plateforme)
                .Include(i => i.LignesImportation)
                .ThenInclude(li => li.Article)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("importations", requireWrite: false)]
        public async Task<ActionResult<Importation>> GetImportation(int id)
        {
            var importation = await _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.Plateforme)
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

        [HttpGet("Statut/{statut}")]
        [RequireModulePermission("importations", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Importation>>> GetImportationsByStatut(StatutImportation statut)
        {
            return await _context.Importations
                .Include(i => i.Fournisseur)
                .Where(i => i.Statut == statut)
                .ToListAsync();
        }

        [HttpGet("Filtrer")]
        [RequireModulePermission("importations", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Importation>>> FiltrerImportations(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? fournisseurId,
            [FromQuery] int? plateformeId,
            [FromQuery] int? commandeClientId,
            [FromQuery] StatutImportation? statut)
        {
            var query = _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.Plateforme)
                .AsQueryable();

            if (dateDebut.HasValue)
                query = query.Where(i => i.DateImportation >= dateDebut.Value);

            if (dateFin.HasValue)
                query = query.Where(i => i.DateImportation <= dateFin.Value);

            if (fournisseurId.HasValue)
                query = query.Where(i => i.FournisseurId == fournisseurId.Value);

            if (plateformeId.HasValue)
                query = query.Where(i => i.PlateformeId == plateformeId.Value);

            if (commandeClientId.HasValue)
                query = query.Where(i => i.LignesImportation.Any(l => l.CommandeClientId == commandeClientId.Value));

            if (statut.HasValue)
                query = query.Where(i => i.Statut == statut.Value);

            return await query.OrderByDescending(i => i.DateImportation).ToListAsync();
        }

        [HttpPost]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<ActionResult<Importation>> PostImportation(CreateImportationDto dto)
        {
            if (dto.FournisseurId.HasValue && dto.PlateformeId.HasValue)
            {
                return BadRequest("La source de l'importation doit être soit un fournisseur, soit une plateforme, pas les deux.");
            }

            var importation = new Importation
            {
                ReferenceImportation = GenerateReferenceImportation(),
                FournisseurId = dto.FournisseurId,
                PlateformeId = dto.PlateformeId,
                DateReceptionPrevue = dto.DateReceptionPrevue,
                ModeExpedition = dto.ModeExpedition,
                Devise = dto.Devise,
                NotesImportation = dto.NotesImportation,
                CreePar = dto.CreePar,
                DateCreation = DateTime.Now
            };

            _context.Importations.Add(importation);

            const int maxTentatives = 5;
            for (var tentative = 1; ; tentative++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (EstConflitReferenceImportation(ex) && tentative < maxTentatives)
                {
                    importation.ReferenceImportation = GenerateReferenceImportation();
                }
            }

            return CreatedAtAction("GetImportation", new { id = importation.Id }, importation);
        }

        [HttpPost("{id}/LignesImportation")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<ActionResult<LigneImportation>> AjouterLigneImportation(int id, CreateLigneImportationDto dto)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            var ligneImportation = new LigneImportation
            {
                ImportationId = id,
                ArticleId = dto.ArticleId,
                TypeDestination = dto.TypeDestination,
                CommandeClientId = dto.CommandeClientId,
                ClientId = dto.ClientId,
                PlateformeId = dto.PlateformeId,
                Designation = dto.Designation,
                Couleur = dto.Couleur,
                CodeCouleur = dto.CodeCouleur,
                Dimension = dto.Dimension,
                Nature = dto.Nature,
                Quantite = dto.Quantite,
                PrixUnitaire = dto.PrixUnitaire,
                MontantLigne = dto.Quantite * dto.PrixUnitaire,
                Devise = dto.Devise,
                Notes = dto.Notes,
                DateCreation = DateTime.Now
            };

            switch (ligneImportation.TypeDestination)
            {
                case TypeDestinationImportation.Commande when !ligneImportation.CommandeClientId.HasValue:
                    return BadRequest("TypeDestination=Commande requiert un CommandeClientId.");
                case TypeDestinationImportation.Marque when !ligneImportation.ClientId.HasValue:
                    return BadRequest("TypeDestination=Marque requiert un ClientId.");
                case TypeDestinationImportation.Plateforme when !ligneImportation.PlateformeId.HasValue:
                    return BadRequest("TypeDestination=Plateforme requiert un PlateformeId.");
                case TypeDestinationImportation.StockLibre:
                    ligneImportation.CommandeClientId = null;
                    ligneImportation.ClientId = null;
                    ligneImportation.PlateformeId = null;
                    break;
            }

            _context.LignesImportation.Add(ligneImportation);

            await RecalculerMontantImportation(id);

            await _context.SaveChangesAsync();

            // Prix de référence + historique (Fonctionnalité 12) : actif aussi pour les
            // importations (le prix d'import devient le dernier prix connu). Best effort,
            // ne doit JAMAIS faire échouer la création de la ligne — erreurs loguées et avalées.
            await PrixHistoriqueService.EnregistrerPrixAsync(
                _context,
                ligneImportation.ArticleId,
                ligneImportation.PrixUnitaire,
                ligneImportation.Devise,
                SourcePrix.LigneImportation,
                ligneImportationId: ligneImportation.Id,
                logger: _logger);

            return CreatedAtAction("GetImportation", new { id = importation.Id }, ligneImportation);
        }

        [HttpPut("{id}/LignesImportation/{ligneId}")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<IActionResult> ModifierLigneImportation(int id, int ligneId, CreateLigneImportationDto dto)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Brouillon)
            {
                return BadRequest("Seules les importations en Brouillon peuvent être modifiées");
            }

            var ligneImportation = await _context.LignesImportation
                .FirstOrDefaultAsync(l => l.Id == ligneId && l.ImportationId == id);
            if (ligneImportation == null)
            {
                return NotFound();
            }

            var commandeClientId = dto.CommandeClientId;
            var clientId = dto.ClientId;
            var plateformeId = dto.PlateformeId;

            switch (dto.TypeDestination)
            {
                case TypeDestinationImportation.Commande when !commandeClientId.HasValue:
                    return BadRequest("TypeDestination=Commande requiert un CommandeClientId.");
                case TypeDestinationImportation.Marque when !clientId.HasValue:
                    return BadRequest("TypeDestination=Marque requiert un ClientId.");
                case TypeDestinationImportation.Plateforme when !plateformeId.HasValue:
                    return BadRequest("TypeDestination=Plateforme requiert un PlateformeId.");
                case TypeDestinationImportation.StockLibre:
                    commandeClientId = null;
                    clientId = null;
                    plateformeId = null;
                    break;
            }

            ligneImportation.ArticleId = dto.ArticleId;
            ligneImportation.TypeDestination = dto.TypeDestination;
            ligneImportation.CommandeClientId = commandeClientId;
            ligneImportation.ClientId = clientId;
            ligneImportation.PlateformeId = plateformeId;
            ligneImportation.Designation = dto.Designation;
            ligneImportation.Couleur = dto.Couleur;
            ligneImportation.CodeCouleur = dto.CodeCouleur;
            ligneImportation.Dimension = dto.Dimension;
            ligneImportation.Nature = dto.Nature;
            ligneImportation.Quantite = dto.Quantite;
            ligneImportation.PrixUnitaire = dto.PrixUnitaire;
            ligneImportation.MontantLigne = dto.Quantite * dto.PrixUnitaire;
            ligneImportation.Devise = dto.Devise;
            ligneImportation.Notes = dto.Notes;

            await RecalculerMontantImportation(id);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}/LignesImportation/{ligneId}")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<IActionResult> SupprimerLigneImportation(int id, int ligneId)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Brouillon)
            {
                return BadRequest("Seules les lignes d'importations en Brouillon peuvent être supprimées");
            }

            var ligneImportation = await _context.LignesImportation
                .FirstOrDefaultAsync(l => l.Id == ligneId && l.ImportationId == id);
            if (ligneImportation == null)
            {
                return NotFound();
            }

            if (ligneImportation.EstAffecteStock)
            {
                return BadRequest("Impossible de supprimer une ligne déjà affectée au stock");
            }

            _context.LignesImportation.Remove(ligneImportation);

            await RecalculerMontantImportation(id);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/Soumettre")]
        [RequireModulePermission("importations", requireWrite: true)]
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

        [HttpPost("{id}/Valider")]
        [RequireModulePermission("importations", requireWrite: true)]
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

        [HttpPost("{id}/Recevoir")]
        [RequireModulePermission("importations", requireWrite: true)]
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

            foreach (var ligne in importation.LignesImportation)
            {
                var quantiteRestante = ligne.Quantite - ligne.QuantiteRecue;
                if (quantiteRestante <= 0)
                {
                    continue; // Déjà entièrement reçue via réceptions partielles
                }

                var stock = new Stock
                {
                    ArticleId = ligne.ArticleId,
                    Couleur = ligne.Couleur,
                    CodeCouleur = ligne.CodeCouleur,
                    Dimension = ligne.Dimension,
                    Notes = ligne.Designation ?? ligne.Nature ?? ligne.Notes,
                    Quantite = quantiteRestante,
                    TypeStock = TypeStock.Importe,
                    CommandeClientId = ligne.TypeDestination == TypeDestinationImportation.Commande ? ligne.CommandeClientId : null,
                    ClientId = ligne.TypeDestination == TypeDestinationImportation.Marque ? ligne.ClientId : null,
                    PlateformeId = ligne.TypeDestination == TypeDestinationImportation.Plateforme ? ligne.PlateformeId : null,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = DateTime.Now,
                    EstValide = true,
                    ValidePar = "Système - Réception Importation"
                };

                _context.Stocks.Add(stock);

                var mouvement = new MouvementStock
                {
                    Stock = stock,
                    TypeMouvement = TypeMouvement.Entree,
                    OrigineMouvement = OrigineMouvement.Importation,
                    Quantite = quantiteRestante,
                    QuantiteAvant = 0,
                    QuantiteApres = quantiteRestante,
                    Motif = $"Réception importation {importation.ReferenceImportation}",
                    DocumentReference = importation.ReferenceImportation,
                    DateMouvement = DateTime.Now,
                    EffectuePar = "Système"
                };

                _context.MouvementsStock.Add(mouvement);

                ligne.QuantiteRecue = ligne.Quantite;
                ligne.StatutLigne = StatutLigneImportation.Complete;
                ligne.EstAffecteStock = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Importation reçue et stock mis à jour avec succès" });
        }

        [HttpPost("{id}/LignesImportation/{ligneId}/RecevoirPartiel")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<IActionResult> RecevoirPartiel(int id, int ligneId, RecevoirPartielDto dto)
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
                return BadRequest("Seules les importations validées peuvent enregistrer une réception partielle");
            }

            var ligne = importation.LignesImportation.FirstOrDefault(l => l.Id == ligneId);
            if (ligne == null)
            {
                return NotFound();
            }

            if (ligne.StatutLigne != StatutLigneImportation.EnAttente && ligne.StatutLigne != StatutLigneImportation.PartielleEnCours)
            {
                return BadRequest("Seules les lignes en attente ou partiellement reçues peuvent être reçues partiellement");
            }

            var quantiteRestante = ligne.Quantite - ligne.QuantiteRecue;
            if (dto.Quantite <= 0 || dto.Quantite > quantiteRestante)
            {
                return BadRequest($"La quantité doit être comprise entre 0.01 et le reliquat restant ({quantiteRestante})");
            }

            var stock = new Stock
            {
                ArticleId = ligne.ArticleId,
                Couleur = ligne.Couleur,
                CodeCouleur = ligne.CodeCouleur,
                Dimension = ligne.Dimension,
                Notes = ligne.Designation ?? ligne.Nature ?? ligne.Notes,
                Quantite = dto.Quantite,
                TypeStock = TypeStock.Importe,
                CommandeClientId = ligne.TypeDestination == TypeDestinationImportation.Commande ? ligne.CommandeClientId : null,
                ClientId = ligne.TypeDestination == TypeDestinationImportation.Marque ? ligne.ClientId : null,
                PlateformeId = ligne.TypeDestination == TypeDestinationImportation.Plateforme ? ligne.PlateformeId : null,
                PrixUnitaire = ligne.PrixUnitaire,
                Devise = ligne.Devise,
                DateEntree = DateTime.Now,
                EstValide = true,
                ValidePar = "Système - Réception Partielle Importation"
            };

            _context.Stocks.Add(stock);

            var mouvement = new MouvementStock
            {
                Stock = stock,
                TypeMouvement = TypeMouvement.Entree,
                OrigineMouvement = OrigineMouvement.Importation,
                Quantite = dto.Quantite,
                QuantiteAvant = 0,
                QuantiteApres = dto.Quantite,
                Motif = $"Réception partielle importation {importation.ReferenceImportation} - ligne {ligne.Id}",
                DocumentReference = importation.ReferenceImportation,
                DateMouvement = DateTime.Now,
                EffectuePar = "Système"
            };

            _context.MouvementsStock.Add(mouvement);

            ligne.QuantiteRecue += dto.Quantite;
            if (ligne.QuantiteRecue >= ligne.Quantite)
            {
                ligne.StatutLigne = StatutLigneImportation.Complete;
                ligne.EstAffecteStock = true;
            }
            else
            {
                ligne.StatutLigne = StatutLigneImportation.PartielleEnCours;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Réception partielle enregistrée avec succès", ligneId = ligne.Id });
        }

        [HttpPost("{id}/ClotureForcee")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<IActionResult> ClotureForcee(int id, ClotureForceeDto dto)
        {
            var importation = await _context.Importations
                .Include(i => i.LignesImportation)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (importation == null)
            {
                return NotFound();
            }

            if (importation.Statut != StatutImportation.Validee)
            {
                return BadRequest("Seules les importations validées peuvent être clôturées");
            }

            foreach (var ligne in importation.LignesImportation)
            {
                if (ligne.StatutLigne != StatutLigneImportation.Complete)
                {
                    ligne.StatutLigne = StatutLigneImportation.ClotureeForcee;
                    if (!ligne.EstAffecteStock && ligne.QuantiteRecue > 0)
                    {
                        ligne.EstAffecteStock = true;
                    }
                }
            }

            importation.Statut = StatutImportation.Recue;
            importation.DateReceptionReelle = DateTime.Now;
            importation.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Importation clôturée avec succès" });
        }

        [HttpPost("{id}/AffecterCommandes")]
        [RequireModulePermission("importations", requireWrite: true)]
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
                        stock.CommandeClientId = ligne.CommandeClientId;
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

            return Ok(new
            {
                message = "Affectation aux commandes terminée",
                affectations
            });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("importations", requireWrite: true)]
        public async Task<IActionResult> PutImportation(int id, UpdateImportationDto dto)
        {
            var importation = await _context.Importations.FindAsync(id);
            if (importation == null)
            {
                return NotFound();
            }

            if (dto.FournisseurId.HasValue && dto.PlateformeId.HasValue)
            {
                return BadRequest("La source de l'importation doit être soit un fournisseur, soit une plateforme, pas les deux.");
            }

            if (dto.FournisseurId.HasValue && !await _context.Fournisseurs.AnyAsync(f => f.Id == dto.FournisseurId.Value))
            {
                return BadRequest("Fournisseur introuvable");
            }

            importation.FournisseurId = dto.FournisseurId;
            importation.PlateformeId = dto.PlateformeId;
            importation.DateReceptionPrevue = dto.DateReceptionPrevue;
            importation.ModeExpedition = dto.ModeExpedition;
            importation.Devise = dto.Devise;
            importation.NotesImportation = dto.NotesImportation;
            importation.DateMiseAJour = DateTime.Now;

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

        [HttpDelete("{id}")]
        [RequireModulePermission("importations", requireWrite: true)]
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
            var references = _context.Importations
                .Where(i => i.ReferenceImportation.StartsWith(prefix))
                .Select(i => i.ReferenceImportation)
                .ToList();
            var maxSuffix = references
                .Where(n => n.Length > prefix.Length)
                .Select(n => int.TryParse(n.Substring(prefix.Length), out var suffix) ? suffix : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{(maxSuffix + 1):D4}";
        }

        private static bool EstConflitReferenceImportation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pg
                && pg.SqlState == "23505"
                && string.Equals(pg.ConstraintName, "IX_Importations_ReferenceImportation", StringComparison.Ordinal);
        }
    }
}
