using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Partage;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Partage
{
    public interface IShareLinkService
    {
        Task<CreateShareLinkResponseDto?> CreerAsync(
            CreateShareLinkDto dto, string userId, CancellationToken ct);

        Task<IReadOnlyList<ShareLinkDto>> ListerAsync(
            string userId, bool estAdmin, CancellationToken ct);

        Task<bool> RevoquerAsync(
            int id, string userId, bool estAdmin, CancellationToken ct);

        Task<PartagePublicDto?> OuvrirAsync(
            string token, string? ip, CancellationToken ct);
    }

    public sealed class ShareLinkService : IShareLinkService
    {
        /// <summary>Plafond de durée : 7 jours. Une durée demandée au-delà est ramenée ici.</summary>
        public const int DureeMaxHeures = 24 * 7;

        private readonly ApplicationDbContext _db;
        private readonly ShareScopeResolver _resolver;
        private readonly ILogger<ShareLinkService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public ShareLinkService(
            ApplicationDbContext db,
            ShareScopeResolver resolver,
            ILogger<ShareLinkService> logger)
        {
            _db = db;
            _resolver = resolver;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Création
        // ─────────────────────────────────────────────────────────────────────────

        public async Task<CreateShareLinkResponseDto?> CreerAsync(
            CreateShareLinkDto dto, string userId, CancellationToken ct)
        {
            if (!SectionsValides(dto.Sections)) return null;
            if (dto.MaxUses is <= 0) return null;

            var scope = await _resolver.ResoudreAsync(dto.ScopeType, dto.ScopeId, ct);
            if (scope == null) return null;

            var token = ShareTokenGenerator.Generer();

            var duree = Math.Clamp(dto.DureeHeures, 1, DureeMaxHeures);
            var link = new ShareLink
            {
                TokenHash = ShareTokenGenerator.Hasher(token),
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(duree),
                ScopeType = dto.ScopeType,
                ScopeId = dto.ScopeId,
                Sections = dto.Sections,
                IsStockLibreAllowed = dto.IsStockLibreAllowed,
                MaxUses = dto.MaxUses,
                Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim(),
                FiltersJson = SerialiserFiltres(dto.Filters),
            };

            _db.ShareLinks.Add(link);
            await _db.SaveChangesAsync(ct);

            // Le token n'est renvoyé qu'ici, et n'est jamais relu depuis la base
            // (la base ne contient que TokenHash).
            return new CreateShareLinkResponseDto
            {
                Token = token,
                Link = VersDto(link, scope.Libelle, dto.Filters),
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Liste / révocation
        // ─────────────────────────────────────────────────────────────────────────

        public async Task<IReadOnlyList<ShareLinkDto>> ListerAsync(
            string userId, bool estAdmin, CancellationToken ct)
        {
            var query = _db.ShareLinks.AsNoTracking();
            // Un porteur non administrateur ne voit que ses propres liens. L'admin
            // voit tout : c'est lui qui révoque en dernier recours après un départ.
            if (!estAdmin) query = query.Where(l => l.CreatedByUserId == userId);

            var links = await query
                .OrderByDescending(l => l.CreatedAtUtc)
                .ToListAsync(ct);

            var libelles = await ResoudreLibellesAsync(links, ct);
            var maintenant = DateTime.UtcNow;

            return links
                .Select(l => VersDto(
                    l,
                    libelles.GetValueOrDefault((l.ScopeType, l.ScopeId), "—"),
                    DeserialiserFiltres(l.FiltersJson),
                    maintenant))
                .ToList();
        }

        public async Task<bool> RevoquerAsync(
            int id, string userId, bool estAdmin, CancellationToken ct)
        {
            var link = await _db.ShareLinks.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (link == null) return false;

            // Non propriétaire et non administrateur : on renvoie false, que le
            // controller traduit en 404 — pas 403. Un 403 confirmerait l'existence
            // d'un lien à quelqu'un qui n'a pas à le savoir.
            if (!estAdmin && link.CreatedByUserId != userId) return false;

            if (link.RevokedAtUtc == null)
            {
                link.RevokedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Ouverture publique
        // ─────────────────────────────────────────────────────────────────────────

        public async Task<PartagePublicDto?> OuvrirAsync(
            string token, string? ip, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var hash = ShareTokenGenerator.Hasher(token.Trim());
            var maintenant = DateTime.UtcNow;

            // Validation ET incrément en UNE instruction conditionnelle : entre un
            // SELECT puis UPDATE, deux requêtes concurrentes pourraient toutes deux
            // voir UseCount = MaxUses - 1 et dépasser la limite. Ici, une seule des
            // deux obtient updated == 1 ; l'autre retombe sur le chemin « refusé ».
            var maj = await _db.ShareLinks
                .Where(l => l.TokenHash == hash
                         && l.RevokedAtUtc == null
                         && l.ExpiresAtUtc > maintenant
                         && (l.MaxUses == null || l.UseCount < l.MaxUses))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.UseCount, l => l.UseCount + 1), ct);

            if (maj == 0)
            {
                await JournaliserRefusAsync(hash, ip, ct);
                return null;
            }

            var link = await _db.ShareLinks.AsNoTracking()
                .FirstOrDefaultAsync(l => l.TokenHash == hash, ct);

            // Ne devrait pas arriver (l'update vient de réussir), mais on ne sert
            // jamais un lien non relu : garde-fou symétrique.
            if (link == null) return null;

            var scope = await _resolver.ResoudreAsync(link.ScopeType, link.ScopeId, ct);
            if (scope == null)
            {
                // Portée supprimée depuis la création : lien inerte. On trace en
                // « refusé » et on renvoie null (404 identique).
                await JournaliserRefusAsync(hash, ip, ct);
                return null;
            }

            var filtres = DeserialiserFiltres(link.FiltersJson);
            var dto = await ConstruireReponseAsync(link, scope, filtres, ct);

            _db.ShareLinkAccess.Add(new ShareLinkAccess
            {
                ShareLinkId = link.Id,
                Date = maintenant,
                IpAddress = TronquerIp(ip),
                Result = ShareLinkAccessResult.Autorise,
            });
            await _db.SaveChangesAsync(ct);

            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Construction de la réponse publique
        // ─────────────────────────────────────────────────────────────────────────

        private async Task<PartagePublicDto> ConstruireReponseAsync(
            ShareLink link, PartageScope scope, ShareLinkFiltersDto? filtres, CancellationToken ct)
        {
            var dto = new PartagePublicDto
            {
                Label = link.Label,
                ScopeType = link.ScopeType.ToString(),
                ScopeLibelle = scope.Libelle,
                DonneesAu = DateTime.UtcNow,
                Sections = SectionsActives(link.Sections),
            };

            if (link.Sections.HasFlag(ShareLinkSection.Stock))
                dto.Stock = await ChargerStockAsync(scope, link.IsStockLibreAllowed, filtres, ct);

            if (link.Sections.HasFlag(ShareLinkSection.Commandes))
                dto.Commandes = await ChargerCommandesAsync(scope, filtres, ct);

            if (link.Sections.HasFlag(ShareLinkSection.Importations))
                dto.Importations = await ChargerImportationsAsync(scope, link.IsStockLibreAllowed, filtres, ct);

            return dto;
        }

        private async Task<List<PartageStockDto>> ChargerStockAsync(
            PartageScope scope, bool stockLibre, ShareLinkFiltersDto? f, CancellationToken ct)
        {
            var plateformeIds = scope.PlateformeIds.ToArray();
            var clientIds = scope.ClientIds.ToArray();
            var commandeIds = scope.CommandeIds.ToArray();
            var groupeIds = scope.GroupeIds.ToArray();

            var query = _db.Stocks.AsNoTracking().AsQueryable().Where(s =>
                (s.PlateformeId != null && plateformeIds.Contains(s.PlateformeId.Value))
                || (s.ClientId != null && clientIds.Contains(s.ClientId.Value))
                || (s.CommandeClientId != null && commandeIds.Contains(s.CommandeClientId.Value))
                || (s.GroupeCommandeId != null && groupeIds.Contains(s.GroupeCommandeId.Value))
                // Stock libre : uniquement si explicitement autorisé. Un stock sans
                // aucun scope n'appartient à personne ; l'exposer est un opt-in.
                || (stockLibre
                    && s.PlateformeId == null && s.ClientId == null
                    && s.CommandeClientId == null && s.GroupeCommandeId == null));

            query = AppliquerFiltresStock(query, f);

            // Projection partielle volontaire : ni PrixUnitaire, ni PrixUnitaireTND,
            // ni Notes, ni ValidePar. La sélection se fait en SQL, pas après coup.
            var lignes = await query
                .OrderBy(s => s.Id)
                .Select(s => new
                {
                    s.Id,
                    ArticleDesignation = s.Article != null ? s.Article.Designation : null,
                    ArticleReference = s.Article != null ? s.Article.Reference : null,
                    s.Couleur,
                    s.Taille,
                    s.Dimension,
                    s.EmplacementPhysique,
                    s.Quantite,
                    s.QuantiteReservee,
                    s.TypeStock,
                })
                .ToListAsync(ct);

            // Conversion enum → libellé en mémoire : « TypeStock.ToString() » n'est
            // pas traduisible en SQL de façon fiable selon les fournisseurs.
            return lignes.Select(s => new PartageStockDto
            {
                Id = s.Id,
                ArticleDesignation = s.ArticleDesignation,
                ArticleReference = s.ArticleReference,
                Couleur = s.Couleur,
                Taille = s.Taille,
                Dimension = s.Dimension,
                EmplacementPhysique = s.EmplacementPhysique,
                Quantite = s.Quantite,
                QuantiteReservee = s.QuantiteReservee,
                TypeStock = s.TypeStock.ToString(),
            }).ToList();
        }

        private async Task<List<PartageCommandeDto>> ChargerCommandesAsync(
            PartageScope scope, ShareLinkFiltersDto? f, CancellationToken ct)
        {
            var clientIds = scope.ClientIds.ToArray();
            var commandeIds = scope.CommandeIds.ToArray();

            // Aucune commande possible : portée dont l'ensemble commande est vide.
            if (clientIds.Length == 0 && commandeIds.Length == 0) return [];

            var query = _db.CommandesClients.AsNoTracking().Include(c => c.Client).Where(c =>
                commandeIds.Contains(c.Id) || clientIds.Contains(c.ClientId));

            if (!string.IsNullOrWhiteSpace(f?.Statut)
                && Enum.TryParse<StatutCommande>(f.Statut.Trim(), ignoreCase: true, out var statut))
                query = query.Where(c => c.Statut == statut);

            if (f?.DateDebut is DateTime debut)
                query = query.Where(c => c.DateLivraisonSouhaitee != null && c.DateLivraisonSouhaitee >= debut);
            if (f?.DateFin is DateTime fin)
                query = query.Where(c => c.DateLivraisonSouhaitee != null && c.DateLivraisonSouhaitee <= fin);

            var lignes = await query
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    c.Statut,
                    c.DateLivraisonSouhaitee,
                    ClientNom = c.Client != null ? c.Client.Nom : null,
                })
                .ToListAsync(ct);

            // Pas de MontantTotal, pas de PrixFacon, pas de NotesSpeciales.
            return lignes.Select(c => new PartageCommandeDto
            {
                Id = c.Id,
                NumeroCommande = c.NumeroCommande,
                TitreCommande = c.TitreCommande,
                Statut = c.Statut.ToString(),
                DateLivraisonSouhaitee = c.DateLivraisonSouhaitee,
                ClientNom = c.ClientNom,
            }).ToList();
        }

        private async Task<List<PartageImportationDto>> ChargerImportationsAsync(
            PartageScope scope, bool stockLibre, ShareLinkFiltersDto? f, CancellationToken ct)
        {
            var plateformeIds = scope.PlateformeIds.ToArray();
            var clientIds = scope.ClientIds.ToArray();
            var commandeIds = scope.CommandeIds.ToArray();
            var groupeIds = scope.GroupeIds.ToArray();

            // Scoping piloté par TypeDestination EN PREMIER, puis par la FK
            // concordante. Une FK résiduelle non concordante (ligne « Marque » portant
            // un PlateformeId d'une autre plateforme) n'est jamais regardée : seule la
            // FK correspondant au TypeDestination décide. Corrige le défaut
            // d'exclusivité des FK côté écriture (ImportationController:252-268).
            var query = _db.LignesImportation.AsNoTracking().Where(l =>
                (l.TypeDestination == TypeDestinationImportation.Commande
                    && l.CommandeClientId != null && commandeIds.Contains(l.CommandeClientId.Value))
                || (l.TypeDestination == TypeDestinationImportation.Marque
                    && l.ClientId != null && clientIds.Contains(l.ClientId.Value))
                || (l.TypeDestination == TypeDestinationImportation.Plateforme
                    && l.PlateformeId != null && plateformeIds.Contains(l.PlateformeId.Value))
                || (l.TypeDestination == TypeDestinationImportation.GroupeCommandes
                    && l.GroupeCommandeId != null && groupeIds.Contains(l.GroupeCommandeId.Value))
                || (l.TypeDestination == TypeDestinationImportation.StockLibre && stockLibre));

            if (!string.IsNullOrWhiteSpace(f?.Article))
            {
                var a = f.Article.Trim().ToLower();
                query = query.Where(l =>
                    (l.Article != null && l.Article.Designation.ToLower().Contains(a))
                    || (l.Designation != null && l.Designation.ToLower().Contains(a)));
            }
            if (!string.IsNullOrWhiteSpace(f?.Couleur))
            {
                var c = f.Couleur.Trim().ToLower();
                query = query.Where(l => l.Couleur != null && l.Couleur.ToLower().Contains(c));
            }
            if (!string.IsNullOrWhiteSpace(f?.Statut)
                && Enum.TryParse<StatutLigneImportation>(f.Statut.Trim(), ignoreCase: true, out var statut))
                query = query.Where(l => l.StatutLigne == statut);
            if (f?.DateDebut is DateTime debut)
                query = query.Where(l => l.DateCreation >= debut);
            if (f?.DateFin is DateTime fin)
                query = query.Where(l => l.DateCreation <= fin);

            var lignes = await query
                .OrderBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    ReferenceImportation = l.Importation != null ? l.Importation.ReferenceImportation : null,
                    ArticleDesignation = l.Article != null ? l.Article.Designation : null,
                    l.Designation,
                    l.Couleur,
                    l.Quantite,
                    l.QuantiteRecue,
                    l.StatutLigne,
                    l.TypeDestination,
                })
                .ToListAsync(ct);

            // Ni PrixUnitaire, ni MontantLigne, ni Devise : ce sont des données
            // commerciales, pas des données de suivi de quantité.
            return lignes.Select(l => new PartageImportationDto
            {
                Id = l.Id,
                ReferenceImportation = l.ReferenceImportation ?? string.Empty,
                ArticleDesignation = l.ArticleDesignation,
                Designation = l.Designation,
                Couleur = l.Couleur,
                Quantite = l.Quantite,
                QuantiteRecue = l.QuantiteRecue,
                Statut = l.StatutLigne.ToString(),
                TypeDestination = l.TypeDestination.ToString(),
            }).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Filtres (liste blanche appliquée en mémoire, sérialisée en JSON)
        // ─────────────────────────────────────────────────────────────────────────

        private static IQueryable<Stock> AppliquerFiltresStock(
            IQueryable<Stock> query, ShareLinkFiltersDto? f)
        {
            if (f == null) return query;

            if (!string.IsNullOrWhiteSpace(f.Article))
            {
                var a = f.Article.Trim().ToLower();
                query = query.Where(s =>
                    (s.Article != null && s.Article.Designation.ToLower().Contains(a))
                    || (s.Article != null && s.Article.Reference != null && s.Article.Reference.ToLower().Contains(a)));
            }
            if (!string.IsNullOrWhiteSpace(f.Couleur))
            {
                var c = f.Couleur.Trim().ToLower();
                query = query.Where(s => s.Couleur != null && s.Couleur.ToLower().Contains(c));
            }
            if (!string.IsNullOrWhiteSpace(f.Taille))
            {
                var t = f.Taille.Trim().ToLower();
                query = query.Where(s => s.Taille != null && s.Taille.ToLower().Contains(t));
            }

            // Sélection d'articles. Liste déjà validée à la création (existence,
            // activité, unicité, plafond) : on ne la réinterprète pas ici. Le
            // Contains sur une liste figée est traduit en SQL par PostgreSQL.
            if (f.ArticleIds is { Count: > 0 })
            {
                var ids = f.ArticleIds.Distinct().ToArray();
                query = query.Where(s => ids.Contains(s.ArticleId));
            }

            if (!string.IsNullOrWhiteSpace(f.Categorie))
            {
                var categorie = f.Categorie.Trim();
                query = query.Where(s => s.Article != null && s.Article.Categorie == categorie);
            }

            if (!string.IsNullOrWhiteSpace(f.TypeStock))
            {
                if (!Enum.TryParse<TypeStock>(f.TypeStock.Trim(), ignoreCase: true, out var typeStock))
                {
                    // Valeur invalide : on ne restreint pas le périmètre, mais elle
                    // a déjà été rejetée à la création. Défensif uniquement.
                    return query;
                }
                query = query.Where(s => s.TypeStock == typeStock);
            }

            return query;
        }

        /// <summary>
        /// Sérialise les filtres en ne recopiant QUE des valeurs déjà typées par le
        /// DTO : aucune donnée brute du client n'est conservée telle quelle.
        /// </summary>
        private static string? SerialiserFiltres(ShareLinkFiltersDto? f)
        {
            if (f == null) return null;

            var propre = new ShareLinkFiltersDto
            {
                Article = Nettoyer(f.Article),
                Couleur = Nettoyer(f.Couleur),
                Taille = Nettoyer(f.Taille),
                Statut = Nettoyer(f.Statut),
                DateDebut = f.DateDebut,
                DateFin = f.DateFin,
                Categorie = Nettoyer(f.Categorie),
                TypeStock = Nettoyer(f.TypeStock)?.ToLowerInvariant(),
                ArticleIds = f.ArticleIds is { Count: > 0 }
                    ? f.ArticleIds.Distinct().OrderBy(i => i).ToList()
                    : null,
            };

            var vide = propre.Article == null && propre.Couleur == null && propre.Taille == null
                    && propre.Statut == null && propre.DateDebut == null && propre.DateFin == null
                    && propre.Categorie == null && propre.TypeStock == null
                    && (propre.ArticleIds == null || propre.ArticleIds.Count == 0);
            if (vide) return null;

            return JsonSerializer.Serialize(propre, JsonOptions);
        }

        private static ShareLinkFiltersDto? DeserialiserFiltres(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<ShareLinkFiltersDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                // JSON corrompu : on ignore les filtres plutôt que de faire échouer
                // l'ouverture. Le périmètre, lui, reste appliqué — le pire cas est une
                // liste un peu plus large, jamais un débordement de portée.
                return null;
            }
        }

        private static string? Nettoyer(string? valeur) =>
            string.IsNullOrWhiteSpace(valeur) ? null : valeur.Trim();

        // ─────────────────────────────────────────────────────────────────────────
        // Journalisation / helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Journalise un refus si (et seulement si) l'empreinte correspond à un lien
        /// connu. Un token purement inconnu ne crée AUCUNE ligne : sinon n'importe qui
        /// pourrait remplir la table d'accès avec des tokens inventés.
        /// </summary>
        private async Task JournaliserRefusAsync(string hash, string? ip, CancellationToken ct)
        {
            var link = await _db.ShareLinks.AsNoTracking()
                .Where(l => l.TokenHash == hash)
                .Select(l => new { l.Id })
                .FirstOrDefaultAsync(ct);

            if (link == null) return;

            _db.ShareLinkAccess.Add(new ShareLinkAccess
            {
                ShareLinkId = link.Id,
                Date = DateTime.UtcNow,
                IpAddress = TronquerIp(ip),
                Result = ShareLinkAccessResult.Refuse,
            });
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>45 caractères = taille max d'une IPv6 textuelle. Tronque plutôt que jeter.</summary>
        private static string? TronquerIp(string? ip) =>
            string.IsNullOrWhiteSpace(ip) ? null : ip.Trim()[..Math.Min(ip.Trim().Length, 45)];

        private static bool SectionsValides(ShareLinkSection sections) =>
            sections != ShareLinkSection.Aucune && (sections & ~ShareLinkSection.Tout) == 0;

        private static string[] SectionsActives(ShareLinkSection sections) =>
            Enum.GetValues<ShareLinkSection>()
                .Where(s => s != ShareLinkSection.Aucune && s != ShareLinkSection.Tout && sections.HasFlag(s))
                .Select(s => s.ToString())
                .ToArray();

        private static ShareLinkDto VersDto(
            ShareLink link, string scopeLibelle, ShareLinkFiltersDto? filtres, DateTime? maintenant = null)
        {
            var now = maintenant ?? DateTime.UtcNow;
            return new ShareLinkDto
            {
                Id = link.Id,
                Label = link.Label,
                ScopeType = link.ScopeType,
                ScopeId = link.ScopeId,
                ScopeLibelle = scopeLibelle,
                Sections = link.Sections,
                IsStockLibreAllowed = link.IsStockLibreAllowed,
                Filters = filtres,
                CreatedAtUtc = link.CreatedAtUtc,
                ExpiresAtUtc = link.ExpiresAtUtc,
                RevokedAtUtc = link.RevokedAtUtc,
                UseCount = link.UseCount,
                MaxUses = link.MaxUses,
                Actif = link.RevokedAtUtc == null && link.ExpiresAtUtc > now,
            };
        }

        /// <summary>
        /// Résout les libellés de portée en lot (3 requêtes pour N liens), au lieu de
        /// faire un aller-retour par lien dans la liste.
        /// </summary>
        private async Task<Dictionary<(ShareLinkScopeType, int), string>> ResoudreLibellesAsync(
            IReadOnlyCollection<ShareLink> links, CancellationToken ct)
        {
            var resultat = new Dictionary<(ShareLinkScopeType, int), string>();

            var plateformeIds = links.Where(l => l.ScopeType == ShareLinkScopeType.Plateforme)
                .Select(l => l.ScopeId).Distinct().ToList();
            var clientIds = links.Where(l => l.ScopeType == ShareLinkScopeType.Marque)
                .Select(l => l.ScopeId).Distinct().ToList();
            var commandeIds = links.Where(l => l.ScopeType == ShareLinkScopeType.Commande)
                .Select(l => l.ScopeId).Distinct().ToList();

            if (plateformeIds.Count > 0)
            {
                var noms = await _db.Plateformes.AsNoTracking()
                    .Where(p => plateformeIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Nom, ct);
                foreach (var id in plateformeIds)
                    resultat[(ShareLinkScopeType.Plateforme, id)] = noms.GetValueOrDefault(id, "—");
            }

            if (clientIds.Count > 0)
            {
                var noms = await _db.Clients.AsNoTracking()
                    .Where(c => clientIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Nom, ct);
                foreach (var id in clientIds)
                    resultat[(ShareLinkScopeType.Marque, id)] = noms.GetValueOrDefault(id, "—");
            }

            if (commandeIds.Count > 0)
            {
                var nums = await _db.CommandesClients.AsNoTracking()
                    .Where(c => commandeIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.NumeroCommande, ct);
                foreach (var id in commandeIds)
                    resultat[(ShareLinkScopeType.Commande, id)] = nums.GetValueOrDefault(id, "—");
            }

            return resultat;
        }
    }
}