using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Exécute les appels d'outils demandés par le modèle Groq.
    /// RÈGLE DE SÉCURITÉ : seules des opérations de lecture sont exposées ici.
    /// Aucune méthode d'écriture (Create/Update/Delete) n'est accessible.
    /// </summary>
    public class ToolExecutor
    {
        private readonly ApplicationDbContext  _context;
        private readonly IArticleService       _articles;
        private readonly CommandeService       _commandes;
        private readonly ImportationService    _importations;
        private readonly IPermissionService    _permissions;
        private readonly ILogger<ToolExecutor> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public ToolExecutor(
            ApplicationDbContext context,
            IArticleService articles,
            CommandeService commandes,
            ImportationService importations,
            IPermissionService permissions,
            ILogger<ToolExecutor> logger)
        {
            _context      = context;
            _articles     = articles;
            _commandes    = commandes;
            _importations = importations;
            _permissions  = permissions;
            _logger       = logger;
        }

        public async Task<string> ExecuteAsync(string toolName, JsonNode args, string? userId)
        {
            if (string.IsNullOrEmpty(userId))
                return Err("Accès refusé : authentification requise pour utiliser les outils de données.");

            var module = toolName switch
            {
                "get_articles"     => "articles",
                "get_stock"        => "stock",
                "get_commandes"    => "commandes",
                "get_achats"       => "achats",
                "get_importations" => "importations",
                "get_mouvements"   => "mouvements",
                _                  => null
            };

            // get_schema est une métadonnée statique (aucune donnée de la base) : pas de module requis.
            if (toolName == "get_schema")
                return GetSchema(args);

            if (module is null)
                return Err($"Outil inconnu : {toolName}");

            var (canAccess, _) = await _permissions.GetPermissionAsync(userId, module);
            if (!canAccess)
                return Err($"Accès refusé au module « {module} » : vous n'avez pas les droits nécessaires.");

            _logger.LogInformation("Outil : {Tool} | args : {Args} | user : {UserId}", toolName, args.ToJsonString(), userId);
            try
            {
                return toolName switch
                {
                    "get_articles"     => await GetArticles(args),
                    "get_stock"        => await GetStock(args),
                    "get_commandes"    => await GetCommandes(args),
                    "get_achats"       => await GetAchats(args),
                    "get_importations" => await GetImportations(args),
                    "get_mouvements"   => await GetMouvements(args),
                    "get_schema"       => GetSchema(args),
                    _                  => Err($"Outil inconnu : {toolName}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur exécution outil {Tool}", toolName);
                return Err(ex.Message);
            }
        }

        // ── Outils ───────────────────────────────────────────────────────────────

        private async Task<string> GetArticles(JsonNode args)
        {
            var term = args["recherche"]?.GetValue<string>();

            IEnumerable<Article> list = string.IsNullOrWhiteSpace(term)
                ? (await _articles.GetArticlesAsync(1, 30)).Data
                : await _articles.SearchArticlesAsync(term);

            var result = list.Select(a => new
            {
                a.Id,
                a.Designation,
                a.Reference,
                a.Categorie,
                a.SousCategorie,
                a.Unite,
                a.SeuilAlerte,
                a.SeuilCritique,
                a.EstActif,
                StockDisponible = a.Stocks?.Sum(s => s.Quantite - s.QuantiteReservee) ?? 0
            }).ToList();

            return Json(new { count = result.Count, articles = result });
        }

        private async Task<string> GetStock(JsonNode args)
        {
            var articleId = args["articleId"]?.GetValue<int?>();
            if (articleId is null)
                return Err("articleId est obligatoire pour get_stock.");

            var article = await _articles.GetArticleByIdAsync(articleId.Value);
            if (article is null)
                return Err($"Article id={articleId} introuvable.");

            var stockTotal = await _articles.GetStockTotalAsync(articleId.Value);

            var parType = article.Stocks?
                .GroupBy(s => s.TypeStock.ToString())
                .Select(g => new
                {
                    TypeStock        = g.Key,
                    Quantite         = g.Sum(s => s.Quantite),
                    QuantiteReservee = g.Sum(s => s.QuantiteReservee),
                    Disponible       = g.Sum(s => s.Quantite - s.QuantiteReservee)
                }).ToList();

            return Json(new
            {
                article   = new { article.Id, article.Designation, article.Reference, article.Unite },
                stockTotal,
                parType
            });
        }

        private async Task<string> GetCommandes(JsonNode args)
        {
            var statutStr      = args["statut"]?.GetValue<string>();
            var marqueNom      = args["marqueNom"]?.GetValue<string>();
            var clientNom      = args["clientNom"]?.GetValue<string>();
            var plateformeNom  = args["plateformeNom"]?.GetValue<string>();
            var dateDebutStr   = args["dateDebut"]?.GetValue<string>();
            var dateFinStr     = args["dateFin"]?.GetValue<string>();
            var marqueId       = args["marqueId"]?.GetValue<int?>();

            var all = await _commandes.GetAllCommandesAsync();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutCommande>(statutStr, ignoreCase: true, out var statut))
                filtered = filtered.Where(c => c.Statut == statut);

            if (marqueId.HasValue)
                filtered = filtered.Where(c => c.MarqueId == marqueId.Value);

            if (!string.IsNullOrWhiteSpace(marqueNom))
                filtered = filtered.Where(c =>
                    c.Marque != null && c.Marque.Nom.Contains(marqueNom, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(clientNom))
                filtered = filtered.Where(c =>
                    c.Client != null &&
                    (c.Client.Nom.Contains(clientNom, StringComparison.OrdinalIgnoreCase) ||
                     (c.Client.NomEntreprise != null &&
                      c.Client.NomEntreprise.Contains(clientNom, StringComparison.OrdinalIgnoreCase))));

            if (!string.IsNullOrWhiteSpace(plateformeNom))
                filtered = filtered.Where(c =>
                    (c.Client != null && c.Client.Plateforme != null &&
                     c.Client.Plateforme.Nom.Contains(plateformeNom, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Marque != null && c.Marque.Plateforme != null &&
                     c.Marque.Plateforme.Nom.Contains(plateformeNom, StringComparison.OrdinalIgnoreCase)));

            if (DateTimeOffset.TryParse(dateDebutStr, out var debut))
                filtered = filtered.Where(c => c.DateCreation >= debut.UtcDateTime);

            if (DateTimeOffset.TryParse(dateFinStr, out var fin))
                filtered = filtered.Where(c => c.DateCreation < fin.UtcDateTime.AddDays(1));

            var result = filtered
                .OrderByDescending(c => c.DateCreation)
                .Take(25)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    Client   = c.Client?.Nom,
                    Marque   = c.Marque?.Nom,
                    Plateforme = c.Client?.Plateforme?.Nom ?? c.Marque?.Plateforme?.Nom,
                    Statut   = c.Statut.ToString(),
                    c.DateCreation,
                    c.DateLivraisonSouhaitee,
                    c.MontantTotal,
                    c.PourcentageRessourcesCouvertes
                }).ToList();

            return Json(new { count = result.Count, commandes = result });
        }

        private async Task<string> GetAchats(JsonNode args)
        {
            var statutStr      = args["statut"]?.GetValue<string>();
            var fournisseurNom = args["fournisseurNom"]?.GetValue<string>();
            var plateformeNom  = args["plateformeNom"]?.GetValue<string>();
            var articleNom     = args["articleNom"]?.GetValue<string>();
            var dateDebutStr   = args["dateDebut"]?.GetValue<string>();
            var dateFinStr     = args["dateFin"]?.GetValue<string>();
            var fournisseurId  = args["fournisseurId"]?.GetValue<int?>();

            var query = _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient).ThenInclude(c => c.Client).ThenInclude(cl => cl.Plateforme)
                .Include(a => a.CommandeClient).ThenInclude(c => c.Marque).ThenInclude(m => m.Plateforme)
                .Include(a => a.LignesAchat).ThenInclude(l => l.Article)
                .Include(a => a.LignesAchat).ThenInclude(l => l.Plateforme)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutAchat>(statutStr, ignoreCase: true, out var statut))
                query = query.Where(a => a.Statut == statut);

            if (fournisseurId.HasValue)
                query = query.Where(a => a.FournisseurId == fournisseurId.Value);

            if (!string.IsNullOrWhiteSpace(fournisseurNom))
                query = query.Where(a =>
                    a.Fournisseur != null && a.Fournisseur.NomEntreprise.Contains(fournisseurNom));

            if (!string.IsNullOrWhiteSpace(plateformeNom))
                query = query.Where(a =>
                    a.LignesAchat.Any(l => l.Plateforme != null &&
                        l.Plateforme.Nom.Contains(plateformeNom)) ||
                    (a.CommandeClient != null &&
                     a.CommandeClient.Client != null && a.CommandeClient.Client.Plateforme != null &&
                     a.CommandeClient.Client.Plateforme.Nom.Contains(plateformeNom)) ||
                    (a.CommandeClient != null &&
                     a.CommandeClient.Marque != null && a.CommandeClient.Marque.Plateforme != null &&
                     a.CommandeClient.Marque.Plateforme.Nom.Contains(plateformeNom)));

            if (!string.IsNullOrWhiteSpace(articleNom))
                query = query.Where(a =>
                    a.LignesAchat.Any(l =>
                        l.Article.Designation.Contains(articleNom) ||
                        (l.Article.Reference != null && l.Article.Reference.Contains(articleNom))));

            if (DateTimeOffset.TryParse(dateDebutStr, out var debut))
                query = query.Where(a => a.DateAchat >= debut.UtcDateTime);

            if (DateTimeOffset.TryParse(dateFinStr, out var fin))
                query = query.Where(a => a.DateAchat < fin.UtcDateTime.AddDays(1)); // inclut la journée de fin

            var achats = await query
                .OrderByDescending(a => a.DateAchat)
                .Take(25)
                .Select(a => new
                {
                    a.Id,
                    a.NumeroAchat,
                    Fournisseur = a.Fournisseur.NomEntreprise,
                    Commande    = a.CommandeClient != null ? a.CommandeClient.NumeroCommande : null,
                    Plateforme  = a.LignesAchat.Where(l => l.Plateforme != null).Select(l => l.Plateforme!.Nom).FirstOrDefault()
                                  ?? (a.CommandeClient != null && a.CommandeClient.Client != null && a.CommandeClient.Client.Plateforme != null
                                      ? a.CommandeClient.Client.Plateforme.Nom : null)
                                  ?? (a.CommandeClient != null && a.CommandeClient.Marque != null && a.CommandeClient.Marque.Plateforme != null
                                      ? a.CommandeClient.Marque.Plateforme.Nom : null),
                    Statut      = a.Statut.ToString(),
                    a.DateAchat,
                    a.DateLivraisonPrevue,
                    a.CreePar,
                    a.MontantTotal,
                    a.Devise,
                    Lignes = a.LignesAchat.Select(l => new
                    {
                        l.Id,
                        Article       = l.Article.Designation,
                        ArticleRef    = l.Article.Reference,
                        l.Couleur,
                        l.Taille,
                        l.Dimension,
                        l.Quantite,
                        l.PrixUnitaire,
                        l.MontantLigne,
                        l.Devise,
                        Plateforme    = l.Plateforme != null ? l.Plateforme.Nom : null,
                        TypeDestination = l.TypeDestination.ToString()
                    })
                })
                .ToListAsync();

            return Json(new { count = achats.Count, achats });
        }

        private async Task<string> GetImportations(JsonNode args)
        {
            var statutStr      = args["statut"]?.GetValue<string>();
            var fournisseurNom = args["fournisseurNom"]?.GetValue<string>();
            var plateformeNom  = args["plateformeNom"]?.GetValue<string>();
            var articleNom     = args["articleNom"]?.GetValue<string>();
            var debutStr       = args["dateDebut"]?.GetValue<string>();
            var finStr         = args["dateFin"]?.GetValue<string>();
            var modeStr        = args["modeExpedition"]?.GetValue<string>();

            var query = _context.Importations
                .Include(i => i.Fournisseur)
                .Include(i => i.Plateforme)
                .Include(i => i.LignesImportation)
                    .ThenInclude(li => li.Article)
                .Include(i => i.LignesImportation)
                    .ThenInclude(li => li.Plateforme)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutImportation>(statutStr, ignoreCase: true, out var statut))
                query = query.Where(i => i.Statut == statut);

            if (!string.IsNullOrEmpty(modeStr) &&
                Enum.TryParse<ModeExpedition>(modeStr, ignoreCase: true, out var mode))
                query = query.Where(i => i.ModeExpedition == mode);

            if (!string.IsNullOrEmpty(fournisseurNom))
                query = query.Where(i => i.Fournisseur != null &&
                    i.Fournisseur.NomEntreprise.ToLower().Contains(fournisseurNom.ToLower()));

            if (!string.IsNullOrEmpty(plateformeNom))
                query = query.Where(i =>
                    (i.Plateforme != null && i.Plateforme.Nom.ToLower().Contains(plateformeNom.ToLower())) ||
                    i.LignesImportation.Any(l =>
                        l.Plateforme != null && l.Plateforme.Nom.ToLower().Contains(plateformeNom.ToLower())));

            if (!string.IsNullOrEmpty(articleNom))
                query = query.Where(i => i.LignesImportation.Any(l =>
                    l.Article.Designation.ToLower().Contains(articleNom.ToLower()) ||
                    (l.Article.Reference != null && l.Article.Reference.ToLower().Contains(articleNom.ToLower()))));

            if (DateTimeOffset.TryParse(debutStr, out var debut))
                query = query.Where(i => i.DateImportation >= debut.UtcDateTime);

            if (DateTimeOffset.TryParse(finStr, out var fin))
                query = query.Where(i => i.DateImportation < fin.UtcDateTime.AddDays(1)); // inclut la journée de fin

            var result = await query
                .OrderByDescending(i => i.DateCreation)
                .Take(25)
                .Select(i => new
                {
                    i.Id,
                    i.ReferenceImportation,
                    Fournisseur = i.Fournisseur != null ? i.Fournisseur.NomEntreprise : null,
                    PlateformeSource = i.Plateforme != null ? i.Plateforme.Nom : null,
                    Statut      = i.Statut.ToString(),
                    ModeExpedition = i.ModeExpedition.ToString(),
                    i.DateImportation,
                    i.DateReceptionPrevue,
                    i.DateReceptionReelle,
                    i.CreePar,
                    i.MontantTotal,
                    i.Devise,
                    Lignes = i.LignesImportation.Select(l => new
                    {
                        l.Id,
                        Article    = l.Article.Designation,
                        ArticleRef = l.Article.Reference,
                        l.Couleur,
                        l.Dimension,
                        l.Nature,
                        l.Quantite,
                        l.PrixUnitaire,
                        l.MontantLigne,
                        l.Devise,
                        Plateforme   = l.Plateforme != null ? l.Plateforme.Nom : null,
                        TypeDestination = l.TypeDestination.ToString(),
                        l.EstAffecteStock
                    })
                })
                .ToListAsync();

            return Json(new { count = result.Count, importations = result });
        }

        private async Task<string> GetMouvements(JsonNode args)
        {
            var articleId = args["articleId"]?.GetValue<int?>();
            var debutStr  = args["dateDebut"]?.GetValue<string>();
            var finStr    = args["dateFin"]?.GetValue<string>();

            DateTime? debut = DateTimeOffset.TryParse(debutStr, out var d)
                ? d.UtcDateTime : null;
            DateTime? fin = DateTimeOffset.TryParse(finStr, out var f)
                ? f.UtcDateTime.AddDays(1) : null;   // inclut la journée de fin

            var query = _context.MouvementsStock
                .Include(m => m.Stock).ThenInclude(s => s.Article)
                .AsQueryable();

            if (articleId.HasValue)
                query = query.Where(m => m.Stock.ArticleId == articleId.Value);

            if (debut.HasValue)
                query = query.Where(m => m.DateMouvement >= debut.Value);

            if (fin.HasValue)
                query = query.Where(m => m.DateMouvement < fin.Value);

            var mouvements = await query
                .OrderByDescending(m => m.DateMouvement)
                .Take(50)
                .Select(m => new
                {
                    m.Id,
                    Article          = m.Stock.Article.Designation,
                    TypeStock        = m.Stock.TypeStock.ToString(),
                    TypeMouvement    = m.TypeMouvement.ToString(),
                    OrigineMouvement = m.OrigineMouvement.ToString(),
                    m.Quantite,
                    m.QuantiteAvant,
                    m.QuantiteApres,
                    m.Motif,
                    m.DateMouvement,
                    m.EffectuePar
                })
                .ToListAsync();

            return Json(new { count = mouvements.Count, mouvements });
        }

        // ── Schéma de données (métadonnées statiques, aucune donnée de la base) ──

        private string GetSchema(JsonNode args)
        {
            var sujet = args["sujet"]?.GetValue<string>()?.Trim().ToLowerInvariant();

            // Hiérarchies et relations entre entités — le cœur que le modèle doit connaître.
            var entites = new object[]
            {
                new
                {
                    nom = "Plateforme",
                    description = "Place de marché (ex : dandy's). EstActif.",
                    relations = "A des Clients et des Marques.",
                    motsCles = "plateforme market place dandy"
                },
                new
                {
                    nom = "Client",
                    description = "Client/atelier, rattaché à UNE plateforme (PlateformeId).",
                    relations = "Client → Plateforme ; Client → Commandes.",
                    motsCles = "client atelier marque client"
                },
                new
                {
                    nom = "Marque",
                    description = "Marque rattachée à UNE plateforme (PlateformeId).",
                    relations = "Marque → Plateforme ; Marque → Commandes.",
                    motsCles = "marque brand"
                },
                new
                {
                    nom = "CommandeClient",
                    description = "Commande d'un client (ClientId) éventuellement d'une marque (MarqueId). Statuts : EnAttente, Prete, EnProduction, Terminee, Annulee.",
                    relations = "Commande → Client → Plateforme ; Commande → Marque → Plateforme.",
                    motsCles = "commande commandes client cde"
                },
                new
                {
                    nom = "Achat",
                    description = "Achat fournisseur local. FournisseurId obligatoire, CommandeClientId optionnel. Statuts : Brouillon, Soumis, Confirme, Livre, Annule.",
                    relations = "Achat → Fournisseur ; Achat → CommandeClient (→ Client → Plateforme / Marque → Plateforme) ; Achat → LignesAchat.",
                    motsCles = "achat achats fournisseur"
                },
                new
                {
                    nom = "LigneAchat",
                    description = "Article acheté. TypeDestination : Commande(0), Marque(1), Plateforme(2), StockLibre(3). Une ligne de destination Plateforme a un PlateformeId.",
                    relations = "LigneAchat → Article ; LigneAchat → Plateforme (si destination Plateforme).",
                    motsCles = "ligne achat article achete"
                },
                new
                {
                    nom = "Importation",
                    description = "Importation (souvent via import maritime/aérien). Origine du shipment entier (pas par ligne) : soit FournisseurId (achat direct), soit PlateformeId (la plateforme a groupé les commandes de plusieurs fournisseurs et envoie tout en un seul envoi) — exclusifs. Statuts : Brouillon, Soumise, Validee, Recue, Annulee. ModeExpedition : Maritime, Aerien, Terrestre, Express, Autre.",
                    relations = "Importation → Fournisseur ; Importation → Plateforme (source) ; Importation → LignesImportation.",
                    motsCles = "importation importations import"
                },
                new
                {
                    nom = "LigneImportation",
                    description = "Article importé. TypeDestination : Commande(0), Marque(1), Plateforme(2), StockLibre(3). PlateformeId pour une destination Plateforme. L'origine (Fournisseur ou Plateforme) est portée par l'Importation (en-tête), pas par la ligne.",
                    relations = "LigneImportation → Article ; LigneImportation → Plateforme (destination).",
                    motsCles = "ligne importation article importe"
                },
                new
                {
                    nom = "Fournisseur",
                    description = "Fournisseur (fournisseur local ou d'importation). NomEntreprise, EstActif.",
                    relations = "Fournisseur → Achats ; Fournisseur → Importations.",
                    motsCles = "fournisseur supplier"
                },
                new
                {
                    nom = "Article",
                    description = "Article du catalogue (matière première, accessoire, emballage). Designation, Reference, Categorie, Unite, SeuilAlerte/Critique.",
                    relations = "Article → Stocks ; Article → LignesAchat ; Article → LignesImportation ; Article → BesoinsCommande.",
                    motsCles = "article articles reference designation bobine bouton tissu"
                },
                new
                {
                    nom = "Stock",
                    description = "Stock d'un article. TypeStock : Libre(0), Reserve(1), Importe(2). Quantite, QuantiteReservee, scopes optionnels (CommandeClientId, ClientId, PlateformeId).",
                    relations = "Stock → Article.",
                    motsCles = "stock inventaire quantite"
                },
                new
                {
                    nom = "MouvementStock",
                    description = "Mouvement de stock. TypeMouvement : Entree, Sortie, Transfert, Ajustement, Reservation, Liberation. OrigineMouvement : Achat, Importation, Production, Ajustement, Transfert, Commande, Retour, Autre.",
                    relations = "MouvementStock → Stock → Article.",
                    motsCles = "mouvement mouvements entree sortie transfert historique"
                },
                new
                {
                    nom = "TacheProduction",
                    description = "Tâche de production liée à une commande (optionnel). Statuts : NonCommence, EnCours, Bloque, Termine, Annule.",
                    relations = "TacheProduction → CommandeClient.",
                    motsCles = "tache production taches"
                },
                new
                {
                    nom = "BesoinCommande",
                    description = "Besoin matière première d'une commande pour un article. QuantiteUnitaire, NombrePieces, QuantiteTotale.",
                    relations = "BesoinCommande → CommandeClient ; BesoinCommande → Article.",
                    motsCles = "besoin besoins matiere"
                }
            };

            // Aide au choix de l'outil : quelle question → quel outil.
            var guide = new object[]
            {
                new { question = "Article du catalogue / trouver un article par nom ou référence", outil = "get_articles(recherche=...) — puis utiliser l'id avec get_stock" },
                new { question = "Stock d'un article (total, réservé, disponible)", outil = "get_stock(articleId=...)" },
                new { question = "Commandes (statut, client, marque, plateforme, période)", outil = "get_commandes(clientNom/marqueNom/plateformeNom/dateDebut/dateFin/statut)" },
                new { question = "Achats fournisseurs et leurs lignes/articles", outil = "get_achats(fournisseurNom/plateformeNom/articleNom/dateDebut/dateFin/statut)" },
                new { question = "Importations et leurs lignes/articles", outil = "get_importations(fournisseurNom/plateformeNom/articleNom/dateDebut/dateFin/statut/modeExpedition)" },
                new { question = "Historique des mouvements de stock d'un article ou période", outil = "get_mouvements(articleId/dateDebut/dateFin)" },
                new { question = "Structure des données / relations / doute sur l'entité à interroger", outil = "get_schema(sujet=...)" }
            };

            // Règles critiques pour éviter les faux négatifs.
            var regles = new object[]
            {
                "La plateforme d'un ACHAT peut venir des LIGNES (typeDestination=Plateforme + plateformeId) OU de la COMMANDE liée (commandeClient.client.plateforme / commandeClient.marque.plateforme). Pour filtrer par plateforme, get_achats couvre les deux sources.",
                "Filtrer par NOM (fournisseur, client, marque, plateforme, article) via les paramètres texte des outils, JAMAIS par un ID.",
                "Les enums voyagent en nombres côté API, mais les outils renvoient leur libellé texte (ex : statut = 'Confirme').",
                "Les statuts des achats (Brouillon/Soumis/Confirme/Livre/Annule) diffèrent de ceux des importations (Brouillon/Soumise/Validee/Recue/Annulee)."
            };

            var schema = new
            {
                description = "Modèle de données de l'IMS (atelier textile tunisien). Utilisez ce schéma pour identifier l'entité et la relation concernées par la question, puis choisissez l'outil adapté.",
                entites,
                guide,
                regles
            };

            // Filtre optionnel par sujet : on renvoie l'entité correspondante + le guide + les règles.
            if (!string.IsNullOrEmpty(sujet))
            {
                var entite = entites.FirstOrDefault(e =>
                {
                    var json = Json(e);
                    var nom = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("nom").GetString();
                    var mots = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("motsCles").GetString();
                    return (nom?.Contains(sujet, StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (mots?.Contains(sujet, StringComparison.OrdinalIgnoreCase) ?? false);
                });

                if (entite != null)
                    return Json(new { sujet, entite, guide, regles });
            }

            return Json(schema);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string Json(object obj) =>
            JsonSerializer.Serialize(obj, _jsonOpts);

        private static string Err(string message) =>
            JsonSerializer.Serialize(new { error = message }, _jsonOpts);
    }
}
