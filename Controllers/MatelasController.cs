using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module « Coupe » transversal : vue globale de tous les matelas (toutes commandes),
    /// avec accès direct au rapport de coupe de leur commande via le frontend.
    /// Lecture — permission « coupe » (module dédié, distinct de « commandes »).
    /// Création de matelas partagée avec le suivi fournitures (module « commandes »).
    /// Garde-fous : unicité du numéro de matelas PAR COMMANDE (CommandeId + numéro,
    /// insensible casse/espaces — même règle que FournitureCommandeController.CreerMatelas) ;
    /// PUT/DELETE refusés (409) dès qu'un matelas porte des coupes enregistrées
    /// (l'historique ne se réécrit pas).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MatelasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MatelasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Unicité (CommandeId, NumeroMatelas) insensible casse/espaces : comparaison
        // normalisée (minuscules sans blancs) sur les matelas de la MÊME commande.
        // L'index unique physique correspondant est porté par la migration
        // 20260922090000_MatelasUniciteParCommande (fonctionnel UPPER+blancs retirés).
        private static string NormaliserNumeroMatelas(string n) =>
            string.Concat(n.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

        private async Task<bool> NumeroDejaPris(int? commandeId, string numero, int? matelasExclu = null)
        {
            var cle = NormaliserNumeroMatelas(numero);
            var numeros = await _context.Matelas
                .Where(m => m.CommandeId == commandeId && (matelasExclu == null || m.Id != matelasExclu.Value))
                .Select(m => m.NumeroMatelas)
                .ToListAsync();
            return numeros.Any(n => NormaliserNumeroMatelas(n) == cle);
        }

        [HttpGet]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<IEnumerable<MatelasGlobalDto>>> GetAll()
        {
            // Tous les matelas, y compris les partagés/historiques sans commande
            // (CommandeId NULL) : NumeroCommande vide dans ce cas.
            var matelas = await _context.Matelas
                .OrderBy(m => m.DateMatelas)
                .ThenBy(m => m.NumeroMatelas)
                .Select(m => new MatelasGlobalDto
                {
                    Id = m.Id,
                    CommandeId = m.CommandeId,
                    NumeroCommande = m.Commande != null ? m.Commande.NumeroCommande : string.Empty,
                    NumeroMatelas = m.NumeroMatelas,
                    DateMatelas = m.DateMatelas,
                    PiecePliage = m.PiecePliage,
                    CoupeEstimee = m.CoupeEstimee,
                    Longueur = m.Longueur,
                    Laize = m.Laize,
                    NombreCoupes = m.LotCoupes.Sum(lc => (int?)lc.QuantiteCoupee) ?? 0,
                    TotalPiecesCommandees = m.Commande != null
                        ? m.Commande.ConfigTailles.Sum(ct => (int?)ct.Quantite) ?? 0
                        : 0,
                    TotalPlanTheorique = m.PlanDeCoupeLignes.Sum(p => (int?)(p.Occurrences * m.PiecePliage)) ?? 0,
                    Notes = m.Notes,
                    EstActif = m.EstActif,
                })
                .ToListAsync();

            // Ordre de coupe = rang dans la séquence (DateMatelas, NumeroMatelas) croissant.
            // Vue calculée, aucune table de planning dédiée.
            for (var i = 0; i < matelas.Count; i++)
                matelas[i].OrdreDeCoupe = i + 1;

            return Ok(matelas.OrderByDescending(m => m.DateMatelas));
        }

        // Totaux globaux du module Coupe (toutes commandes confondues).
        [HttpGet("Stats")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<MatelasStatsDto>> GetStats()
        {
            var piecesCommandees = await _context.CommandesClients
                .SelectMany(c => c.ConfigTailles)
                .SumAsync(ct => (int?)ct.Quantite) ?? 0;

            var dto = new MatelasStatsDto
            {
                TotalMatelas = await _context.Matelas.CountAsync(),
                TotalMatelasActifs = await _context.Matelas.CountAsync(m => m.EstActif),
                TotalPiecesCommandees = piecesCommandees,
                TotalPiecesCoupees = await _context.LotCoupes.SumAsync(l => (int?)l.QuantiteCoupee) ?? 0,
                TotalPiecesExportees = await _context.LotExports.SumAsync(l => (int?)l.QuantiteExportee) ?? 0,
            };

            return Ok(dto);
        }

        // Journal du jour : coupes enregistrées aujourd'hui, toutes commandes confondues.
        // Alimente le tableau de bord global du module Coupe — lecture seule, aucune écriture.
        [HttpGet("CoupesDuJour")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<JournalCoupeDto>> GetCoupesDuJour()
        {
            var aujourdhui = DateTime.Today;

            var lignes = await _context.LotCoupes
                .Where(l => l.DateCoupe >= aujourdhui && l.DateCoupe < aujourdhui.AddDays(1))
                .OrderByDescending(l => l.DateCoupe)
                .ThenByDescending(l => l.Id)
                .Select(l => new JournalCoupeLigneDto
                {
                    Id = l.Id,
                    CommandeId = l.CommandeId,
                    NumeroCommande = l.Commande != null ? l.Commande.NumeroCommande : string.Empty,
                    Taille = l.Taille,
                    QuantiteCoupee = l.QuantiteCoupee,
                    DateCoupe = l.DateCoupe,
                    EffectuePar = l.EffectuePar,
                    ForcerDepassement = l.ForcerDepassement,
                    MatelasId = l.MatelasId,
                    MatelasNumero = l.Matelas != null ? l.Matelas.NumeroMatelas : null,
                })
                .ToListAsync();

            return Ok(new JournalCoupeDto
            {
                Date = aujourdhui,
                NombreLignes = lignes.Count,
                TotalQuantite = lignes.Sum(l => l.QuantiteCoupee),
                Lignes = lignes,
            });
        }

        [HttpPost]
        [RequireModulePermission("commandes,coupe", requireWrite: true)]
        public async Task<ActionResult<Matelas>> Create(CreateMatelasDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NumeroMatelas))
                return BadRequest(new { message = "Le numéro du matelas est requis." });

            var commandeExiste = await _context.CommandesClients.AnyAsync(c => c.Id == dto.CommandeId);
            if (!commandeExiste)
                return NotFound(new { message = "Commande introuvable." });

            // Unicité par commande (insensible casse/espaces) — règle partagée avec
            // FournitureCommandeController.CreerMatelas. Deux commandes différentes
            // peuvent porter chacune un matelas « M1 ».
            if (await NumeroDejaPris(dto.CommandeId, dto.NumeroMatelas))
                return Conflict(new { message = $"Un matelas numéro '{dto.NumeroMatelas}' existe déjà pour cette commande." });

            var matelas = new Matelas
            {
                CommandeId = dto.CommandeId,
                NumeroMatelas = dto.NumeroMatelas,
                DateMatelas = dto.DateMatelas ?? DateTime.Now,
                PiecePliage = dto.PiecePliage,
                CoupeEstimee = dto.CoupeEstimee,
                Longueur = dto.Longueur,
                Laize = dto.Laize ?? await LaizeParDefautAsync(dto.CommandeId),
                Notes = dto.Notes,
            };

            _context.Matelas.Add(matelas);
            await _context.SaveChangesAsync();

            return Created($"/api/Matelas/{matelas.Id}", matelas);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateMatelasDto dto)
        {
            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            // Garde-fou : un matelas qui a déjà servi à des coupes est verrouillé.
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == id))
                return Conflict(new { message = "Impossible de modifier un matelas qui a des coupes enregistrées." });

            var nouveauNumero = dto.NumeroMatelas ?? matelas.NumeroMatelas;
            if (string.IsNullOrWhiteSpace(nouveauNumero))
                return BadRequest(new { message = "Le numéro du matelas est requis." });

            // Unicité par commande (règle partagée) — hors matelas lui-même,
            // sur la commande cible (après un éventuel changement de commande).
            var commandeCible = dto.CommandeId ?? matelas.CommandeId;
            if (await NumeroDejaPris(commandeCible, nouveauNumero, matelas.Id))
                return Conflict(new { message = $"Un matelas numéro '{nouveauNumero}' existe déjà pour cette commande." });

            if (dto.CommandeId.HasValue && dto.CommandeId.Value != matelas.CommandeId)
            {
                if (!await _context.CommandesClients.AnyAsync(c => c.Id == dto.CommandeId.Value))
                    return NotFound(new { message = "Commande introuvable." });
                matelas.CommandeId = dto.CommandeId.Value;
            }

            if (dto.NumeroMatelas != null) matelas.NumeroMatelas = nouveauNumero;
            if (dto.DateMatelas.HasValue) matelas.DateMatelas = dto.DateMatelas.Value;
            if (dto.PiecePliage.HasValue) matelas.PiecePliage = dto.PiecePliage.Value;
            if (dto.CoupeEstimee.HasValue) matelas.CoupeEstimee = dto.CoupeEstimee.Value;
            if (dto.Longueur.HasValue) matelas.Longueur = dto.Longueur.Value;
            if (dto.Laize.HasValue) matelas.Laize = dto.Laize.Value;
            if (dto.Notes != null) matelas.Notes = dto.Notes;
            if (dto.EstActif.HasValue) matelas.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Matelas mis à jour", id = matelas.Id });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Delete(int id)
        {
            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            // Garde-fou : pas de suppression tant que le matelas porte des coupes —
            // l'historique des coupes n'est jamais réécrit (conservation SetNull sinon).
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == id))
                return Conflict(new { message = "Impossible de supprimer un matelas qui a des coupes enregistrées." });

            _context.Matelas.Remove(matelas);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Matelas supprimé" });
        }

        // ───────────────────────────── Plan de coupe (L1) ─────────────────────────────
        // Le plan de coupe (marker) d'un matelas est la référence atelier : une ligne par
        // gabarit/taille, Occurrences × PiecePliage = quantité théorique. Verrous identiques
        // au matelas lui-même : immuable dès qu'une coupe réelle y est rattachée (409).

        /// <summary>
        /// Laize par défaut d'un matelas : héritée de Article.Laize d'une BOM tissu de la
        /// commande (premier tissu consommable trouvé). Libre ensuite sur le matelas.
        /// </summary>
        private async Task<decimal?> LaizeParDefautAsync(int commandeId)
        {
            return await _context.BomLignes
                .Where(b => b.CommandeId == commandeId && b.EstConsommableTissu && b.Article != null && b.Article.Laize.HasValue)
                .Select(b => b.Article!.Laize)
                .FirstOrDefaultAsync();
        }

        [HttpGet("{id}/PlanDeCoupe")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<IEnumerable<PlanDeCoupeLigneDto>>> GetPlanDeCoupe(int id)
        {
            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            var lignes = await _context.PlanDeCoupeLignes
                .Where(p => p.MatelasId == id)
                .OrderBy(p => p.Taille)
                .Select(p => new PlanDeCoupeLigneDto
                {
                    Id = p.Id,
                    MatelasId = p.MatelasId,
                    Taille = p.Taille,
                    Occurrences = p.Occurrences,
                    Notes = p.Notes,
                    Theorique = p.Occurrences * matelas.PiecePliage,
                })
                .ToListAsync();

            return Ok(lignes);
        }

        [HttpPost("{id}/PlanDeCoupe")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterLignePlan(int id, [FromBody] CreatePlanDeCoupeLigneDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.Occurrences < 1)
                return BadRequest(new { message = "Taille requise et occurrences ≥ 1." });

            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            // Verrou L1 : un matelas qui a déjà servi à des coupes a un plan figé.
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == id))
                return Conflict(new { message = "Impossible de modifier le plan d'un matelas qui a des coupes enregistrées." });

            var tailleValide = matelas.CommandeId.HasValue
                && await _context.ConfigTailles.AnyAsync(ct => ct.CommandeId == matelas.CommandeId.Value && ct.Taille == dto.Taille);
            if (matelas.CommandeId.HasValue && !tailleValide)
                return BadRequest(new { message = $"Taille '{dto.Taille}' absente de la configuration de la commande." });

            var existe = await _context.PlanDeCoupeLignes.AnyAsync(p => p.MatelasId == id && p.Taille == dto.Taille);
            if (existe)
                return Conflict(new { message = $"Une ligne de plan existe déjà pour la taille '{dto.Taille}' sur ce matelas." });

            var ligne = new PlanDeCoupeLigne
            {
                MatelasId = id,
                Taille = dto.Taille,
                Occurrences = dto.Occurrences,
                Notes = dto.Notes,
            };
            _context.PlanDeCoupeLignes.Add(ligne);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Ligne de plan enregistrée",
                id = ligne.Id,
                taille = ligne.Taille,
                occurrences = ligne.Occurrences,
                theorique = ligne.Occurrences * matelas.PiecePliage
            });
        }

        [HttpPut("PlanDeCoupe/{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> ModifierLignePlan(int id, [FromBody] UpdatePlanDeCoupeLigneDto dto)
        {
            var ligne = await _context.PlanDeCoupeLignes
                .Include(p => p.Matelas)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (ligne == null || ligne.Matelas == null)
                return NotFound(new { message = "Ligne de plan introuvable." });

            // Verrou L1 : plan figé dès qu'une coupe réelle existe sur le matelas.
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == ligne.MatelasId))
                return Conflict(new { message = "Impossible de modifier le plan d'un matelas qui a des coupes enregistrées." });

            var nouvelleTaille = dto.Taille ?? ligne.Taille;
            if (string.IsNullOrWhiteSpace(nouvelleTaille))
                return BadRequest(new { message = "La taille est requise." });

            if (ligne.Matelas.CommandeId.HasValue)
            {
                var tailleValide = await _context.ConfigTailles.AnyAsync(ct => ct.CommandeId == ligne.Matelas.CommandeId.Value && ct.Taille == nouvelleTaille);
                if (!tailleValide)
                    return BadRequest(new { message = $"Taille '{nouvelleTaille}' absente de la configuration de la commande." });
            }

            if (nouvelleTaille != ligne.Taille)
            {
                var existe = await _context.PlanDeCoupeLignes.AnyAsync(p => p.MatelasId == ligne.MatelasId && p.Taille == nouvelleTaille && p.Id != id);
                if (existe)
                    return Conflict(new { message = $"Une ligne de plan existe déjà pour la taille '{nouvelleTaille}' sur ce matelas." });
            }

            if (dto.Occurrences.HasValue && dto.Occurrences.Value < 1)
                return BadRequest(new { message = "Occurrences ≥ 1." });

            ligne.Taille = nouvelleTaille;
            if (dto.Occurrences.HasValue) ligne.Occurrences = dto.Occurrences.Value;
            if (dto.Notes != null) ligne.Notes = dto.Notes;

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Ligne de plan mise à jour",
                id = ligne.Id,
                taille = ligne.Taille,
                occurrences = ligne.Occurrences,
                theorique = ligne.Occurrences * ligne.Matelas.PiecePliage
            });
        }

        [HttpDelete("PlanDeCoupe/{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerLignePlan(int id)
        {
            var ligne = await _context.PlanDeCoupeLignes.FindAsync(id);
            if (ligne == null)
                return NotFound(new { message = "Ligne de plan introuvable." });

            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == ligne.MatelasId))
                return Conflict(new { message = "Impossible de modifier le plan d'un matelas qui a des coupes enregistrées." });

            _context.PlanDeCoupeLignes.Remove(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne de plan supprimée" });
        }
    }
}