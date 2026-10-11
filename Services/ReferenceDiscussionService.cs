using System.Text.RegularExpressions;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Discussion;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Correspondance entre un objet métier, son segment d'URL, son préfixe de
    /// référence et le module de permission qui en régit l'accès (E3).
    ///
    /// C'est l'unique table de vérité : ajouter un objet contextuel = ajouter une
    /// ligne ici, jamais une condition dispersée dans le contrôleur ou le frontend.
    /// </summary>
    public static class ContexteDiscussion
    {
        /// <summary>Segment d'URL (minuscules) → nature de l'objet.</summary>
        public static bool TryDepuisSegment(string segment, out TypeContexte type)
        {
            switch ((segment ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "commande": type = TypeContexte.Commande; return true;
                case "of": type = TypeContexte.OrdreFabrication; return true;
                case "tache": type = TypeContexte.TacheProduction; return true;
                case "achat": type = TypeContexte.Achat; return true;
                default: type = default; return false;
            }
        }

        /// <summary>Préfixe de référence (sans #), ex. "CMD".</summary>
        public static string Prefixe(TypeContexte type) => type switch
        {
            TypeContexte.Commande => "CMD",
            TypeContexte.OrdreFabrication => "OF",
            TypeContexte.TacheProduction => "TAC",
            TypeContexte.Achat => "ACH",
            _ => "???"
        };

        /// <summary>Préfixe (sans #) → nature, insensible à la casse.</summary>
        public static bool TryDepuisPrefixe(string prefixe, out TypeContexte type)
        {
            switch ((prefixe ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CMD": type = TypeContexte.Commande; return true;
                case "OF": type = TypeContexte.OrdreFabrication; return true;
                case "TAC": type = TypeContexte.TacheProduction; return true;
                case "ACH": type = TypeContexte.Achat; return true;
                default: type = default; return false;
            }
        }

        /// <summary>
        /// Module de permission qui régit l'objet. C'est LUI, et non l'appartenance,
        /// qui ouvre la conversation contextuelle : un utilisateur sans ce module ne
        /// voit ni la discussion ni les cartes.
        /// </summary>
        public static string Module(TypeContexte type) => type switch
        {
            TypeContexte.Commande => "commandes",
            TypeContexte.OrdreFabrication => "production",
            TypeContexte.TacheProduction => "taches",
            TypeContexte.Achat => "achats",
            _ => string.Empty
        };

        /// <summary>Valeur stockée dans Conversation.ContexteType.</summary>
        public static string Stockage(TypeContexte type) => type.ToString();

        /// <summary>Relit la valeur stockée ; false si absente ou inconnue (objet futur).</summary>
        public static bool TryDepuisStockage(string? valeur, out TypeContexte type)
        {
            if (string.IsNullOrWhiteSpace(valeur))
            {
                type = default;
                return false;
            }
            return Enum.TryParse(valeur, ignoreCase: true, out type);
        }
    }

    public interface IReferenceDiscussionService
    {
        /// <summary>
        /// Résout, pour le LECTEUR donné, les cartes des références présentes dans une
        /// série de messages. Résultat indexé par identifiant de message.
        /// </summary>
        Task<Dictionary<long, List<ReferenceDto>>> ResoudreMessagesAsync(
            IReadOnlyList<(long MessageId, string Contenu)> messages,
            string lecteurUserId,
            CancellationToken cancellationToken = default);

        /// <summary>Autocomplétion : uniquement les objets accessibles au lecteur.</summary>
        Task<List<ResultatReferenceDto>> RechercherAsync(
            string recherche,
            string lecteurUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Libellé affichable de l'objet (nom de la conversation contextuelle), ou null
        /// s'il n'existe pas. N'applique AUCUN contrôle de droits : l'appelant le fait.
        /// </summary>
        Task<string?> LibelleObjetAsync(
            TypeContexte type,
            int id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Participants « responsables assignés » initiaux d'un objet : résolus depuis
        /// les FK utilisateur quand elles existent, sinon depuis le nom d'auteur legacy
        /// (CreePar) rapproché du UserName. Toujours des comptes actifs ET internes.
        /// </summary>
        Task<List<string>> ParticipantsResponsablesAsync(
            TypeContexte type,
            int id,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Calcul côté serveur des cartes de référence (#CMD-123, #OF-…, #TAC-…, #ACH-…),
    /// TOUJOURS selon les droits du lecteur (E3).
    ///
    /// Deux garde-fous non négociables :
    /// <list type="bullet">
    /// <item>un objet dont le lecteur n'a pas le module ne produit AUCUNE carte — pas
    /// une carte vide, pas un libellé : rien ;</item>
    /// <item>les champs sensibles (montant, devise, tiers) ne sont renseignés que si le
    /// lecteur a le droit d'ÉCRITURE du module. Le serveur ne les émet jamais pour un
    /// simple lecteur : ils ne transitent donc pas, même masqués côté client.</item>
    /// </list>
    /// </summary>
    public class ReferenceDiscussionService : IReferenceDiscussionService
    {
        private const int MaxParType = 5;
        private const int MaxTotalRecherche = 12;

        private static readonly Regex MotifReference =
            new(@"#(CMD|OF|TAC|ACH)-(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ApplicationDbContext _db;
        private readonly IPermissionService _permissions;

        public ReferenceDiscussionService(ApplicationDbContext db, IPermissionService permissions)
        {
            _db = db;
            _permissions = permissions;
        }

        public async Task<Dictionary<long, List<ReferenceDto>>> ResoudreMessagesAsync(
            IReadOnlyList<(long MessageId, string Contenu)> messages,
            string lecteurUserId,
            CancellationToken cancellationToken = default)
        {
            var parMessage = new Dictionary<long, List<ReferenceDto>>();
            if (messages.Count == 0) return parMessage;

            // 1. Parse de chaque message : on mémorise l'ordre d'apparition des jetons.
            var jetonsMessage = new Dictionary<long, List<(TypeContexte Type, int Id, string Jeton)>>();
            var demandes = new HashSet<(TypeContexte Type, int Id)>();

            foreach (var (messageId, contenu) in messages)
            {
                var jetons = new List<(TypeContexte, int, string)>();
                if (!string.IsNullOrEmpty(contenu))
                {
                    foreach (Match m in MotifReference.Matches(contenu))
                    {
                        if (!ContexteDiscussion.TryDepuisPrefixe(m.Groups[1].Value, out var type))
                            continue;
                        if (!int.TryParse(m.Groups[2].Value, out var id) || id <= 0)
                            continue;
                        var jeton = $"{ContexteDiscussion.Prefixe(type)}-{id}";
                        jetons.Add((type, id, jeton));
                        demandes.Add((type, id));
                    }
                }
                jetonsMessage[messageId] = jetons;
            }

            if (demandes.Count == 0)
            {
                foreach (var id in jetonsMessage.Keys) parMessage[id] = new List<ReferenceDto>();
                return parMessage;
            }

            // 2. Droits du lecteur, une fois par module concerné.
            var droits = new Dictionary<TypeContexte, (bool CanAccess, bool CanWrite)>();
            foreach (var type in demandes.Select(d => d.Type).Distinct())
            {
                var module = ContexteDiscussion.Module(type);
                var (canAccess, canWrite) = module.Length == 0
                    ? (false, false)
                    : await _permissions.GetPermissionAsync(lecteurUserId, module);
                droits[type] = (canAccess, canWrite);
            }

            // 3. Résolution EN LOT par type (au plus 4 requêtes, jamais une par jeton).
            var cartes = new Dictionary<string, ReferenceDto>();
            foreach (var type in demandes.Select(d => d.Type).Distinct())
            {
                var (canAccess, canWrite) = droits[type];
                if (!canAccess) continue; // aucune carte pour ce type

                var ids = demandes.Where(d => d.Type == type).Select(d => d.Id).Distinct().ToList();
                foreach (var carte in await ChargerCartesAsync(type, ids, canWrite, cancellationToken))
                    cartes[ContexteDiscussion.Prefixe(type) + "-" + carte.Id] = carte;
            }

            // 4. Rattachement aux messages, en préservant l'ordre et sans doublon.
            foreach (var (messageId, jetons) in jetonsMessage)
            {
                var liste = new List<ReferenceDto>();
                var vus = new HashSet<string>();
                foreach (var (_, _, jeton) in jetons)
                {
                    if (!vus.Add(jeton)) continue;
                    if (cartes.TryGetValue(jeton, out var carte)) liste.Add(carte);
                }
                parMessage[messageId] = liste;
            }

            return parMessage;
        }

        private async Task<List<ReferenceDto>> ChargerCartesAsync(
            TypeContexte type, List<int> ids, bool canWrite, CancellationToken cancellationToken)
        {
            switch (type)
            {
                case TypeContexte.Commande:
                    var commandes = await _db.CommandesClients
                        .AsNoTracking()
                        .Where(c => ids.Contains(c.Id))
                        .Select(c => new
                        {
                            c.Id,
                            c.NumeroCommande,
                            c.TitreCommande,
                            c.Statut,
                            c.DateCommande,
                            c.MontantTotal,
                            c.Devise,
                            Client = c.Client.Nom
                        })
                        .ToListAsync(cancellationToken);

                    return commandes.Select(c => new ReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(TypeContexte.Commande),
                        Reference = "CMD-" + c.Id,
                        Id = c.Id,
                        Libelle = string.IsNullOrWhiteSpace(c.TitreCommande)
                            ? c.NumeroCommande
                            : c.NumeroCommande + " — " + c.TitreCommande,
                        Statut = c.Statut.ToString(),
                        Date = c.DateCommande,
                        Montant = canWrite ? c.MontantTotal : null,
                        Devise = canWrite ? c.Devise : null,
                        Tiers = canWrite ? c.Client : null
                    }).ToList();

                case TypeContexte.OrdreFabrication:
                    var ordres = await _db.OrdresFabrication
                        .AsNoTracking()
                        .Where(o => ids.Contains(o.Id))
                        .Select(o => new
                        {
                            o.Id,
                            o.NumeroOF,
                            o.DateCreation,
                            CommandeNumero = o.Commande.NumeroCommande,
                            Chaine = o.ChaineProduction != null ? o.ChaineProduction.Nom : null
                        })
                        .ToListAsync(cancellationToken);

                    return ordres.Select(o => new ReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(TypeContexte.OrdreFabrication),
                        Reference = "OF-" + o.Id,
                        Id = o.Id,
                        Libelle = o.NumeroOF + " · " + o.CommandeNumero,
                        Statut = null,
                        Date = o.DateCreation,
                        Montant = null,
                        Devise = null,
                        Tiers = canWrite ? o.Chaine : null
                    }).ToList();

                case TypeContexte.TacheProduction:
                    var taches = await _db.TachesProduction
                        .AsNoTracking()
                        .Where(t => ids.Contains(t.Id))
                        .Select(t => new
                        {
                            t.Id,
                            t.Titre,
                            t.Statut,
                            t.DateCreation,
                            CommandeNumero = t.CommandeClient != null ? t.CommandeClient.NumeroCommande : null
                        })
                        .ToListAsync(cancellationToken);

                    return taches.Select(t => new ReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(TypeContexte.TacheProduction),
                        Reference = "TAC-" + t.Id,
                        Id = t.Id,
                        Libelle = string.IsNullOrWhiteSpace(t.CommandeNumero)
                            ? t.Titre
                            : t.Titre + " · " + t.CommandeNumero,
                        Statut = t.Statut.ToString(),
                        Date = t.DateCreation,
                        Montant = null,
                        Devise = null,
                        Tiers = null
                    }).ToList();

                case TypeContexte.Achat:
                    var achats = await _db.Achats
                        .AsNoTracking()
                        .Where(a => ids.Contains(a.Id))
                        .Select(a => new
                        {
                            a.Id,
                            a.NumeroAchat,
                            a.Statut,
                            a.DateAchat,
                            a.MontantTotal,
                            a.Devise,
                            Fournisseur = a.Fournisseur != null ? a.Fournisseur.NomEntreprise : null
                        })
                        .ToListAsync(cancellationToken);

                    return achats.Select(a => new ReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(TypeContexte.Achat),
                        Reference = "ACH-" + a.Id,
                        Id = a.Id,
                        Libelle = a.NumeroAchat,
                        Statut = a.Statut.ToString(),
                        Date = a.DateAchat,
                        Montant = canWrite ? a.MontantTotal : null,
                        Devise = canWrite ? a.Devise : null,
                        Tiers = canWrite ? a.Fournisseur : null
                    }).ToList();

                default:
                    return new List<ReferenceDto>();
            }
        }

        public async Task<List<ResultatReferenceDto>> RechercherAsync(
            string recherche, string lecteurUserId, CancellationToken cancellationToken = default)
        {
            var brut = (recherche ?? string.Empty).Trim().TrimStart('#').Trim();
            if (brut.Length == 0) return new List<ResultatReferenceDto>();

            // Un préfixe explicite restreint la recherche à un seul type.
            TypeContexte? typeImpose = null;
            var terme = brut;
            var separateur = brut.IndexOfAny(new[] { '-', ' ' });
            if (separateur >= 0 && ContexteDiscussion.TryDepuisPrefixe(brut[..separateur], out var typePrefixe))
            {
                typeImpose = typePrefixe;
                terme = brut[(separateur + 1)..].Trim();
            }

            var resultats = new List<ResultatReferenceDto>();
            foreach (TypeContexte type in Enum.GetValues<TypeContexte>())
            {
                if (typeImpose is { } impose && impose != type) continue;
                if (resultats.Count >= MaxTotalRecherche) break;

                var module = ContexteDiscussion.Module(type);
                var (canAccess, _) = await _permissions.GetPermissionAsync(lecteurUserId, module);
                if (!canAccess) continue;

                resultats.AddRange(await ChercherParTypeAsync(type, terme, MaxTotalRecherche - resultats.Count, cancellationToken));
            }

            return resultats;
        }

        private async Task<List<ResultatReferenceDto>> ChercherParTypeAsync(
            TypeContexte type, string terme, int limite, CancellationToken cancellationToken)
        {
            // « CMD-12 » ou « 12 » : on cherche le début de l'identifiant technique.
            var idExact = int.TryParse(terme, out var id) ? id : (int?)null;
            var motif = terme.ToLowerInvariant();

            switch (type)
            {
                case TypeContexte.Commande:
                    var commandes = await _db.CommandesClients.AsNoTracking()
                        .Where(c => idExact == null
                            ? c.NumeroCommande.ToLower().Contains(motif)
                            : c.Id == idExact.Value || c.Id.ToString().StartsWith(terme))
                        .OrderByDescending(c => c.Id)
                        .Take(limite)
                        .Select(c => new { c.Id, c.NumeroCommande, c.TitreCommande })
                        .ToListAsync(cancellationToken);
                    return commandes.Select(c => new ResultatReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(type),
                        Reference = "CMD-" + c.Id,
                        Id = c.Id,
                        Libelle = string.IsNullOrWhiteSpace(c.TitreCommande)
                            ? c.NumeroCommande
                            : c.NumeroCommande + " — " + c.TitreCommande
                    }).ToList();

                case TypeContexte.OrdreFabrication:
                    var ordres = await _db.OrdresFabrication.AsNoTracking()
                        .Where(o => idExact == null
                            ? o.NumeroOF.ToLower().Contains(motif)
                            : o.Id == idExact.Value || o.Id.ToString().StartsWith(terme))
                        .OrderByDescending(o => o.Id)
                        .Take(limite)
                        .Select(o => new { o.Id, o.NumeroOF, o.Commande.NumeroCommande })
                        .ToListAsync(cancellationToken);
                    return ordres.Select(o => new ResultatReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(type),
                        Reference = "OF-" + o.Id,
                        Id = o.Id,
                        Libelle = o.NumeroOF + " · " + o.NumeroCommande
                    }).ToList();

                case TypeContexte.TacheProduction:
                    var taches = await _db.TachesProduction.AsNoTracking()
                        .Where(t => idExact == null
                            ? t.Titre.ToLower().Contains(motif)
                            : t.Id == idExact.Value || t.Id.ToString().StartsWith(terme))
                        .OrderByDescending(t => t.Id)
                        .Take(limite)
                        .Select(t => new { t.Id, t.Titre })
                        .ToListAsync(cancellationToken);
                    return taches.Select(t => new ResultatReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(type),
                        Reference = "TAC-" + t.Id,
                        Id = t.Id,
                        Libelle = t.Titre
                    }).ToList();

                case TypeContexte.Achat:
                    var achats = await _db.Achats.AsNoTracking()
                        .Where(a => idExact == null
                            ? a.NumeroAchat.ToLower().Contains(motif)
                            : a.Id == idExact.Value || a.Id.ToString().StartsWith(terme))
                        .OrderByDescending(a => a.Id)
                        .Take(limite)
                        .Select(a => new { a.Id, a.NumeroAchat })
                        .ToListAsync(cancellationToken);
                    return achats.Select(a => new ResultatReferenceDto
                    {
                        Type = ContexteDiscussion.Stockage(type),
                        Reference = "ACH-" + a.Id,
                        Id = a.Id,
                        Libelle = a.NumeroAchat
                    }).ToList();

                default:
                    return new List<ResultatReferenceDto>();
            }
        }

        public async Task<string?> LibelleObjetAsync(
            TypeContexte type, int id, CancellationToken cancellationToken = default)
        {
            switch (type)
            {
                case TypeContexte.Commande:
                    return await _db.CommandesClients.AsNoTracking()
                        .Where(c => c.Id == id)
                        .Select(c => "Commande " + c.NumeroCommande)
                        .FirstOrDefaultAsync(cancellationToken);

                case TypeContexte.OrdreFabrication:
                    return await _db.OrdresFabrication.AsNoTracking()
                        .Where(o => o.Id == id)
                        .Select(o => "OF " + o.NumeroOF + " · " + o.Commande.NumeroCommande)
                        .FirstOrDefaultAsync(cancellationToken);

                case TypeContexte.TacheProduction:
                    return await _db.TachesProduction.AsNoTracking()
                        .Where(t => t.Id == id)
                        .Select(t => "Tâche " + t.Titre)
                        .FirstOrDefaultAsync(cancellationToken);

                case TypeContexte.Achat:
                    return await _db.Achats.AsNoTracking()
                        .Where(a => a.Id == id)
                        .Select(a => "Achat " + a.NumeroAchat)
                        .FirstOrDefaultAsync(cancellationToken);

                default:
                    return null;
            }
        }

        public async Task<List<string>> ParticipantsResponsablesAsync(
            TypeContexte type, int id, CancellationToken cancellationToken = default)
        {
            var candidats = new List<string?>();

            switch (type)
            {
                case TypeContexte.TacheProduction:
                    var tache = await _db.TachesProduction.AsNoTracking()
                        .Where(t => t.Id == id)
                        .Select(t => new { t.AssignedToUserId, t.CreatedByUserId })
                        .FirstOrDefaultAsync(cancellationToken);
                    if (tache != null)
                    {
                        candidats.Add(tache.AssignedToUserId);
                        candidats.Add(tache.CreatedByUserId);
                    }
                    break;

                case TypeContexte.Commande:
                    candidats.Add(await _db.CommandesClients.AsNoTracking()
                        .Where(c => c.Id == id).Select(c => c.CreePar).FirstOrDefaultAsync(cancellationToken));
                    break;

                case TypeContexte.OrdreFabrication:
                    candidats.Add(await _db.OrdresFabrication.AsNoTracking()
                        .Where(o => o.Id == id).Select(o => o.CreePar).FirstOrDefaultAsync(cancellationToken));
                    break;

                case TypeContexte.Achat:
                    candidats.Add(await _db.Achats.AsNoTracking()
                        .Where(a => a.Id == id).Select(a => a.CreePar).FirstOrDefaultAsync(cancellationToken));
                    break;
            }

            var userIds = candidats.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList();
            if (userIds.Count == 0) return new List<string>();

            // FK utilisateur direct OU nom d'auteur legacy (CreePar) rapproché du UserName.
            return await _db.Users.AsNoTracking()
                .Where(u => u.EstActif && u.EstCompteInterne
                    && (userIds.Contains(u.Id) || (u.UserName != null && userIds.Contains(u.UserName))))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
