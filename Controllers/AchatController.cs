using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Achat;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AchatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AchatController> _logger;

        public AchatController(ApplicationDbContext context, ILogger<AchatController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [RequireModulePermission("achats", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<AchatListDto>>> GetAchats()
        {
            return await _context.Achats
                .Select(a => new AchatListDto
                {
                    Id = a.Id,
                    NumeroAchat = a.NumeroAchat,
                    DateAchat = a.DateAchat,
                    DateLivraisonPrevue = a.DateLivraisonPrevue,
                    Statut = a.Statut,
                    MontantTotal = a.MontantTotal,
                    Devise = a.Devise,
                    FournisseurId = a.FournisseurId,
                    CommandeClientId = a.CommandeClientId,
                    CreePar = a.CreePar,
                    Fournisseur = a.Fournisseur != null ? new AchatFournisseurDto
                    {
                        Id = a.Fournisseur.Id,
                        NomEntreprise = a.Fournisseur.NomEntreprise
                    } : null,
                    CommandeClient = a.CommandeClient != null ? new AchatCommandeClientDto
                    {
                        Id = a.CommandeClient.Id,
                        NumeroCommande = a.CommandeClient.NumeroCommande,
                        TitreCommande = a.CommandeClient.TitreCommande,
                        Client = a.CommandeClient.Client != null ? new AchatClientDto
                        {
                            Id = a.CommandeClient.Client.Id,
                            Nom = a.CommandeClient.Client.Nom,
                            Plateforme = a.CommandeClient.Client.Plateforme != null ? new AchatPlateformeDto
                            {
                                Id = a.CommandeClient.Client.Plateforme.Id,
                                Nom = a.CommandeClient.Client.Plateforme.Nom
                            } : null
                        } : null
                    } : null,
                    LignesAchat = a.LignesAchat.Select(l => new LigneAchatDto
                    {
                        Id = l.Id,
                        ArticleId = l.ArticleId,
                        Quantite = l.Quantite,
                        QuantiteRecue = l.QuantiteRecue,
                        StatutLigne = l.StatutLigne,
                        PrixUnitaire = l.PrixUnitaire,
                        MontantLigne = l.MontantLigne,
                        Devise = l.Devise,
                        Unite = l.Unite,
                        Couleur = l.Couleur,
                        CodeCouleur = l.CodeCouleur,
                        Taille = l.Taille,
                        Dimension = l.Dimension,
                        DescriptionSpecifique = l.DescriptionSpecifique,
                        Notes = l.Notes,
                        TypeDestination = l.TypeDestination,
                        CommandeClientId = l.CommandeClientId,
                        ClientId = l.ClientId,
                        PlateformeId = l.PlateformeId,
                        GroupeCommandeId = l.GroupeCommandeId,
                        EstAffecteStock = false,
                        Article = l.Article != null ? new LigneAchatArticleDto
                        {
                            Id = l.Article.Id,
                            Designation = l.Article.Designation,
                            Reference = l.Article.Reference
                        } : null
                    }).ToList()
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("achats", requireWrite: false)]
        public async Task<ActionResult<AchatDetailDto>> GetAchat(int id)
        {
            var dto = await _context.Achats
                .Where(a => a.Id == id)
                .Select(a => new AchatDetailDto
                {
                    Id = a.Id,
                    NumeroAchat = a.NumeroAchat,
                    DateAchat = a.DateAchat,
                    DateLivraisonPrevue = a.DateLivraisonPrevue,
                    DateLivraisonReelle = a.DateLivraisonReelle,
                    Statut = a.Statut,
                    MontantTotal = a.MontantTotal,
                    Devise = a.Devise,
                    ConditionsPaiement = a.ConditionsPaiement,
                    NotesAchat = a.NotesAchat,
                    CreePar = a.CreePar,
                    FournisseurId = a.FournisseurId,
                    CommandeClientId = a.CommandeClientId,
                    Fournisseur = a.Fournisseur != null ? new AchatFournisseurDto
                    {
                        Id = a.Fournisseur.Id,
                        NomEntreprise = a.Fournisseur.NomEntreprise
                    } : null,
                    CommandeClient = a.CommandeClient != null ? new AchatCommandeClientDto
                    {
                        Id = a.CommandeClient.Id,
                        NumeroCommande = a.CommandeClient.NumeroCommande,
                        TitreCommande = a.CommandeClient.TitreCommande,
                        Client = a.CommandeClient.Client != null ? new AchatClientDto
                        {
                            Id = a.CommandeClient.Client.Id,
                            Nom = a.CommandeClient.Client.Nom,
                            Plateforme = a.CommandeClient.Client.Plateforme != null ? new AchatPlateformeDto
                            {
                                Id = a.CommandeClient.Client.Plateforme.Id,
                                Nom = a.CommandeClient.Client.Plateforme.Nom
                            } : null
                        } : null
                    } : null,
                    LignesAchat = a.LignesAchat.Select(l => new LigneAchatDto
                    {
                        Id = l.Id,
                        ArticleId = l.ArticleId,
                        Quantite = l.Quantite,
                        QuantiteRecue = l.QuantiteRecue,
                        StatutLigne = l.StatutLigne,
                        PrixUnitaire = l.PrixUnitaire,
                        MontantLigne = l.MontantLigne,
                        Devise = l.Devise,
                        Unite = l.Unite,
                        Couleur = l.Couleur,
                        CodeCouleur = l.CodeCouleur,
                        Taille = l.Taille,
                        Dimension = l.Dimension,
                        DescriptionSpecifique = l.DescriptionSpecifique,
                        Notes = l.Notes,
                        TypeDestination = l.TypeDestination,
                        CommandeClientId = l.CommandeClientId,
                        ClientId = l.ClientId,
                        PlateformeId = l.PlateformeId,
                        GroupeCommandeId = l.GroupeCommandeId,
                        EstAffecteStock = false,
                        Article = l.Article != null ? new LigneAchatArticleDto
                        {
                            Id = l.Article.Id,
                            Designation = l.Article.Designation,
                            Reference = l.Article.Reference
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
        public async Task<ActionResult<Achat>> PostAchat(CreateAchatDto dto)
        {
            if (!await FournisseurExiste(dto.FournisseurId))
            {
                return BadRequest("Fournisseur introuvable");
            }

            var achat = new Achat
            {
                FournisseurId = dto.FournisseurId,
                CommandeClientId = dto.CommandeClientId,
                DateLivraisonPrevue = dto.DateLivraisonPrevue,
                Devise = dto.Devise,
                ConditionsPaiement = dto.ConditionsPaiement,
                NotesAchat = dto.NotesAchat,
                CreePar = dto.CreePar,
                DateCreation = DateTime.Now,
                NumeroAchat = GenerateNumeroAchat()
            };

            _context.Achats.Add(achat);

            const int maxTentatives = 5;
            for (var tentative = 1; ; tentative++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (EstConflitNumeroAchat(ex) && tentative < maxTentatives)
                {
                    achat.NumeroAchat = GenerateNumeroAchat();
                }
            }

            return CreatedAtAction("GetAchat", new { id = achat.Id }, achat);
        }

        [HttpPost("{id}/LignesAchat")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<ActionResult<LigneAchat>> AjouterLigneAchat(int id, CreateLigneAchatDto dto)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            var ligneAchat = new LigneAchat
            {
                AchatId = id,
                ArticleId = dto.ArticleId,
                Quantite = dto.Quantite,
                PrixUnitaire = dto.PrixUnitaire,
                MontantLigne = dto.Quantite * dto.PrixUnitaire,
                TypeDestination = dto.TypeDestination,
                CommandeClientId = dto.CommandeClientId,
                ClientId = dto.ClientId,
                PlateformeId = dto.PlateformeId,
                GroupeCommandeId = dto.GroupeCommandeId,
                Couleur = dto.Couleur,
                CodeCouleur = dto.CodeCouleur,
                Taille = dto.Taille,
                Dimension = dto.Dimension,
                Devise = dto.Devise,
                Unite = dto.Unite,
                DescriptionSpecifique = dto.DescriptionSpecifique,
                Notes = dto.Notes,
                DateCreation = DateTime.Now
            };

            if (ligneAchat.TypeDestination == TypeDestinationAchat.GroupeCommandes
                && ligneAchat.GroupeCommandeId == null
                && dto.CommandeClientIds != null)
            {
                var groupeId = await ResoudreOuCreerGroupe(dto.CommandeClientIds);
                if (groupeId == null)
                    return BadRequest("TypeDestination=GroupeCommandes requiert au moins 2 commandes valides.");
                ligneAchat.GroupeCommandeId = groupeId;
            }

            switch (ligneAchat.TypeDestination)
            {
                case TypeDestinationAchat.Commande when !ligneAchat.CommandeClientId.HasValue:
                    return BadRequest("TypeDestination=Commande requiert un CommandeClientId.");
                case TypeDestinationAchat.Marque when !ligneAchat.ClientId.HasValue:
                    return BadRequest("TypeDestination=Marque requiert un ClientId.");
                case TypeDestinationAchat.Plateforme when !ligneAchat.PlateformeId.HasValue:
                    return BadRequest("TypeDestination=Plateforme requiert un PlateformeId.");
                case TypeDestinationAchat.GroupeCommandes when !ligneAchat.GroupeCommandeId.HasValue:
                    return BadRequest("TypeDestination=GroupeCommandes requiert un GroupeCommandeId.");
                case TypeDestinationAchat.StockLibre:
                    ligneAchat.CommandeClientId = null;
                    ligneAchat.ClientId = null;
                    ligneAchat.PlateformeId = null;
                    ligneAchat.GroupeCommandeId = null;
                    break;
            }

            _context.LignesAchat.Add(ligneAchat);

            await RecalculerMontantAchat(id);

            await _context.SaveChangesAsync();

            // Prix de référence + historique (Fonctionnalité 12) : best effort, ne doit
            // JAMAIS faire échouer la création de la ligne — erreurs loguées et avalées.
            await PrixHistoriqueService.EnregistrerPrixAsync(
                _context,
                ligneAchat.ArticleId,
                ligneAchat.PrixUnitaire,
                ligneAchat.Devise,
                SourcePrix.LigneAchat,
                ligneAchatId: ligneAchat.Id,
                logger: _logger);

            return CreatedAtAction("GetAchat", new { id = achat.Id }, ligneAchat);
        }

        [HttpPost("{id}/LignesAchat/{ligneId}/RecevoirPartiel")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<IActionResult> RecevoirPartiel(int id, int ligneId, RecevoirPartielDto dto)
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
                return BadRequest("Seuls les achats confirmés peuvent enregistrer une réception partielle");
            }

            var ligne = achat.LignesAchat.FirstOrDefault(l => l.Id == ligneId);
            if (ligne == null)
            {
                return NotFound();
            }

            if (ligne.StatutLigne != StatutLigneAchat.EnAttente && ligne.StatutLigne != StatutLigneAchat.PartielleEnCours)
            {
                return BadRequest("Seules les lignes en attente ou partiellement reçues peuvent être reçues partiellement");
            }

            if (dto.Quantite <= 0)
            {
                return BadRequest("La quantité reçue doit être supérieure à 0");
            }

            // Créer le stock pour cette réception partielle
            var stock = new Stock
            {
                ArticleId = ligne.ArticleId,
                Couleur = ligne.Couleur,
                CodeCouleur = ligne.CodeCouleur,
                Taille = ligne.Taille,
                Dimension = ligne.Dimension,
                Quantite = dto.Quantite,
                TypeStock = TypeStock.Reserve,
                    CommandeClientId = ligne.TypeDestination == TypeDestinationAchat.Commande ? ligne.CommandeClientId : null,
                    ClientId = ligne.TypeDestination == TypeDestinationAchat.Marque ? ligne.ClientId : null,
                    PlateformeId = ligne.TypeDestination == TypeDestinationAchat.Plateforme ? ligne.PlateformeId : null,
                    GroupeCommandeId = ligne.TypeDestination == TypeDestinationAchat.GroupeCommandes ? ligne.GroupeCommandeId : null,
                    PrixUnitaire = ligne.PrixUnitaire,
                    Devise = ligne.Devise,
                    DateEntree = DateTime.Now,
                    EstValide = true,
                    ValidePar = "Système - Réception Partielle Achat"
            };

            _context.Stocks.Add(stock);

            var mouvement = new MouvementStock
            {
                Stock = stock,
                TypeMouvement = TypeMouvement.Entree,
                OrigineMouvement = OrigineMouvement.Achat,
                Quantite = dto.Quantite,
                QuantiteAvant = 0,
                QuantiteApres = dto.Quantite,
                Motif = $"Réception partielle achat {achat.NumeroAchat} - ligne {ligne.Id}",
                DocumentReference = achat.NumeroAchat,
                DateMouvement = DateTime.Now,
                EffectuePar = "Système"
            };

            _context.MouvementsStock.Add(mouvement);

            // Mettre à jour la ligne
            ligne.QuantiteRecue += dto.Quantite;
            if (ligne.QuantiteRecue >= ligne.Quantite)
            {
                ligne.StatutLigne = StatutLigneAchat.Complete;
            }
            else
            {
                ligne.StatutLigne = StatutLigneAchat.PartielleEnCours;
            }

            // Montant reflétant la quantité réellement reçue (sur-réception incluse)
            ligne.MontantLigne = ligne.QuantiteRecue * ligne.PrixUnitaire;

            await RecalculerMontantAchat(id);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Réception partielle enregistrée avec succès", ligneId = ligne.Id });
        }

        [HttpPost("{id}/ClotureForcee")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<IActionResult> ClotureForcee(int id, ClotureForceeDto dto)
        {
            var achat = await _context.Achats
                .Include(a => a.LignesAchat)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Confirme)
            {
                return BadRequest("Seuls les achats confirmés peuvent être clôturés");
            }

            foreach (var ligne in achat.LignesAchat)
            {
                if (ligne.StatutLigne != StatutLigneAchat.Complete)
                {
                    ligne.StatutLigne = StatutLigneAchat.ClotureeForcee;
                }
            }

            achat.Statut = StatutAchat.Livre;
            achat.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Achat clôturé avec succès" });
        }

        [HttpPut("{id}/LignesAchat/{ligneId}")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<IActionResult> ModifierLigneAchat(int id, int ligneId, CreateLigneAchatDto dto)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Brouillon)
            {
                return BadRequest("Seuls les achats en Brouillon peuvent être modifiés");
            }

            var ligneAchat = await _context.LignesAchat
                .FirstOrDefaultAsync(l => l.Id == ligneId && l.AchatId == id);
            if (ligneAchat == null)
            {
                return NotFound();
            }

            var commandeClientId = dto.CommandeClientId;
            var clientId = dto.ClientId;
            var plateformeId = dto.PlateformeId;

            switch (dto.TypeDestination)
            {
                case TypeDestinationAchat.Commande when !commandeClientId.HasValue:
                    return BadRequest("TypeDestination=Commande requiert un CommandeClientId.");
                case TypeDestinationAchat.Marque when !clientId.HasValue:
                    return BadRequest("TypeDestination=Marque requiert un ClientId.");
                case TypeDestinationAchat.Plateforme when !plateformeId.HasValue:
                    return BadRequest("TypeDestination=Plateforme requiert un PlateformeId.");
                case TypeDestinationAchat.GroupeCommandes when !dto.GroupeCommandeId.HasValue:
                    return BadRequest("TypeDestination=GroupeCommandes requiert un GroupeCommandeId.");
                case TypeDestinationAchat.StockLibre:
                    commandeClientId = null;
                    clientId = null;
                    plateformeId = null;
                    break;
            }

            ligneAchat.ArticleId = dto.ArticleId;
            ligneAchat.TypeDestination = dto.TypeDestination;
            ligneAchat.CommandeClientId = commandeClientId;
            ligneAchat.ClientId = clientId;
            ligneAchat.PlateformeId = plateformeId;
            ligneAchat.GroupeCommandeId = dto.GroupeCommandeId;
            ligneAchat.Couleur = dto.Couleur;
            ligneAchat.CodeCouleur = dto.CodeCouleur;
            ligneAchat.Taille = dto.Taille;
            ligneAchat.Dimension = dto.Dimension;
            ligneAchat.Quantite = dto.Quantite;
            ligneAchat.PrixUnitaire = dto.PrixUnitaire;
            ligneAchat.MontantLigne = dto.Quantite * dto.PrixUnitaire;
            ligneAchat.Devise = dto.Devise;
            ligneAchat.Unite = dto.Unite;
            ligneAchat.DescriptionSpecifique = dto.DescriptionSpecifique;
            ligneAchat.Notes = dto.Notes;

            if (ligneAchat.TypeDestination == TypeDestinationAchat.GroupeCommandes
                && ligneAchat.GroupeCommandeId == null
                && dto.CommandeClientIds != null)
            {
                var groupeId = await ResoudreOuCreerGroupe(dto.CommandeClientIds);
                if (groupeId == null)
                    return BadRequest("TypeDestination=GroupeCommandes requiert au moins 2 commandes valides.");
                ligneAchat.GroupeCommandeId = groupeId;
            }

            await RecalculerMontantAchat(id);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}/LignesAchat/{ligneId}")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<IActionResult> SupprimerLigneAchat(int id, int ligneId)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            if (achat.Statut != StatutAchat.Brouillon)
            {
                return BadRequest("Seules les lignes d'achats en Brouillon peuvent être supprimées");
            }

            var ligneAchat = await _context.LignesAchat
                .FirstOrDefaultAsync(l => l.Id == ligneId && l.AchatId == id);
            if (ligneAchat == null)
            {
                return NotFound();
            }

            _context.LignesAchat.Remove(ligneAchat);

            await RecalculerMontantAchat(id);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/Soumettre")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<ActionResult> SoumettreAchat(int id, [FromBody] Dtos.Achat.SoumettreAchatDto? dto = null)
        {
            var forcerDepassement = dto?.ForcerDepassement == true;

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
            var depassements = new List<object>();

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
                        depassements.Add(new
                        {
                            ligneId = ligne.Id,
                            articleDesignation = ligne.Article?.Designation ?? "N/A",
                            quantiteCommandee = ligne.Quantite,
                            besoinTotal = besoin.QuantiteTotale,
                            exces = ligne.Quantite - besoin.QuantiteTotale
                        });
                    }
                }
            }

            if (erreurs.Any())
            {
                return BadRequest(new { message = "Erreurs de cohérence détectées", erreurs });
            }

            if (depassements.Any() && !forcerDepassement)
            {
                return StatusCode(409, new
                {
                    avertissement = true,
                    message = " certaines lignes dépassent le besoin de la commande",
                    depassements
                });
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
                var quantiteRestante = ligne.Quantite - ligne.QuantiteRecue;
                if (quantiteRestante <= 0)
                {
                    continue; // Déjà entièrement reçue, pas besoin de créer de stock
                }

                var stock = new Stock
                {
                    ArticleId = ligne.ArticleId,
                    Couleur = ligne.Couleur,
                    CodeCouleur = ligne.CodeCouleur,
                    Taille = ligne.Taille,
                    Dimension = ligne.Dimension,
                    Quantite = quantiteRestante,
                    TypeStock = TypeStock.Reserve,
                    CommandeClientId = ligne.TypeDestination == TypeDestinationAchat.Commande ? ligne.CommandeClientId : null,
                    ClientId = ligne.TypeDestination == TypeDestinationAchat.Marque ? ligne.ClientId : null,
                    PlateformeId = ligne.TypeDestination == TypeDestinationAchat.Plateforme ? ligne.PlateformeId : null,
                    GroupeCommandeId = ligne.TypeDestination == TypeDestinationAchat.GroupeCommandes ? ligne.GroupeCommandeId : null,
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
                    Quantite = quantiteRestante,
                    QuantiteAvant = 0,
                    QuantiteApres = quantiteRestante,
                    Motif = $"Réception achat {achat.NumeroAchat}",
                    DocumentReference = achat.NumeroAchat,
                    DateMouvement = DateTime.Now,
                    EffectuePar = "Système"
                };

                _context.MouvementsStock.Add(mouvement);

                // Mettre à jour la ligne
                ligne.QuantiteRecue = ligne.Quantite;
                if (ligne.QuantiteRecue >= ligne.Quantite)
                {
                    ligne.StatutLigne = StatutLigneAchat.Complete;
                }
                else
                {
                    ligne.StatutLigne = StatutLigneAchat.PartielleEnCours;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Achat livré et stock mis à jour avec succès" });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("achats", requireWrite: true)]
        public async Task<IActionResult> PutAchat(int id, UpdateAchatDto dto)
        {
            var achat = await _context.Achats.FindAsync(id);
            if (achat == null)
            {
                return NotFound();
            }

            if (!await FournisseurExiste(dto.FournisseurId))
            {
                return BadRequest("Fournisseur introuvable");
            }

            achat.FournisseurId = dto.FournisseurId;
            achat.CommandeClientId = dto.CommandeClientId;
            achat.DateLivraisonPrevue = dto.DateLivraisonPrevue;
            achat.Devise = dto.Devise;
            achat.ConditionsPaiement = dto.ConditionsPaiement;
            achat.NotesAchat = dto.NotesAchat;
            achat.DateMiseAJour = DateTime.Now;

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

        private async Task<bool> FournisseurExiste(int fournisseurId)
        {
            return await _context.Fournisseurs.AnyAsync(f => f.Id == fournisseurId);
        }

        private string GenerateNumeroAchat()
        {
            var today = DateTime.Now;
            var prefix = $"ACH{today:yyyyMM}";
            var numeros = _context.Achats
                .Where(a => a.NumeroAchat.StartsWith(prefix))
                .Select(a => a.NumeroAchat)
                .ToList();
            var maxSuffix = numeros
                .Where(n => n.Length > prefix.Length)
                .Select(n => int.TryParse(n.Substring(prefix.Length), out var suffix) ? suffix : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{(maxSuffix + 1):D4}";
        }

        private static bool EstConflitNumeroAchat(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pg
                && pg.SqlState == "23505"
                && string.Equals(pg.ConstraintName, "IX_Achats_NumeroAchat", StringComparison.Ordinal);
        }

        /// <summary>
        /// LOT 1.6 : Résout ou crée un GroupeCommande à partir d'une liste de CommandeClientId.
        /// Recherche un groupe existant avec exactement les mêmes membres (même nombre, mêmes IDs)
        /// avant de créer un nouveau. Retourne l'Id du groupe.
        /// </summary>
        private async Task<int?> ResoudreOuCreerGroupe(List<int>? commandeClientIds)
        {
            if (commandeClientIds == null || commandeClientIds.Count < 2)
                return null;

            var idsDistincts = commandeClientIds.Distinct().OrderBy(x => x).ToList();

            var existantes = await _context.CommandesClients
                .Where(cc => idsDistincts.Contains(cc.Id))
                .Select(cc => cc.Id)
                .ToListAsync();

            if (existantes.Count != idsDistincts.Count)
                return null;

            var groupesCandidats = await _context.GroupesCommandes
                .Include(gc => gc.Membres)
                .Where(gc => gc.Membres.Count == idsDistincts.Count)
                .ToListAsync();

            var groupe = groupesCandidats.FirstOrDefault(gc =>
                gc.Membres.Select(m => m.CommandeClientId).OrderBy(x => x)
                    .SequenceEqual(idsDistincts));

            if (groupe != null)
                return groupe.Id;

            groupe = new GroupeCommande { DateCreation = DateTime.UtcNow };
            _context.GroupesCommandes.Add(groupe);
            await _context.SaveChangesAsync();

            foreach (var cid in idsDistincts)
            {
                _context.GroupeCommandeCommandes.Add(new GroupeCommandeCommande
                {
                    GroupeCommandeId = groupe.Id,
                    CommandeClientId = cid
                });
            }

            await _context.SaveChangesAsync();
            return groupe.Id;
        }
    }
}
