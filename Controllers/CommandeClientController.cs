using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        [HttpGet]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<CommandeListDto>>> GetCommandes([FromQuery] string? recherche)
        {
            var query = _context.CommandesClients.AsQueryable();

            query = AppliquerRecherche(query, recherche);

            return await query.Select(c => new CommandeListDto
            {
                Id = c.Id,
                NumeroCommande = c.NumeroCommande,
                TitreCommande = c.TitreCommande,
                Statut = c.Statut,
                PourcentageRessourcesCouvertes = c.PourcentageRessourcesCouvertes,
                DateLivraisonSouhaitee = c.DateLivraisonSouhaitee,
                ClientId = c.ClientId,
                Client = c.Client != null ? new CommandeClientInfoDto
                {
                    Id = c.Client.Id,
                    Nom = c.Client.Nom,
                    Plateforme = c.Client.Plateforme != null ? new CommandePlateformeInfoDto
                    {
                        Nom = c.Client.Plateforme.Nom
                    } : null
                } : null
            }).ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<CommandeDetailDto>> GetCommandeClient(int id)
        {
            var dto = await _context.CommandesClients
                .Where(c => c.Id == id)
                .Select(c => new CommandeDetailDto
                {
                    Id = c.Id,
                    NumeroCommande = c.NumeroCommande,
                    TitreCommande = c.TitreCommande,
                    DescriptionCommande = c.DescriptionCommande,
                    Statut = c.Statut,
                    DateCommande = c.DateCommande,
                    DateLivraisonSouhaitee = c.DateLivraisonSouhaitee,
                    ClientId = c.ClientId,
                    MontantTotal = c.MontantTotal,
                    Devise = c.Devise,
                    PourcentageRessourcesCouvertes = c.PourcentageRessourcesCouvertes,
                    NotesSpeciales = c.NotesSpeciales,
                    SpecificationsClient = c.SpecificationsClient,
                    DateCreation = c.DateCreation,
                    DateMiseAJour = c.DateMiseAJour,
                    CreePar = c.CreePar,
                    ModifiePar = c.ModifiePar,
                    Client = c.Client != null ? new CommandeDetailClientDto
                    {
                        Id = c.Client.Id,
                        Nom = c.Client.Nom,
                        Prenom = c.Client.Prenom,
                        Plateforme = c.Client.Plateforme != null ? new CommandePlateformeInfoDto
                        {
                            Nom = c.Client.Plateforme.Nom
                        } : null
                    } : null,
                    Besoins = c.Besoins.Select(b => new BesoinCommandeDto
                    {
                        Id = b.Id,
                        CommandeClientId = b.CommandeClientId,
                        ArticleId = b.ArticleId,
                        TypeBesoin = b.TypeBesoin,
                        Couleur = b.Couleur,
                        Taille = b.Taille,
                        Dimension = b.Dimension,
                        QuantiteUnitaire = b.QuantiteUnitaire,
                        NombrePieces = b.NombrePieces,
                        QuantiteTotale = b.QuantiteTotale,
                        QuantiteCouverte = b.QuantiteCouverte,
                        QuantiteStockImporte = b.QuantiteStockImporte,
                        QuantiteAchatsLocaux = b.QuantiteAchatsLocaux,
                        QuantiteStockLibre = b.QuantiteStockLibre,
                        EstCompletementCouvert = b.EstCompletementCouvert,
                        Notes = b.Notes,
                        DateCreation = b.DateCreation,
                        Article = b.Article != null ? new BesoinArticleDto
                        {
                            Id = b.Article.Id,
                            Designation = b.Article.Designation,
                            Reference = b.Article.Reference
                        } : null
                    }).ToList(),
                    ConfigTailles = c.ConfigTailles.Select(ct => new ConfigTailleItemDto
                    {
                        Id = ct.Id,
                        CommandeId = ct.CommandeId,
                        Taille = ct.Taille,
                        Quantite = ct.Quantite
                    }).ToList(),
                    BomLignes = c.BomLignes.Select(bl => new BomLigneItemDto
                    {
                        Id = bl.Id,
                        CommandeId = bl.CommandeId,
                        ArticleId = bl.ArticleId,
                        QuantiteParPiece = bl.QuantiteParPiece,
                        Unite = bl.Unite,
                        Article = bl.Article != null ? new BesoinArticleDto
                        {
                            Id = bl.Article.Id,
                            Designation = bl.Article.Designation,
                            Reference = bl.Article.Reference
                        } : null
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (dto == null)
            {
                return NotFound();
            }

            return dto;
        }

        [HttpGet("Statut/{statut}")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<CommandeListDto>>> GetCommandesByStatut(StatutCommande statut, [FromQuery] string? recherche)
        {
            var query = _context.CommandesClients
                .Where(c => c.Statut == statut);

            query = AppliquerRecherche(query, recherche);

            return await query.Select(c => new CommandeListDto
            {
                Id = c.Id,
                NumeroCommande = c.NumeroCommande,
                TitreCommande = c.TitreCommande,
                Statut = c.Statut,
                PourcentageRessourcesCouvertes = c.PourcentageRessourcesCouvertes,
                DateLivraisonSouhaitee = c.DateLivraisonSouhaitee,
                ClientId = c.ClientId,
                Client = c.Client != null ? new CommandeClientInfoDto
                {
                    Id = c.Client.Id,
                    Nom = c.Client.Nom,
                    Plateforme = c.Client.Plateforme != null ? new CommandePlateformeInfoDto
                    {
                        Nom = c.Client.Plateforme.Nom
                    } : null
                } : null
            }).ToListAsync();
        }

        // Recherche texte combinable (façon Excel) : numéro, titre, client ou marque.
        private static IQueryable<CommandeClient> AppliquerRecherche(IQueryable<CommandeClient> query, string? recherche)
        {
            if (string.IsNullOrWhiteSpace(recherche))
                return query;

            var terme = recherche.Trim().ToLower();
            return query.Where(c =>
                c.NumeroCommande.ToLower().Contains(terme) ||
                (c.TitreCommande != null && c.TitreCommande.ToLower().Contains(terme)) ||
                c.Client.Nom.ToLower().Contains(terme));
        }

        [HttpGet("{id}/Tailles")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<ConfigTaille>>> GetTailles(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            return await _context.ConfigTailles
                .Where(ct => ct.CommandeId == id)
                .OrderBy(ct => ct.Taille)
                .ToListAsync();
        }

        [HttpGet("{id}/Bom")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<BomLigne>>> GetBom(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            return await _context.BomLignes
                .Include(b => b.Article)
                .Where(b => b.CommandeId == id)
                .ToListAsync();
        }

        [HttpGet("{id}/ResultatCalcul")]
        [RequireModulePermission("commandes", requireWrite: false)]
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

        [HttpPost]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult<CommandeClient>> PostCommandeClient(CreateCommandeClientDto dto)
        {
            var commande = new CommandeClient
            {
                ClientId = dto.ClientId,
                TitreCommande = dto.TitreCommande,
                DescriptionCommande = dto.DescriptionCommande,
                DateCommande = DateTime.Now,
                DateLivraisonSouhaitee = dto.DateLivraisonSouhaitee,
                Devise = dto.Devise ?? "EUR",
                NotesSpeciales = dto.NotesSpeciales,
                SpecificationsClient = dto.SpecificationsClient,
                CreePar = dto.CreePar,
                DateCreation = DateTime.Now,
                NumeroCommande = GenerateNumeroCommande()
            };

            _context.CommandesClients.Add(commande);

            const int maxTentatives = 5;
            for (var tentative = 1; ; tentative++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (EstConflitNumeroCommande(ex) && tentative < maxTentatives)
                {
                    commande.NumeroCommande = GenerateNumeroCommande();
                }
            }

            return CreatedAtAction("GetCommandeClient", new { id = commande.Id }, commande);
        }

        [HttpPost("{id}/Besoins")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult<BesoinCommande>> AjouterBesoin(int id, CreateBesoinCommandeDto dto)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null)
            {
                return NotFound();
            }

            var besoin = new BesoinCommande
            {
                CommandeClientId = id,
                ArticleId = dto.ArticleId,
                TypeBesoin = dto.TypeBesoin,
                Couleur = dto.Couleur,
                Taille = dto.Taille,
                Dimension = dto.Dimension,
                QuantiteUnitaire = dto.QuantiteUnitaire,
                NombrePieces = dto.NombrePieces,
                QuantiteTotale = dto.QuantiteUnitaire * dto.NombrePieces,
                Notes = dto.Notes,
                DateCreation = DateTime.Now
            };

            _context.BesoinsCommandes.Add(besoin);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCommandeClient", new { id = commande.Id }, besoin);
        }

        [HttpPost("{id}/ValiderRessources")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> ValiderRessources(int id)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Besoins)
                .ThenInclude(b => b.Article)
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (commande == null)
                return NotFound();

            var plateformeId = commande.Client?.PlateformeId;

            var groupeCommandeIds = await _context.GroupeCommandeCommandes
                .Where(gcc => gcc.CommandeClientId == commande.Id)
                .Select(gcc => gcc.GroupeCommandeId)
                .ToListAsync();

            decimal totalCouverture = 0;
            int besoinsTraites = 0;

            foreach (var besoin in commande.Besoins)
            {
                var s1 = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId &&
                               s.TypeStock == TypeStock.Importe &&
                               s.CommandeClientId == commande.Id &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var s2 = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId &&
                               s.TypeStock == TypeStock.Importe &&
                               s.ClientId == commande.ClientId &&
                               s.CommandeClientId == null &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var s3 = plateformeId.HasValue
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId &&
                                   s.TypeStock == TypeStock.Importe &&
                                   s.PlateformeId == plateformeId &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var s4 = groupeCommandeIds.Any()
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId &&
                                   s.TypeStock == TypeStock.Importe &&
                                   s.GroupeCommandeId.HasValue &&
                                   groupeCommandeIds.Contains(s.GroupeCommandeId.Value) &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var r1 = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId &&
                               s.TypeStock == TypeStock.Reserve &&
                               s.CommandeClientId == commande.Id &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var r2 = await _context.Stocks
                    .Where(s => s.ArticleId == besoin.ArticleId &&
                               s.TypeStock == TypeStock.Reserve &&
                               s.ClientId == commande.ClientId &&
                               s.CommandeClientId == null &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var r3 = plateformeId.HasValue
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId &&
                                   s.TypeStock == TypeStock.Reserve &&
                                   s.PlateformeId == plateformeId &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var r4 = groupeCommandeIds.Any()
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId &&
                                   s.TypeStock == TypeStock.Reserve &&
                                   s.GroupeCommandeId.HasValue &&
                                   groupeCommandeIds.Contains(s.GroupeCommandeId.Value) &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var stockImporte = s1 + s2 + s3 + s4;
                besoin.QuantiteStockImporte = Math.Min(stockImporte, besoin.QuantiteTotale);
                var stockAchatsLocaux = r1 + r2 + r3 + r4;
                besoin.QuantiteAchatsLocaux = Math.Min(stockAchatsLocaux, besoin.QuantiteTotale - besoin.QuantiteStockImporte);

                var quantiteRestante = besoin.QuantiteTotale - besoin.QuantiteStockImporte - besoin.QuantiteAchatsLocaux;

                if (quantiteRestante > 0)
                {
                    var s5 = await _context.Stocks
                        .Where(s => s.ArticleId == besoin.ArticleId &&
                                   s.TypeStock == TypeStock.Libre &&
                                   s.Quantite > s.QuantiteReservee)
                        .SumAsync(s => s.Quantite - s.QuantiteReservee);

                    besoin.QuantiteStockLibre = Math.Min(s5, quantiteRestante);
                }

                besoin.QuantiteCouverte = besoin.QuantiteStockImporte + besoin.QuantiteAchatsLocaux + besoin.QuantiteStockLibre;
                besoin.EstCompletementCouvert = besoin.QuantiteCouverte >= besoin.QuantiteTotale;

                if (besoin.EstCompletementCouvert && (s1 + r1) < besoin.QuantiteTotale)
                {
                    var aReclamer = besoin.QuantiteTotale - s1 - r1;
                    await ScinderStock(besoin.ArticleId, aReclamer, s2, s3, s4, r2, r3, r4,
                        commande.Id, commande.ClientId, plateformeId, groupeCommandeIds);
                }

                totalCouverture += (besoin.QuantiteCouverte / besoin.QuantiteTotale) * 100;
                besoinsTraites++;
            }

            commande.PourcentageRessourcesCouvertes = besoinsTraites > 0 ? totalCouverture / besoinsTraites : 0;

            if (commande.PourcentageRessourcesCouvertes >= 100)
                commande.Statut = StatutCommande.Prete;
            else
                commande.Statut = StatutCommande.EnAttente;

            commande.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Validation des ressources terminée",
                pourcentageCouverture = commande.PourcentageRessourcesCouvertes,
                statut = commande.Statut.ToString()
            });
        }

        /// <summary>
        /// Ordre de consommation (déterministe) :
        /// 1. Importe exclusif commande (s1) — déjà compté, pas ici
        /// 2. Reserve exclusif commande (r1) — déjà compté, pas ici
        /// 3. Importe partagé : Client (s2) → Plateforme (s3) → Groupe (s4)
        /// 4. Reserve partagé : Client (r2) → Plateforme (r3) → Groupe (r4)
        /// 5. Libre non réservé
        /// </summary>
        private async Task ScinderStock(int articleId, decimal aReclamer,
            decimal s2, decimal s3, decimal s4, decimal r2, decimal r3, decimal r4,
            int commandeClientId, int? clientClientId, int? plateformeId,
            List<int> groupeCommandeIds)
        {
            var restant = aReclamer;

            if (restant > 0 && s2 > 0)
            {
                var prendre = Math.Min(restant, s2);
                await ScinderDepuisScope(articleId, TypeStock.Importe, prendre, commandeClientId,
                    clientClientId, null, null);
                restant -= prendre;
            }

            if (restant > 0 && s3 > 0 && plateformeId.HasValue)
            {
                var prendre = Math.Min(restant, s3);
                await ScinderDepuisScope(articleId, TypeStock.Importe, prendre, commandeClientId,
                    null, plateformeId, null);
                restant -= prendre;
            }

            if (restant > 0 && s4 > 0 && groupeCommandeIds.Any())
            {
                foreach (var gid in groupeCommandeIds)
                {
                    if (restant <= 0) break;
                    var disponible = await _context.Stocks
                        .Where(s => s.ArticleId == articleId &&
                                   s.TypeStock == TypeStock.Importe &&
                                   s.GroupeCommandeId == gid &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite);
                    if (disponible > 0)
                    {
                        var prendre = Math.Min(restant, disponible);
                        await ScinderDepuisScope(articleId, TypeStock.Importe, prendre, commandeClientId,
                            null, null, gid);
                        restant -= prendre;
                    }
                }
            }

            if (restant > 0 && r2 > 0)
            {
                var prendre = Math.Min(restant, r2);
                await ScinderDepuisScope(articleId, TypeStock.Reserve, prendre, commandeClientId,
                    clientClientId, null, null);
                restant -= prendre;
            }

            if (restant > 0 && r3 > 0 && plateformeId.HasValue)
            {
                var prendre = Math.Min(restant, r3);
                await ScinderDepuisScope(articleId, TypeStock.Reserve, prendre, commandeClientId,
                    null, plateformeId, null);
                restant -= prendre;
            }

            if (restant > 0 && r4 > 0 && groupeCommandeIds.Any())
            {
                foreach (var gid in groupeCommandeIds)
                {
                    if (restant <= 0) break;
                    var disponible = await _context.Stocks
                        .Where(s => s.ArticleId == articleId &&
                                   s.TypeStock == TypeStock.Reserve &&
                                   s.GroupeCommandeId == gid &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite);
                    if (disponible > 0)
                    {
                        var prendre = Math.Min(restant, disponible);
                        await ScinderDepuisScope(articleId, TypeStock.Reserve, prendre, commandeClientId,
                            null, null, gid);
                        restant -= prendre;
                    }
                }
            }

            if (restant > 0)
            {
                var libre = await _context.Stocks
                    .Where(s => s.ArticleId == articleId &&
                               s.TypeStock == TypeStock.Libre &&
                               s.Quantite > s.QuantiteReservee)
                    .SumAsync(s => s.Quantite - s.QuantiteReservee);
                if (libre > 0)
                {
                    var prendre = Math.Min(restant, libre);
                    await ScinderDepuisScope(articleId, TypeStock.Libre, prendre, commandeClientId,
                        null, null, null);
                }
            }
        }

        /// <summary>
        /// LOT 2.3 : Scission physique — retire depuis un scope partagé et crée un Stock
        /// exclusif (CommandeClientId renseigné). Préserve le scope partagé d'origine
        /// (ClientId/PlateformeId/GroupeCommandeId) pour permettre la libération (LOT 3).
        /// Supprime la ligne source si entièrement consommée. Crée un MouvementStock
        /// de type Transfert pour chaque ligne scindée.
        /// </summary>
        private async Task ScinderDepuisScope(int articleId, TypeStock typeStock, decimal quantite,
            int commandeClientId, int? clientId, int? plateformeId, int? groupeCommandeId)
        {
            var lignes = await _context.Stocks
                .Where(s => s.ArticleId == articleId &&
                           s.TypeStock == typeStock &&
                           s.Quantite > 0 &&
                           s.CommandeClientId == null &&
                           (clientId.HasValue ? s.ClientId == clientId.Value : s.ClientId == null) &&
                           (plateformeId.HasValue ? s.PlateformeId == plateformeId.Value : s.PlateformeId == null) &&
                           (groupeCommandeId.HasValue ? s.GroupeCommandeId == groupeCommandeId.Value : s.GroupeCommandeId == null))
                .ToListAsync();

            var restant = quantite;
            foreach (var ligne in lignes)
            {
                if (restant <= 0) break;
                var prendre = Math.Min(restant, ligne.Quantite);
                var sourceId = ligne.Id;

                if (prendre == ligne.Quantite)
                {
                    // LOT 2.3 : supprimer la ligne source plutôt que laisser à 0
                    _context.Stocks.Remove(ligne);
                }
                else
                {
                    ligne.Quantite -= prendre;
                }

                // LOT 2.3 : conserver le scope d'origine pour permettre la libération (LOT 3)
                _context.Stocks.Add(new Stock
                {
                    ArticleId = articleId,
                    TypeStock = typeStock,
                    Quantite = prendre,
                    CommandeClientId = commandeClientId,
                    ClientId = ligne.ClientId,
                    PlateformeId = ligne.PlateformeId,
                    GroupeCommandeId = ligne.GroupeCommandeId,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = ligne.DateEntree,
                    EstValide = ligne.EstValide,
                    ValidePar = ligne.ValidePar
                });

                // LOT 2.3 : MouvementStock Transfert pour traçabilité
                _context.MouvementsStock.Add(new MouvementStock
                {
                    StockId = sourceId,
                    TypeMouvement = TypeMouvement.Transfert,
                    Quantite = prendre,
                    DateMouvement = DateTime.Now,
                    Notes = $"Scission physique scope partagé → exclusif commande #{commandeClientId}"
                });

                restant -= prendre;
            }
        }

        [HttpPost("{id}/GenererTaches")]
        [RequireModulePermission("commandes", requireWrite: true)]
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

        [HttpPost("{id}/Tailles")]
        [RequireModulePermission("commandes", requireWrite: true)]
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

        [HttpPost("{id}/Bom")]
        [RequireModulePermission("commandes", requireWrite: true)]
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

        [HttpPost("{id}/Calculer")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> Calculer(int id, [FromBody] CalculerRequest request)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (commande == null)
                return NotFound();

            var bomPlateformeId = commande.Client?.PlateformeId;

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

            var groupeCommandeIds = await _context.GroupeCommandeCommandes
                .Where(gcc => gcc.CommandeClientId == commande.Id)
                .Select(gcc => gcc.GroupeCommandeId)
                .ToListAsync();

            foreach (var ligne in bomLignes)
            {
                var besoinBrut = ligne.QuantiteParPiece * totalPieces;
                var besoinFinal = besoinBrut * (1 + marge / 100);

                var r1 = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId &&
                               s.TypeStock == TypeStock.Reserve &&
                               s.CommandeClientId == id &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var r2 = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId &&
                               s.TypeStock == TypeStock.Reserve &&
                               s.ClientId == commande.ClientId &&
                               s.CommandeClientId == null &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var r3 = bomPlateformeId.HasValue
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == ligne.ArticleId &&
                                   s.TypeStock == TypeStock.Reserve &&
                                   s.PlateformeId == bomPlateformeId &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var r4 = groupeCommandeIds.Any()
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == ligne.ArticleId &&
                                   s.TypeStock == TypeStock.Reserve &&
                                   s.GroupeCommandeId.HasValue &&
                                   groupeCommandeIds.Contains(s.GroupeCommandeId.Value) &&
                                   s.ClientId == null &&
                                   s.CommandeClientId == null &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var qteStockReserve = r1 + r2 + r3 + r4;

                var b1 = await _context.LignesAchat
                    .Include(la => la.Achat)
                    .Where(la => la.ArticleId == ligne.ArticleId &&
                                la.TypeDestination == TypeDestinationAchat.Commande &&
                                la.CommandeClientId == id &&
                                la.Achat.Statut == StatutAchat.Confirme)
                    .SumAsync(la => la.Quantite - la.QuantiteRecue);

                var b2 = await _context.LignesAchat
                    .Include(la => la.Achat)
                    .Where(la => la.ArticleId == ligne.ArticleId &&
                                la.TypeDestination == TypeDestinationAchat.Marque &&
                                la.ClientId == commande.ClientId &&
                                la.Achat.Statut == StatutAchat.Confirme)
                    .SumAsync(la => la.Quantite - la.QuantiteRecue);

                var b3 = bomPlateformeId.HasValue
                    ? await _context.LignesAchat
                        .Include(la => la.Achat)
                        .Where(la => la.ArticleId == ligne.ArticleId &&
                                    la.TypeDestination == TypeDestinationAchat.Plateforme &&
                                    la.PlateformeId == bomPlateformeId &&
                                    la.Achat.Statut == StatutAchat.Confirme)
                        .SumAsync(la => la.Quantite - la.QuantiteRecue)
                    : 0;

                var qteAchat = b1 + b2 + b3;

                var si1 = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId &&
                               s.TypeStock == TypeStock.Importe &&
                               s.CommandeClientId == id &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var si2 = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId &&
                               s.TypeStock == TypeStock.Importe &&
                               s.ClientId == commande.ClientId &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var si3 = bomPlateformeId.HasValue
                    ? await _context.Stocks
                        .Where(s => s.ArticleId == ligne.ArticleId &&
                                   s.TypeStock == TypeStock.Importe &&
                                   s.PlateformeId == bomPlateformeId &&
                                   s.Quantite > 0)
                        .SumAsync(s => s.Quantite)
                    : 0;

                var si4 = await _context.Stocks
                    .Where(s => s.ArticleId == ligne.ArticleId &&
                               s.TypeStock == TypeStock.Importe &&
                               s.CommandeClientId == null &&
                               s.ClientId == null &&
                               s.PlateformeId == null &&
                               s.Quantite > 0)
                    .SumAsync(s => s.Quantite);

                var qteImport = si1 + si2 + si3 + si4;

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

        // Convertit la BOM (nomenclature par pièce) + les tailles configurées en besoins
        // "officiels" (BesoinCommande) : QuantiteUnitaire = QuantiteParPiece, NombrePieces = total
        // des tailles. Idempotent : les besoins déjà générés (marqués via Notes) sont mis à jour
        // plutôt que dupliqués ; les besoins saisis manuellement (sans le marqueur) ne sont jamais
        // touchés. Un besoin généré dont l'article a été retiré de la BOM est supprimé.
        private const string TagGenereDepuisBom = "[Généré depuis BOM]";

        [HttpPost("{id}/GenererBesoinsDepuisBom")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> GenererBesoinsDepuisBom(int id)
        {
            if (!CommandeClientExists(id))
                return NotFound();

            var totalPieces = await _context.ConfigTailles
                .Where(ct => ct.CommandeId == id)
                .SumAsync(ct => (decimal)ct.Quantite);

            if (totalPieces <= 0)
                return BadRequest("Aucune configuration de tailles définie pour cette commande.");

            var bomLignes = await _context.BomLignes.Where(b => b.CommandeId == id).ToListAsync();
            if (!bomLignes.Any())
                return BadRequest("Aucune ligne BOM définie pour cette commande.");

            var nombrePieces = (int)Math.Round(totalPieces, MidpointRounding.AwayFromZero);

            var besoinsGeneresExistants = await _context.BesoinsCommandes
                .Where(b => b.CommandeClientId == id && b.Notes != null && b.Notes.Contains(TagGenereDepuisBom))
                .ToListAsync();

            int crees = 0, misAJour = 0;

            foreach (var ligne in bomLignes)
            {
                var existant = besoinsGeneresExistants.FirstOrDefault(b => b.ArticleId == ligne.ArticleId);
                if (existant != null)
                {
                    existant.QuantiteUnitaire = ligne.QuantiteParPiece;
                    existant.NombrePieces = nombrePieces;
                    existant.QuantiteTotale = ligne.QuantiteParPiece * nombrePieces;
                    misAJour++;
                }
                else
                {
                    _context.BesoinsCommandes.Add(new BesoinCommande
                    {
                        CommandeClientId = id,
                        ArticleId = ligne.ArticleId,
                        TypeBesoin = TypeBesoin.MatierePremiere,
                        QuantiteUnitaire = ligne.QuantiteParPiece,
                        NombrePieces = nombrePieces,
                        QuantiteTotale = ligne.QuantiteParPiece * nombrePieces,
                        Notes = TagGenereDepuisBom,
                        DateCreation = DateTime.Now
                    });
                    crees++;
                }
            }

            // Nettoie les besoins générés dont l'article n'est plus dans la BOM courante.
            var articleIdsBom = bomLignes.Select(b => b.ArticleId).ToHashSet();
            var obsoletes = besoinsGeneresExistants.Where(b => !articleIdsBom.Contains(b.ArticleId)).ToList();
            _context.BesoinsCommandes.RemoveRange(obsoletes);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Besoins générés depuis la BOM",
                crees,
                misAJour,
                supprimes = obsoletes.Count
            });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<IActionResult> PutCommandeClient(int id, UpdateCommandeClientDto dto)
        {
            var commande = await _context.CommandesClients.FindAsync(id);
            if (commande == null)
            {
                return NotFound();
            }

            if (commande.Statut > StatutCommande.Prete)
            {
                return BadRequest(new { message = "La commande ne peut plus être modifiée après le statut Prête." });
            }

            commande.TitreCommande = dto.TitreCommande;
            commande.DateLivraisonSouhaitee = dto.DateLivraisonSouhaitee;
            commande.NotesSpeciales = dto.NotesSpeciales;
            commande.DateMiseAJour = DateTime.Now;

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

        [HttpDelete("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
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

        private bool CommandeClientExists(int id)
        {
            return _context.CommandesClients.Any(e => e.Id == id);
        }

        private string GenerateNumeroCommande()
        {
            var today = DateTime.Now;
            var prefix = $"CMD{today:yyyyMM}";
            var numeros = _context.CommandesClients
                .Where(c => c.NumeroCommande.StartsWith(prefix))
                .Select(c => c.NumeroCommande)
                .ToList();
            var maxSuffix = numeros
                .Where(n => n.Length > prefix.Length)
                .Select(n => int.TryParse(n.Substring(prefix.Length), out var suffix) ? suffix : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{(maxSuffix + 1):D4}";
        }

        private static bool EstConflitNumeroCommande(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pg
                && pg.SqlState == "23505"
                && string.Equals(pg.ConstraintName, "IX_CommandesClients_NumeroCommande", StringComparison.Ordinal);
        }
    }

    public record ConfigTailleDto(string Taille, int Quantite);
    public record BomLigneDto(int ArticleId, decimal QuantiteParPiece, string? Unite);
    public record CalculerRequest(decimal MargeAppliquee);
}
