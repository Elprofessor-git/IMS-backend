using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module « Fournitures liées aux pièces coupées » + sous-traitance multi-chaînes.
    /// Rattachement à la ligne de commande fourniture (CommandeFournitureLigneId) —
    /// les réceptions comme les envois portent ce FK (modèle EF).
    /// Plafond d'envoi = par ligne fourniture : Σ envois (toutes chaînes confondues)
    /// ≤ Σ réceptions de la ligne ; ForcerDepassement en garde-fou (identique au
    /// pattern RapportCoupeController).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FournitureCommandeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FournitureCommandeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────── C1 — Matelas ─────────────────────────────

        [HttpGet("CommandeClient/{commandeId}/Matelas")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MatelasDto>>> GetMatelas(int commandeId)
        {
            var commandeExiste = await _context.CommandesClients.AnyAsync(c => c.Id == commandeId);
            if (!commandeExiste)
                return NotFound(new { message = "Commande introuvable." });

            var matelas = await _context.Matelas
                .Where(m => m.CommandeId == commandeId)
                .OrderByDescending(m => m.DateMatelas)
                .Select(m => new MatelasDto
                {
                    Id = m.Id,
                    CommandeId = commandeId,
                    NumeroMatelas = m.NumeroMatelas,
                    DateMatelas = m.DateMatelas,
                    PiecePliage = m.PiecePliage,
                    CoupeEstimee = m.CoupeEstimee,
                    Notes = m.Notes,
                    EstActif = m.EstActif,
                    NombreCoupes = m.LotCoupes.Where(lc => lc.CommandeId == commandeId).Sum(lc => lc.QuantiteCoupee),
                })
                .ToListAsync();
            return Ok(matelas);
        }

        [HttpPost("CommandeClient/{commandeId}/Matelas")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> CreerMatelas(int commandeId, [FromBody] CreateMatelasDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NumeroMatelas))
                return BadRequest(new { message = "Le numéro du matelas est requis." });

            var commandeExiste = await _context.CommandesClients.AnyAsync(c => c.Id == commandeId);
            if (!commandeExiste)
                return NotFound(new { message = "Commande introuvable." });

            // Unicité PAR COMMANDE (insensible casse/espaces) — règle partagée avec
            // MatelasController.Create/Update (voir 20260922090000_MatelasUniciteParCommande).
            // Deux commandes différentes peuvent porter chacune un matelas « M1 ».
            var cle = string.Concat(dto.NumeroMatelas.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
            var numeros = await _context.Matelas
                .Where(m => m.CommandeId == commandeId)
                .Select(m => m.NumeroMatelas)
                .ToListAsync();
            if (numeros.Any(n => string.Concat(n.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant() == cle))
                return Conflict(new { message = $"Un matelas numéro '{dto.NumeroMatelas}' existe déjà pour cette commande." });

            var matelas = new Matelas
            {
                CommandeId = commandeId,
                NumeroMatelas = dto.NumeroMatelas,
                DateMatelas = dto.DateMatelas ?? DateTime.Now,
                PiecePliage = dto.PiecePliage,
                CoupeEstimee = dto.CoupeEstimee,
                Notes = dto.Notes,
                EstActif = true,
            };
            _context.Matelas.Add(matelas);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Matelas créé", id = matelas.Id });
        }

        // ─────────────────────────── C3 — Nomenclature fournitures ───────────────────────────

        [HttpGet("CommandeClient/{commandeId}/FournitureCommandeLigne")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<FournitureCommandeLigneDto>>> GetNomenclature(int commandeId)
        {
            var lignes = await _context.FournitureCommandesLignes
                .Where(f => f.CommandeId == commandeId)
                .OrderBy(f => f.ArticleId)
                .Select(f => new FournitureCommandeLigneDto
                {
                    Id = f.Id,
                    CommandeId = f.CommandeId,
                    ArticleId = f.ArticleId,
                    ArticleDesignation = f.Article.Designation,
                    Portee = f.Portee.ToString(),
                    DesignationSpecifique = f.DesignationSpecifique,
                    QuantiteFourniture = f.QuantiteFourniture,
                    Taille = f.Taille,
                    Unite = f.Unite,
                    Notes = f.Notes,
                    TotalRecu = f.Receptions.Sum(r => (decimal?)r.QuantiteRecue) ?? 0,
                    TotalEnvoye = f.Envois.Sum(e => (decimal?)e.QuantiteEnvoyee) ?? 0,
                })
                .ToListAsync();
            return Ok(lignes);
        }

        [HttpPost("CommandeClient/{commandeId}/FournitureCommandeLigne")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> CreerLigneNomenclature(int commandeId, [FromBody] CreateFournitureCommandeLigneDto dto)
        {
            var commandeExiste = await _context.CommandesClients.AnyAsync(c => c.Id == commandeId);
            if (!commandeExiste)
                return NotFound(new { message = "Commande introuvable." });

            PorteeFourniture portee;
            if (!Enum.TryParse<PorteeFourniture>(dto.Portee, out portee))
                return BadRequest(new { message = $"Portée invalide : '{dto.Portee}'. Valeurs attendues : Commune, ParTaille." });

            if (portee == PorteeFourniture.ParTaille && string.IsNullOrWhiteSpace(dto.Taille))
                return BadRequest(new { message = "Une taille est requise pour une ligne « ParTaille »." });

            if (dto.QuantiteFourniture <= 0)
                return BadRequest(new { message = "La quantité par pièce doit être > 0." });

            var article = await _context.Articles.FindAsync(dto.ArticleId);
            if (article == null)
                return BadRequest(new { message = "Article introuvable." });

            var ligne = new FournitureCommandeLigne
            {
                CommandeId = commandeId,
                ArticleId = dto.ArticleId,
                Portee = portee,
                DesignationSpecifique = dto.DesignationSpecifique,
                QuantiteFourniture = dto.QuantiteFourniture,
                Taille = dto.Taille,
                Unite = dto.Unite,
                Notes = dto.Notes,
            };
            _context.FournitureCommandesLignes.Add(ligne);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ligne de nomenclature fourniture ajoutée", id = ligne.Id });
        }

        [HttpPut("CommandeClient/{commandeId}/FournitureCommandeLigne/{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> ModifierLigneNomenclature(int commandeId, int id, [FromBody] UpdateFournitureCommandeLigneDto dto)
        {
            var ligne = await _context.FournitureCommandesLignes
                .FirstOrDefaultAsync(f => f.Id == id && f.CommandeId == commandeId);
            if (ligne == null)
                return NotFound(new { message = "Ligne de nomenclature fourniture introuvable." });

            var portee = ligne.Portee;
            if (dto.Portee != null)
            {
                if (!Enum.TryParse<PorteeFourniture>(dto.Portee, out portee))
                    return BadRequest(new { message = $"Portée invalide : '{dto.Portee}'. Valeurs attendues : Commune, ParTaille." });
            }

            var taille = dto.Taille ?? ligne.Taille;
            if (portee == PorteeFourniture.ParTaille && string.IsNullOrWhiteSpace(taille))
                return BadRequest(new { message = "Une taille est requise pour une ligne « ParTaille »." });

            if (dto.QuantiteFourniture.HasValue && dto.QuantiteFourniture.Value <= 0)
                return BadRequest(new { message = "La quantité par pièce doit être > 0." });

            if (dto.ArticleId.HasValue && dto.ArticleId.Value != ligne.ArticleId)
            {
                var article = await _context.Articles.FindAsync(dto.ArticleId.Value);
                if (article == null)
                    return BadRequest(new { message = "Article introuvable." });
                ligne.ArticleId = dto.ArticleId.Value;
            }

            ligne.Portee = portee;
            ligne.Taille = taille;
            if (dto.DesignationSpecifique != null) ligne.DesignationSpecifique = dto.DesignationSpecifique;
            if (dto.QuantiteFourniture.HasValue) ligne.QuantiteFourniture = dto.QuantiteFourniture.Value;
            if (dto.Unite != null) ligne.Unite = dto.Unite;
            if (dto.Notes != null) ligne.Notes = dto.Notes;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne de nomenclature fourniture mise à jour" });
        }

        [HttpDelete("CommandeClient/{commandeId}/FournitureCommandeLigne/{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> SupprimerLigneNomenclature(int commandeId, int id)
        {
            var ligne = await _context.FournitureCommandesLignes
                .FirstOrDefaultAsync(f => f.Id == id && f.CommandeId == commandeId);
            if (ligne == null)
                return NotFound(new { message = "Ligne de nomenclature fourniture introuvable." });

            _context.FournitureCommandesLignes.Remove(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne de nomenclature fourniture supprimée" });
        }

        // ─────────────────────────── C4 — Réception fourniture (sans plafond) ───────────────────────────

        [HttpGet("CommandeClient/{commandeId}/ReceptionFourniture")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<ReceptionFournitureDto>>> GetReceptions(int commandeId)
        {
            var receptions = await _context.ReceptionsFourniture
                .Where(r => r.CommandeLigne.CommandeId == commandeId)
                .OrderByDescending(r => r.DateReception)
                .Select(r => new ReceptionFournitureDto
                {
                    Id = r.Id,
                    CommandeFournitureLigneId = r.CommandeFournitureLigneId,
                    CommandeId = commandeId,
                    ArticleId = r.CommandeLigne.ArticleId,
                    ArticleDesignation = r.CommandeLigne.Article.Designation,
                    Taille = r.CommandeLigne.Taille,
                    QuantiteRecue = r.QuantiteRecue,
                    DateReception = r.DateReception,
                    EffectuePar = r.EffectuePar,
                    Notes = r.Notes,
                })
                .ToListAsync();
            return Ok(receptions);
        }

        [HttpPost("CommandeClient/{commandeId}/ReceptionFourniture")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> RejeterReceptionFourniture(int commandeId, [FromBody] CreateReceptionFournitureDto dto)
        {
            if (dto.CommandeFournitureLigneId <= 0)
                return BadRequest(new { message = "CommandeFournitureLigneId requis." });

            var ligne = await _context.FournitureCommandesLignes
                .FirstOrDefaultAsync(f => f.Id == dto.CommandeFournitureLigneId && f.CommandeId == commandeId);
            if (ligne == null)
                return NotFound(new { message = "Ligne de nomenclature fourniture introuvable pour cette commande." });

            if (dto.QuantiteRecue <= 0)
                return BadRequest(new { message = "La quantité reçue doit être > 0." });

            var reception = new ReceptionFourniture
            {
                CommandeFournitureLigneId = dto.CommandeFournitureLigneId,
                QuantiteRecue = dto.QuantiteRecue,
                DateReception = dto.DateReception ?? DateTime.Now,
                EffectuePar = dto.EffectuePar ?? User.Identity?.Name,
                Notes = dto.Notes,
            };
            _context.ReceptionsFourniture.Add(reception);
            await _context.SaveChangesAsync();

            var totalRecu = await _context.ReceptionsFourniture
                .Where(r => r.CommandeFournitureLigneId == dto.CommandeFournitureLigneId)
                .SumAsync(r => (decimal?)r.QuantiteRecue) ?? 0;

            return Ok(new { message = "Réception fourniture enregistrée", id = reception.Id, totalRecu });
        }

        // ─────────────────────────── C5 — Envoi fourniture (plafond + ForcerDepassement) ───────────────────────────

        [HttpGet("CommandeClient/{commandeId}/EnvoiFourniture")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<EnvoiFournitureDto>>> GetEnvois(int commandeId)
        {
            var envois = await _context.EnvoisFourniture
                .Where(e => e.CommandeLigne.CommandeId == commandeId)
                .OrderByDescending(e => e.DateEnvoi)
                .Select(e => new EnvoiFournitureDto
                {
                    Id = e.Id,
                    CommandeFournitureLigneId = e.CommandeFournitureLigneId,
                    CommandeId = commandeId,
                    ArticleId = e.CommandeLigne.ArticleId,
                    ArticleDesignation = e.CommandeLigne.Article.Designation,
                    Taille = e.CommandeLigne.Taille,
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineProductionNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    QuantiteEnvoyee = e.QuantiteEnvoyee,
                    DateEnvoi = e.DateEnvoi,
                    EffectuePar = e.EffectuePar,
                    ForcerDepassement = e.ForcerDepassement,
                    Notes = e.Notes,
                })
                .ToListAsync();
            return Ok(envois);
        }

        [HttpPost("CommandeClient/{commandeId}/EnvoiFourniture")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> EnvoyerFourniture(int commandeId, [FromBody] CreateEnvoiFournitureDto dto)
        {
            if (dto.CommandeFournitureLigneId <= 0)
                return BadRequest(new { message = "CommandeFournitureLigneId requis." });

            var ligne = await _context.FournitureCommandesLignes
                .FirstOrDefaultAsync(f => f.Id == dto.CommandeFournitureLigneId && f.CommandeId == commandeId);
            if (ligne == null)
                return NotFound(new { message = "Ligne de nomenclature fourniture introuvable pour cette commande." });

            if (dto.QuantiteEnvoyee <= 0)
                return BadRequest(new { message = "La quantité envoyée doit être > 0." });

            if (dto.ChaineProductionId.HasValue)
            {
                var chaineExiste = await _context.ChainesProduction
                    .AnyAsync(c => c.Id == dto.ChaineProductionId.Value);
                if (!chaineExiste)
                    return BadRequest(new { message = "Chaîne de production introuvable." });
            }

            var totalRecu = await _context.ReceptionsFourniture
                .Where(r => r.CommandeFournitureLigneId == dto.CommandeFournitureLigneId)
                .SumAsync(r => (decimal?)r.QuantiteRecue) ?? 0;

            var totalEnvoyeExistant = await _context.EnvoisFourniture
                .Where(e => e.CommandeFournitureLigneId == dto.CommandeFournitureLigneId)
                .SumAsync(e => (decimal?)e.QuantiteEnvoyee) ?? 0;

            var totalApres = totalEnvoyeExistant + dto.QuantiteEnvoyee;
            if (totalApres > totalRecu && !dto.ForcerDepassement)
            {
                return Conflict(new
                {
                    message = $"Dépassement d'envoi fourniture : {totalApres} > total reçu {totalRecu}. Cochez « forcer le dépassement » pour enregistrer quand même.",
                    avertissement = true,
                    totalRecu,
                    totalEnvoyeExistant,
                    depassement = totalApres - totalRecu,
                });
            }

            var envoi = new EnvoiFourniture
            {
                CommandeFournitureLigneId = dto.CommandeFournitureLigneId,
                ChaineProductionId = dto.ChaineProductionId,
                QuantiteEnvoyee = dto.QuantiteEnvoyee,
                DateEnvoi = dto.DateEnvoi ?? DateTime.Now,
                EffectuePar = dto.EffectuePar ?? User.Identity?.Name,
                ForcerDepassement = dto.ForcerDepassement,
                Notes = dto.Notes,
            };
            _context.EnvoisFourniture.Add(envoi);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Envoi fourniture enregistré", id = envoi.Id, totalEnvoye = totalApres });
        }

        // ─────────────────────────── C6 — Rapport agrégé fournitures ───────────────────────────

        [HttpGet("CommandeClient/{commandeId}/RapportFournitures")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<RapportFournituresDto>> GetRapportFournitures(int commandeId)
        {
            var commande = await _context.CommandesClients
                .FirstOrDefaultAsync(c => c.Id == commandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            // Quantités réellement coupées par taille (source du besoin — jamais ConfigTailles)
            var coupesParTaille = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId)
                .GroupBy(l => l.Taille)
                .Select(g => new { Taille = g.Key, Quantite = g.Sum(l => l.QuantiteCoupee) })
                .ToDictionaryAsync(g => g.Taille, g => g.Quantite);

            var totalPiecesCoupees = coupesParTaille.Values.Sum();

            // Pièces exportées par chaîne + taille (filtre/affichage uniquement)
            var exportsParChaineTaille = await _context.LotExports
                .Where(l => l.CommandeId == commandeId)
                .GroupBy(l => new { l.ChaineProductionId, l.Taille })
                .Select(g => new { g.Key.ChaineProductionId, g.Key.Taille, Quantite = g.Sum(l => l.QuantiteExportee) })
                .ToListAsync();

            var nomsChaines = await _context.ChainesProduction
                .Select(c => new { c.Id, c.Nom })
                .ToDictionaryAsync(c => c.Id, c => c.Nom);

            // Lignes de nomenclature + totaux reçus/envoyés (Joint à gauche sur la ligne)
            var lignes = await _context.FournitureCommandesLignes
                .Where(f => f.CommandeId == commandeId)
                .Select(f => new
                {
                    f.Id,
                    f.ArticleId,
                    Designation = f.Article.Designation,
                    f.Taille,
                    Portee = f.Portee.ToString(),
                    f.QuantiteFourniture,
                    f.Unite,
                    TotalRecu = f.Receptions.Sum(r => (decimal?)r.QuantiteRecue) ?? 0,
                    TotalEnvoye = f.Envois.Sum(e => (decimal?)e.QuantiteEnvoyee) ?? 0,
                })
                .ToListAsync();

            var rapport = new RapportFournituresDto
            {
                CommandeId = commandeId,
                NumeroCommande = commande.NumeroCommande,
                TotalMatelas = await _context.Matelas
                    .CountAsync(m => m.LotCoupes.Any(lc => lc.CommandeId == commandeId)),
                TotalPiecesCoupees = totalPiecesCoupees,
            };

            foreach (var l in lignes)
            {
                decimal besoin = 0;
                if (l.Portee == PorteeFourniture.Commune.ToString())
                {
                    besoin = l.QuantiteFourniture * totalPiecesCoupees;
                }
                else
                {
                    var coupeTaille = coupesParTaille.GetValueOrDefault(l.Taille ?? string.Empty);
                    besoin = l.QuantiteFourniture * coupeTaille;
                }

                rapport.Articles.Add(new RapportFournitureArticleDto
                {
                    CommandeFournitureLigneId = l.Id,
                    ArticleId = l.ArticleId,
                    ArticleDesignation = l.Designation,
                    Taille = l.Taille,
                    Portee = l.Portee,
                    QuantiteParPiece = l.QuantiteFourniture,
                    Unite = l.Unite,
                    BesoinCalcule = Math.Round(besoin, 4),
                    TotalRecu = l.TotalRecu,
                    TotalEnvoye = l.TotalEnvoye,
                    Ecart = Math.Round(l.TotalEnvoye - besoin, 4),
                });
            }

            // Par chaîne + taille : pièces exportées × QuantiteParPiece (Commun = toutes tailles, ParTaille = taille)
            foreach (var ex in exportsParChaineTaille)
            {
                var fournituresAttendues = 0m;
                foreach (var l in lignes)
                {
                    if (l.Portee == PorteeFourniture.Commune.ToString())
                        fournituresAttendues += l.QuantiteFourniture * ex.Quantite;
                    else if ((l.Taille ?? string.Empty) == ex.Taille)
                        fournituresAttendues += l.QuantiteFourniture * ex.Quantite;
                }

                // Fournitures envoyées vers cette chaîne, scoped sur cette taille de ligne fourniture
                // (lignes ParTaille dont la taille == ex.Taille ; les envois d'une ligne « Commune »
                // ne sont pas décomposables par taille — ils restent visibles dans le tableau par article).
                var fournituresEnvoyees = await _context.EnvoisFourniture
                    .Where(e => e.CommandeLigne.CommandeId == commandeId
                        && e.ChaineProductionId == ex.ChaineProductionId
                        && e.CommandeLigne.Taille == ex.Taille)
                    .SumAsync(e => (decimal?)e.QuantiteEnvoyee) ?? 0;

                rapport.ParChaine.Add(new RapportFournitureChaineDto
                {
                    ChaineProductionId = ex.ChaineProductionId,
                    ChaineProductionNom = ex.ChaineProductionId.HasValue
                        ? (nomsChaines.GetValueOrDefault(ex.ChaineProductionId.Value) ?? null)
                        : null,
                    Taille = ex.Taille,
                    PiècesExportees = ex.Quantite,
                    FournituresAttendues = Math.Round(fournituresAttendues, 4),
                    FournituresEnvoyees = Math.Round(fournituresEnvoyees, 4),
                    Ecart = Math.Round(fournituresEnvoyees - fournituresAttendues, 4),
                });
            }

            // Compléter avec les envois fournitures vers des chaînes/tailles sans pièces exportées
            // (agrégés par chaîne + taille pour ne pas dupliquer les lignes).
            var envoisSansExport = await _context.EnvoisFourniture
                .Where(e => e.CommandeLigne.CommandeId == commandeId)
                .GroupBy(e => new { e.ChaineProductionId, Taille = e.CommandeLigne.Taille })
                .Select(g => new
                {
                    g.Key.ChaineProductionId,
                    g.Key.Taille,
                    QuantiteEnvoyee = g.Sum(x => x.QuantiteEnvoyee),
                })
                .ToListAsync();
            foreach (var es in envoisSansExport)
            {
                if (string.IsNullOrWhiteSpace(es.Taille))
                    continue; // envoi d'une ligne « Commune » : non décomposable par taille
                var existe = rapport.ParChaine.Any(p =>
                    p.ChaineProductionId == es.ChaineProductionId && p.Taille == es.Taille);
                if (!existe)
                {
                    var attendues = 0m;
                    foreach (var l in lignes)
                    {
                        if ((l.Taille ?? string.Empty) == es.Taille)
                            attendues += l.QuantiteFourniture * coupesParTaille.GetValueOrDefault(es.Taille);
                    }
                    if (attendues > 0 || es.QuantiteEnvoyee > 0)
                    {
                        rapport.ParChaine.Add(new RapportFournitureChaineDto
                        {
                            ChaineProductionId = es.ChaineProductionId,
                            ChaineProductionNom = es.ChaineProductionId.HasValue
                                ? (nomsChaines.GetValueOrDefault(es.ChaineProductionId.Value) ?? null)
                                : null,
                            Taille = es.Taille,
                            PiècesExportees = coupesParTaille.GetValueOrDefault(es.Taille),
                            FournituresAttendues = Math.Round(attendues, 4),
                            FournituresEnvoyees = Math.Round(es.QuantiteEnvoyee, 4),
                            Ecart = Math.Round(es.QuantiteEnvoyee - attendues, 4),
                        });
                    }
                }
            }

            return Ok(rapport);
        }
    }
}