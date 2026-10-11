---

# LOT — Emails système via l'API Gmail (remplace Resend)
**Date** : 2026-10-02
**Commits** : Backend `5b4f7e0` (Frontend inchangé)

## Résumé — envoi transactionnel par la boîte Gmail du système

| # | Élément | Gravité | Mise en œuvre | Preuve |
|---|---------|---------|---------------|--------|
| 1 | **Resend inutilisable** — pas de domaine vérifié ; **SMTP bloqué** par l'hébergement gratuit (ports 25/465/587) | Haute | Envoi par l'**API Gmail HTTPS** en réutilisant le module Courriels (`IGmailApiService`) : MIME, assainissement HTML, refresh token chiffré — aucune dépendance, aucune clé, aucun domaine | `Services/Email/GmailApiEmailSender.cs` |
| 2 | **Choix de l'émetteur** | Moyenne | `SystemEmailSender` : `Email:SenderGmailAddress` renseignée → Gmail ; absente → `LoggingEmailSender` (dev/test/recette, aucun appel réseau) | `Services/Email/SystemEmailSender.cs` |
| 3 | **Contrat d'échec** | Moyenne | Toute impossibilité (adresse absente, connexion inactive, refresh token révoqué, refus API) lève `EmailEnvoyeException`, rattrapée par `PasswordSetupLinkService` — le flux métier ne s'effondre jamais | `GmailApiEmailSender.SendAsync`, `PasswordSetupLinkService.cs:84-99` |
| 4 | **Retrait de Resend** | Moyenne | `ResendEmailSender.cs`, section `appsettings` `Resend` et `RESEND_API_KEY` supprimés dans le même lot | `git show 5b4f7e0` |
| 5 | **Refresh token révoqué** | Haute | `GmailOAuthService` désactive la connexion et journalise ; `GmailApiEmailSender` ajoute destinataire + objet et emballe en `EmailEnvoyeException` (jamais le corps) | `GmailOAuthService.cs:262-269` |
| 6 | **Résolution de l'adresse** | Basse | Correspondance **insensible à la casse** sur la connexion `IsActive` (Gmail ignore la casse) | `SystemEmailSenderTests.Adresse_systeme_resolue_sans_tenir_compte_de_la_casse` |

## Détails techniques

### Backend
- **Nouveaux** : `Services/Email/{GmailApiEmailSender,SystemEmailSender}.cs`, `backend.Tests/SystemEmailSenderTests.cs` (7 tests).
- **Modifiés** : `Program.cs` (DI : `IEmailSender → SystemEmailSender`, retrait du `HttpClient "resend"` et du choix sur `RESEND_API_KEY`), `appsettings.json` (`Email:SenderGmailAddress` ajouté, section `Resend` supprimée), `Services/Email/LoggingEmailSender.cs` (libellés), `backend.Tests/AuthApiFactory.cs` (neutralise `Email__SenderGmailAddress` au lieu de `RESEND_API_KEY`).
- **Supprimé** : `Services/Email/ResendEmailSender.cs` (116 lignes).
- **Docs** : `document_maitre_IMS.md` et `pont-chantier-ims.md` (« Intégration Resend » → « API Gmail »).

### Décisions retenues
- Variable `Email__SenderGmailAddress` (config `Email:SenderGmailAddress`).
- L'émetteur **lève** `EmailEnvoyeException` ; `PasswordSetupLinkService` la rattrape (comportement inchangé).
- **Resend supprimé dans le même lot**, une fois les tests verts.

### Écarts assumés par rapport au plan initial
- **Sélection à chaque envoi** (et non figée au démarrage) : permet de connecter l'adresse Gmail après lancement sans redéployer.
- **Adresse configurée mais aucune connexion active = échec** (`EmailEnvoyeException`), et non repli silencieux sur la journalisation. Le repli journalisé ne s'applique qu'à l'absence d'adresse.

## Vérifications
```
Backend:  dotnet build ✅ | dotnet test ✅ 197 / 197 (190 baseline + 7 SystemEmailSenderTests)
EF:       dotnet ef migrations has-pending-model-changes → « No changes »
Réseau:   tests sur double IGmailApiService — aucun appel Google
Protocole: git diff --stat → AUCUNE méthode protégée touchée
           (ValiderRessources, Calculer, LivrerAchat, RecevoirImportation, RecevoirPartiel)
```

## Points laissés ouverts
- **Test d'envoi réel** avec un vrai compte Gmail (jamais automatisé) — à faire côté utilisateur.
- Quota Gmail d'envoi (~500 messages/jour pour un compte gratuit) : suffisant pour des emails de mot de passe, documenté comme limite.
- Aucun changement de scope OAuth : `gmail.compose` couvre `messages.send`.

---

# LOT — Nettoyage des corrections rapides
**Date** : 2026-10-02
**Commits** : Backend `1a4765e`, Frontend `9d1addc`

## Résumé — 8 correctifs indépendants

| # | Élément | Gravité | Mise en œuvre | Preuve |
|---|---------|---------|---------------|--------|
| 1 | **`.env` copié dans l'image Docker** | Haute | `backend/.dockerignore` (`.env`, `.env.*`, `*.env`, `appsettings.Development.json`, `appsettings.*.local.json`, `bin/`, `obj/`, `backend.Tests/`, `.claude/`, `.codegraph/`, `.qodo/`, `.vs/`, `.vscode/`) | `docker build` ✅ + `ls /app` **sans** `.env`/`appsettings.Development` |
| 2 | **Annuaire des utilisateurs exposé sans droit** | Haute | `TacheProductionController.UtilisateursAssignables` exige `CanAssignerTachesAsync` → 403 ; `/Permission/me` expose `peutAssignerTaches` (additif) ; frontend `useCanAssignerTaches()` + requête `.enabled` | `use-taches.ts`, `TacheProductionController.cs` |
| 3 | **`GET /api/notification/me` non paginé** | Moyenne | `page`/`pageSize` (défauts 1/50, plafond 200), `Skip`/`Take` + compteurs SQL ; réponse **additive** (`notifications`, `countNonLivrees` inchangés) | `NotificationController.cs` |
| 4 | **Dérive de migration (précision devise)** | Moyenne | **Audit** : `has-pending-model-changes` → *No changes* ; `HasPrecision(18,4)` déjà configuré (`ApplicationDbContext.cs:528-530,545`), SQL `numeric(18,4)` | `dotnet ef migrations has-pending-model-changes` |
| 5 | **Devise par défaut incohérente (`EUR`)** | Moyenne | Défauts `EUR`→`TND` : 7 modèles, 4 DTOs, `Stock`/`CommandeClient`/`Facture` controllers, `PdfExportService` + 10 écrans frontend | `git diff` ciblé ; fallback taux 1 conservé |
| 6 | **Résidus de marque « IMS »** | Basse | Cookie `ims_token`→`sgt_token` ; JWT `Issuer/Audience` → `SystemeGestionTextile`/`...Users` (+ fallbacks `sgt-app`/`sgt-users`) ; prompts, commentaires, READMEs | URLs infra GitHub/Render conservées |
| 7 | **N+1 `QualiteService.CalculerSoldeAsync`** | Moyenne | Batch `CalculerSoldesAsync` branché dans `Board` **et** les indicateurs (Dashboard l'utilisait déjà) ; référence `EnvoiRetouche.ControleSource` **déjà correcte** | `QualiteBatchEquivalenceTests` (équivalence + coût SQL constant) |
| 8 | **Rôle « Lecteur » absent** | Haute | `SeedData.EnsureLecteurRole` (idempotent par nom) : lecture de tous les modules, aucune écriture/validation | `RoleLecteurTests` (`canWrite=false`, POST → 403) |

## Détails techniques

### Backend
- **Nouveaux** : `.dockerignore`, `backend.Tests/QualiteBatchEquivalenceTests.cs`, `backend.Tests/RoleLecteurTests.cs`.
- **Modifiés** : `Controllers/{TacheProduction,Permission,Notification,Qualite,Stock,CommandeClient,Facture}Controller.cs`, `Data/SeedData.cs`, 7 `Models/*`, 4 `Dtos/*`, `Services/{PdfExportService,ChatbotAgentService,ToolExecutor,NotificationService,ICurrentUserService,IPermissionService}.cs`, `Program.cs`, `appsettings.json`, `README.md`, fabriques de test (`AuthApiFactory`, `TacheApiFactory`).

### Frontend
- **Modifiés** : `types/permission.ts` (+`peutAssignerTaches?`), `hooks/use-permissions.ts` (`useCanAssignerTaches`), `hooks/use-taches.ts` (requête assignables gatée), `lib/auth.ts` (cookie `sgt_token`), 10 écrans (défauts devise `TND`), textes/commentaires (`courriels`, `gmail.ts`, `validations/tache.ts`) et 3 specs e2e (cookie), `README.md`. `lib/format-devise.ts` **conserve** la branche EUR (affichage).

### Décisions retenues
- Point 5 : **base = TND** pour tous les défauts de devise.
- Point 6 : **cookie + JWT + textes** — les jetons déjà émis sont invalidés → **reconnexion forcée** au déploiement.
- Point 7 : seule la source de N+1 ciblée (`CalculerSoldeAsync`) est supprimée ; la référence `ControleQualite`→`ControleSource` était **déjà** `ControleSource`.

## Vérifications
```
Backend:  dotnet test  ✅ 190 / 190 (188 baseline + 2 : QualiteBatchEquivalence, RoleLecteur)
Frontend: npm run typecheck ✅ | npx eslint src ✅ | npm run build ✅
Docker:   docker build ✅ — image sans .env / appsettings.Development (point 1)
EF:       dotnet ef migrations has-pending-model-changes → « No changes » (point 4)
Protocole: git diff --stat → AUCUNE méthode protégée touchée
           (ValiderRessources, Calculer, LivrerAchat, RecevoirImportation, RecevoirPartiel)
```

## Points laissés ouverts
- `Board` : le N+1 `CalculerSoldeAsync` est supprimé, mais des requêtes **par triplet** subsistent (nom/sous-traitance de chaîne, comptes contrôles/envois) — pré-existant, hors périmètre du point 7.
- Renommage JWT : rupture de sessions en production au déploiement.
- Specs e2e Playwright non exécutées (seuls build/typecheck/lint vérifiés).
- Devise : `TauxChangeService` conserve le fallback taux 1.

---

# LOT — Sécurité : Partage en lecture seule (« Partage sécurisé », Phase 3)
**Date** : 2026-10-01
**Commits** : Backend `0d42f75`, Frontend `be88283`

## Résumé — exposer un périmètre en lecture seule, hors application

| # | Élément | Gravité | Mise en œuvre | Preuve |
|---|---------|---------|---------------|--------|
| 1 | **Jeton de partage** — fuite via URL / logs | Haute | Lien `/partage#TOKEN` (fragment jamais envoyé au serveur) ; lecture puis `history.replaceState` ; l'API publique reçoit le token dans le **corps** (`PublicShareRequestDto`) ; base = **SHA-256** uniquement, token affiché **une seule fois** | `frontend/src/app/partage/page.tsx`, `backend/Services/Partage/ShareTokenGenerator.cs` |
| 2 | **Sur-exposition** — portée d'un lien | **CRITIQUE** | `ShareScopeResolver` : `PlateformeIds` / `ClientIds` / `CommandeIds` / `GroupeIds` calculés par type de portée ; stock libre **opt-in** ; groupes exposés seulement si **tous** les membres sont dans le périmètre | `backend/Services/Partage/ShareScopeResolver.cs` |
| 3 | **Fuite de données commerciales** | Haute | Projections partielles en SQL : **aucun** prix, montant, devise, fournisseur, note ni chemin de document ; `TypeStock`/`Statut` convertis enum→libellé en mémoire | `ShareLinkService.Charger{Stock,Commandes,Importations}Async` |
| 4 | **Réponses indiscernables** | Haute | 404 unique (token inconnu / expiré / révoqué / `MaxUses` atteint) ; en-têtes `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex, nofollow` | `PartageController.Public` |
| 5 | **Course sur `UseCount`** | Haute | `ExecuteUpdateAsync` conditionnel unique (hash + non révoqué + non expiré + `UseCount < MaxUses`) — incrément et validation atomiques | `ShareLinkService.OuvrirAsync:155-160` |
| 6 | **Droit de partager** | Haute | Capacité **transverse** `Role.PeutPartagerLiens` (option A), distincte des modules ; `CanPartagerLiensAsync` résolu depuis `Role` à chaque appel ; admin par construction | `Models/Role.cs`, `Services/PermissionService.cs` |
| 7 | **Anti-révocation silencieuse** — `PUT /roles` (remplacement complet) | **CRITIQUE** | `PeutPartagerLiens` présent dans `RoleDto` **et** `CreateRoleDto`, réaffecté dans `UpdateRole` ; test GET→PUT sans perte | `RoleController.cs:146`, `PartageShareLinkTests.Role_GetPuisPut_NePerdPasPeutPartagerLiens` |
| 8 | **Abus du point anonyme** | Moyenne | Rate limiting par **IP réelle** : `UseForwardedHeaders` (KnownProxies/KnownNetworks vidés) + politique `partage-public` (FixedWindow 30/min, `QueueLimit=0`, 429) ; le proxy Next transmet `X-Forwarded-For` | `Program.cs`, `frontend/src/app/api/proxy/[...path]/route.ts` |
| 9 | **Importations — FK résiduelles incohérentes** | Moyenne | Scoping piloté par `TypeDestination` **d'abord**, puis par la FK concordante ; FK non concordante ignorée | `ShareLinkService.ChargerImportationsAsync:343-352` |
| 10 | **Audit `[AllowAnonymous]`** | — | **5 exactement** : `AuthController.cs:128,183,220`, `GmailController.cs:137`, `PartageController.cs:117` (le nouveau) | `rg "\[AllowAnonymous\]"` |

## Détails techniques

### Backend
- **Nouveaux** : `Models/ShareLink.cs` (`ShareLink`, `ShareLinkAccess`, enums `ShareLinkSection` flags, `ShareLinkScopeType` `Plateforme=1/Marque=2/Commande=3`, `ShareLinkAccessResult`) ; `Dtos/Partage/ShareLinkDtos.cs` (liste blanche stricte de filtres) ; `Services/Partage/{ShareTokenGenerator,ShareScopeResolver,ShareLinkService}.cs` ; `Controllers/PartageController.cs` ; migration additive `20261001213812_AddPartageSecurise` (index unique `TokenHash`, index `{ScopeType,ScopeId}`, `ExpiresAtUtc`).
- **Modifiés** : `Data/ApplicationDbContext.cs` (DbSets + config EF), `Models/Role.cs` + `Dtos/RoleDto.cs` + `Controllers/RoleController.cs` + `Data/SeedData.cs` (`PeutPartagerLiens`, admin Id=1 = true), `Services/IPermissionService.cs` + `Services/PermissionService.cs` + `Controllers/PermissionController.cs` (champ additif répété sur chaque entrée de `/me`), `Program.cs` (DI, `UseForwardedHeaders` en premier, `AddRateLimiter`/`UseRateLimiter`, seuil lu via `IConfiguration` **DI**).
- **Motif clé** : le seuil du limiteur est lu via l'`IConfiguration` de la **requête** (et non `builder.Configuration`, lu avant `Build`) ; la config d'un test n'est visible qu'après construction de l'hôte — d'où une fabrique dédiée `PartageRateLimitFactory`.

### Frontend
- **Nouveaux** : `types/partage.ts`, `hooks/use-partage.ts` (CRUD + `ouvrirPartagePublic`), `components/partage/partage-dialog.tsx` (périmètre, sections, durée ≤168 h, `maxUses`, stock libre, filtres, libellé), `components/partage/bouton-partage.tsx` (gaté par `peutPartagerLiens`), `app/partage/page.tsx` (page **publique**), `app/(dashboard)/partages/page.tsx` (liste / état / révocation).
- **Modifiés** : `middleware.ts` (`/partage` public en correspondance exacte ; `/partages` reste protégé), proxy `X-Forwarded-For`, `hooks/use-permissions.ts` (`useCanPartagerLiens`), `types/permission.ts` (champ optionnel additif), `types/role.ts` + `roles/page.tsx` (case « Créer des liens de partage » + colonne, `PERM_TRANSVERSE`, garde `AssertNever`), `sidebar.tsx` (lien « Liens de partage » gaté), boutons « Partager » sur Stock / Importations / Commandes.

### Décisions de conception retenues
- Token en **fragment** + **corps** de requête (jamais en chemin/query).
- Groupe de commandes exposé **seulement** si **toutes** ses commandes sont dans le périmètre.
- `EstActif` (clients/plateformes) **non filtré** à la projection (documenté).
- Migration **strictement additive**, appliquée au démarrage (`MigrateAsync`).
- `PeutPartagerLiens` : booléen **dédié** (option A), pas dérivé d'un module.
- Stock « libre » (4 FK nulles) inclus **uniquement** si `IsStockLibreAllowed`.
- Limite assumée : `KnownProxies/KnownNetworks` vidés → un appel **direct** au backend (sans proxy) pourrait forger `X-Forwarded-For` ; le limiteur est une défense en profondeur, le contrôle d'accès restant l'empreinte du token.

## Vérifications
```
Backend:  dotnet test  ✅ 188 / 188 (172 baseline + 16 Partage : 15 ShareLink + 1 rate limit)
Frontend: npm run typecheck ✅ | npx eslint ✅ | npm run build ✅ (/partage, /partages générés)
Protocole: git diff --stat → AUCUNE méthode protégée touchée
           (ValiderRessources, Calculer, LivrerAchat, RecevoirImportation, RecevoirPartiel)
```

## Points laissés ouverts
- **Backlog inchangé (non corrigé)** : défaut d'exclusivité des FK `LigneImportation` / `LigneAchat` (`ImportationController.cs:252-268`, `:319-341`). Le scoping du partage **neutralise** déjà le symptôme par `TypeDestination` d'abord.
- Rate limiting : IP directe forgeable si le backend est joint sans passer par le proxy (voir « Limite assumée »).
- Reconduction du protocole : aucun commit/push sans `git diff --stat` préalable — respecté (`0d42f75`, `be88283`).

---

# LOT IMMÉDIAT — Sécurité critique (LOT18 §1+§2) + Nettoyage UI + Audit
**Date** : 2026-09-28  
**Commits** : Backend `098c31f`, Frontend `fb46737` (depuis LOT 17 : `6f2d3a3` / `8e80f36`)  
**Ajout** : Backend `e04140c` — build(tests) : retrait de net10.0 (`.sln` recompilable en SDK 9) · `a6e6aa2` — diagnostic + correction des 8 échecs révélés (**65/65**)

## Résumé — 2 bugs critiques de sécurité fermés + nettoyage + audit

| # | Élément | Gravité | Correction | Preuve |
|---|---------|---------|------------|--------|
| 1 | **LOT18 §1** — Permissions silencieusement révoquées via `PUT /roles` | **CRITIQUE** | Garde exhaustivité compile-time `AssertNever<Exclude<BooleanPermissionKey, ...>>` dans `roles/page.tsx:158` — toute permission `Role` oubliée dans l'UI = **erreur TS** | `npm run typecheck` ✅ |
| 2 | **LOT18 §2** — Contournement `PeutAssignerTaches` via `POST /Groupes/{id}/Appliquer` | **CRITIQUE** | Check `CanAssignerTachesAsync` dans `ResolveLigneResponsableAsync` (`TacheProductionController.cs:898-908`) + 2 tests Alice/Carol (403 sans droit / 200 avec droit) | `TacheOwnershipTests.cs:LOT18_S2_*` |
| 3 | **Phase 1.1** — 9 permissions manquantes exposées dans l'UI Rôles | Haute | `types/role.ts` (+6), `roles/page.tsx` (schema, PERM lists, defaults, mapping, colonne « Portée tâches », garde exhaustivité) | Build ✅ |
| 4 | **Phase 1.2** — `CommencerDialog` champ fantôme « Responsable assigné » retiré | Basse | `use-taches.ts` (hook sans body), `taches/page.tsx` (dialog lecture seule + message explicatif) | Endpoint `POST /{id}/Commencer` n'attend plus de body |
| 5 | **Phase 1.3** — E2E LOT9 sélecteur flaky corrigé | Basse | `e2e/lot9-taches-coupe.spec.ts:193` (`.last()` → scoped à `groupeCard`) | Build ✅ |
| 6 | **Phase 2** — Bug 32 Stock scope transition | Haute | `StockController.cs` (`GetScope`, `GetScopeFromDto`, check dans PUT) + `StockScopeTransitionTests.cs` (9 tests) | Build ✅ |
| 7 | **Phase 13** — Audit sécurité (read-only) | — | 0 `FromBody UserId`, 0 mass-assignment ownership, 2 `AllowAnonymous` légitimes (login, Gmail callback), secrets via env vars | — |
| 8 | **Phase 6 Auth** — 3 bugs identifiés & diagnostiqués | — | 1) Analyse email 500 → `GROQ_API_KEY` absente env 2) Filtre recherche → **déjà** sur From/Subject/Snippet 3) Bouton refuser IA → **déjà** met statut `Rejected` (audit trail) | — |

## Détails techniques

### LOT18 §1 — Garde exhaustivité UI Rôles
- **Fichier** : `frontend/src/app/(dashboard)/roles/page.tsx`
- **Mécanisme** : type `BooleanPermissionKey` extrait toutes les `boolean` de `Role` ; `AssertNever<Exclude<...>>` échoue à la compilation si une permission n'est pas listée dans `PERM_ECRITURE`, `PERM_LECTURE`, `PERM_PORTEE` ou `PermAdminKey`.
- **Résultat** : 9 permissions ajoutées (6 du rapport + 3 découvertes : `peutGererUtilisateurs`, `peutVoirUtilisateurs`, `peutVoirRoles`).

### LOT18 §2 — Contournement `PeutAssignerTaches` fermé
- **Fichier** : `backend/Controllers/TacheProductionController.cs:898-908`
- **Avant** : `ResolveLigneResponsableAsync` résolvait un nom vers un utilisateur et assignait la tâche **sans vérifier le droit**.
- **Après** : si l'assigné résolu ≠ utilisateur courant → `await _permissions.CanAssignerTachesAsync(userId)` → 403 si faux.
- **Tests** : `LOT18_S2_AppliquerGroupe_sans_PeutAssignerTaches_refuse_assignation_tiers` + `LOT18_S2_AppliquerGroupe_AVEC_PeutAssignerTaches_accepte_assignation_tiers`.

### Phase 1.1 — UI Rôles
- `PERM_ECRITURE` : + `peutGererUtilisateurs`, `peutGererProduction`, `peutGererQualite`
- `PERM_LECTURE` : + `peutVoirUtilisateurs`, `peutVoirRoles`, `peutVoirProduction`, `peutVoirQualite`
- `PERM_PORTEE` (nouveau) : `peutVoirToutesTaches`, `peutAssignerTaches` (droits de ressource, pas module)
- Colonne « Portée tâches » dans le tableau + compteurs dérivés des listes (plus de drift possible).

### Phase 1.2 — CommencerDialog
- **Fichiers** : `frontend/src/hooks/use-taches.ts` (hook `useCommencerTache` sans paramètre `responsable`), `frontend/src/app/(dashboard)/taches/page.tsx` (dialog affiche le responsable actuel en lecture seule).
- **Contrat backend** : `POST /api/TacheProduction/{id}/Commencer` n'attend plus de body (le responsable = `AssignedToUserId` déjà posé).

### Phase 2 — Bug 32 Stock
- **Fichier** : `backend/Controllers/StockController.cs`
- **Ajout** : `GetScope(Stock)` / `GetScopeFromDto(CreateStockDto)` + check dans `PutStock` comparant scope persisté vs demandé.
- **Tests** : `StockScopeTransitionTests.cs` (9 cas : scope identique, G→C, C→G, Client→Plateforme, scope→null OK, null→scope rejeté, aucun scope).

### Audit sécurité (Phase 13) — résultats
- `FromBody UserId` : **0 occurrence** dans les contrôleurs
- Mass-assignment ownership (`CreatedByUserId`, `AssignedToUserId`) : **absent** des DTOs écriture (cf. `CreateTacheProductionDto`, `UpdateTacheProductionDto`)
- `[AllowAnonymous]` : 2 légitimes — `AuthController.Login` + `GmailController.Callback` (state signé anti-CSRF)
- Secrets : `JWT_SECRET`, `GROQ_API_KEY`, `Google:ClientSecret` via variables d'environnement uniquement

### Phase 6 Auth — 3 bugs signalés, diagnostiqués non bloquants
1. **Analyse email → 500** : `GROQ_API_KEY` non définie dans l'environnement → `GmailAiService.IsAvailable = false` → 503 clair si configuré, 500 si exception non gérée. À corriger : définir la variable d'env.
2. **Filtre recherche emails** : backend `GmailController.cs:233-236` cherche **déjà** sur `From`, `Subject`, `Snippet` — le bug rapporté n'existe pas dans le code actuel.
3. **Bouton refuser réponse IA** : `RejectReply` met `Statut = Rejected` (pas suppression) — comportement correct pour audit trail. Si l'UX attend une disparition, c'est une décision produit, pas un bug.

### Lot 9 — Retrait de net10.0 de `backend.Tests` (commit backend `e04140c`)

| # | Élément | Gravité | Correction | Preuve |
|---|---------|---------|------------|--------|
| 9 | **`dotnet build` de la solution ÉCHOUE en SDK 9** — `backend.Tests` ne compilait pas du tout | **Bloquante** | `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` → `<TargetFramework>net9.0</TargetFramework>` (singulier) | `NETSDK1045` résorbé, `.sln` compile ✅ |

- **Fichier** : `backend/backend.Tests/backend.Tests.csproj` (1 ligne, seul fichier modifié)
- **Constat** : l'API cible `net9.0`, le projet de tests ciblait `net9.0;net10.0`. Le protocole de build passant par `mcr.microsoft.com/dotnet/sdk:9.0`, le SDK restaure le projet multi-TFM puis échoue sur net10.0 — `dotnet build` de la solution **échouait** et `dotnet test` était **impossible**, pas seulement lent :
  ```
  error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.
    [backend.Tests/backend.Tests.csproj::TargetFramework=net10.0]
  ```
- **Audit préalable** : aucune référence `net10.0` ailleurs dans `backend.Tests/**`, aucun `#if NET10_0` dans le repo, aucun workflow CI ne lance `dotnet build`/`dotnet test` (seul `keep-alive.yml` existe), aucune doc ne dépend d'un SDK 10 natif. Aucune dépendance réelle → retrait sans risque.
- **Nettoyage** : `backend.Tests/obj/` et `backend.Tests/bin/` supprimés avant rebuild (artefacts net10.0 périmés). `obj/`+`bin/` du projet API non touchés. `.gitignore` couvre déjà `bin/` et `obj/`.
- **Mesures réelles** (image `mcr.microsoft.com/dotnet/sdk:9.0`) :

  | Commande | AVANT | APRÈS |
  |---|---|---|
  | `build Backend_Gestion_Magasin_API.csproj` | 10m50 (échec) | **1m29** |
  | `build Backend_Gestion_Magasin_API.sln` | **ÉCHEC 2m05** | **0m29** |
  | `build backend.Tests/backend.Tests.csproj` | non compilable | **0m03.8** |
  | `dotnet test Backend_Gestion_Magasin_API.sln` | non exécutable | **2m18** |

- **Comparaison** : la comparaison de durée du `.sln` reste qualitative (échec → 29 s), l'état « avant » n'étant pas un build mais une erreur.

### Suite de tests — 57/65 PASS, 8 échecs PRÉEXISTANTS

`Failed: 8, Passed: 57, Skipped: 0, Total: 65`

Aucune référence « avant » n'était mesurable via `dotnet test` (le projet ne compilait pas). Établissement d'une référence à **TFM équivalent** : csproj revenu à l'original par `git stash`, build forcé sur net9.0 via `-p:TargetFrameworks=net9.0` → **57 PASS / 8 échecs, mêmes 8 noms de tests qu'après modification**. La modification ne change donc aucun test.

### Lot 10 — Diagnostic et correction des 8 échecs révélés (commit backend `a6e6aa2`)

Le fix net10.0 (`e04140c`) a rendu la suite exécutable et révélé que ces 11 tests (Bug 32 Stock + LOT18 §2) **n'avaient jamais tourné** dans l'environnement Docker `sdk:9.0` depuis leur création (`098c31f`). Diagnostic effectué sur les **vraies exceptions PostgreSQL**, pas sur hypothèse. **Aucun code applicatif modifié** — les 3 fichiers sont exclusivement des tests.

| # | Élément | Cause racine | Correction | Preuve |
|---|---------|--------------|------------|--------|
| 10 | **6 × `StockScopeTransitionTests` en HTTP 500** | Défaut de **test** : `CommandeClientId` / `GroupeCommandeId` / `ClientId = 1` en dur, lignes non seedées (base créée par les migrations seules) | Helpers `CreateCommandeClientAsync` / `CreateGroupeCommandeAsync` / `CreateClientAsync` / `CreatePlateformeAsync` dans `TacheApiFactory` | `23503` : `Key (CommandeClientId)=(1) is not present in table "CommandesClients"` |
| 11 | **2 × `LOT18_S2_AppliquerGroupe_*` en `23503` sur `Clients`** | Défaut de **test** : le contrôleur ne crée pas de `CommandeClient`, il en reçoit l'`Id` (`TacheProductionController.cs:898`) ; c'est le test qui insère avec `ClientId = 1` sans Client | Création d'un `Client` réel + sa `Plateforme` (requise par `Client.PlateformeId`) | `FK_CommandesClients_Clients_ClientId` |
| 12 | **Défaut supplémentaire A** — `ResponsableAssigne = "Carol"` non résolu | Le libellé ne matche **aucun** des 4 formats de `ResolveLigneResponsableAsync` (`TacheProductionController.cs:944-959`) → `assignee = null` → repli silencieux sur le créateur (`:911`) : **200 au lieu de 403** | Libellé porté par le `UserName` unique (`lot18-carol` / `lot18-carol-ok`) | `Expected: Forbidden / Actual: OK` |
| 13 | **Défaut supplémentaire B** — `typeStock` nombre et non chaîne | Aucun `JsonStringEnumConverter` enregistré (`Program.cs:23`) ; le frontend consomme `typeStock: number` (`src/types/stock.ts:28`) | Assertion alignée sur le comportement réel : `(int)TypeStock.Libre` | `InvalidOperationException: requires an element of type 'String', but the target element has type 'Number'` |

**Détail — pourquoi les 6 Stock échouaient au même endroit** : tous à la **création initiale**, pas au PUT. Insertion en `StockController.cs:213` (`PostStock`) et `:267` (`PutStock`) — **aucune ligne applicative en cause** : la garde de transition (`:236-241`) et l'exclusivité (`:181`, `:222`) sont correctes et n'étaient jamais atteintes. Les 3 tests qui passaient n'écrivent jamais de scope FK, donc n'atteignent jamais l'insertion : `PostStock_MultiplesScopes_Rejete` est rejeté en 400 par `ValiderScopesExclusifs` **avant** tout accès base, et les deux autres n'utilisent aucun scope.

**Piège du défaut A** : les deux tests créent un opérateur au `Nom = "C"` sur la **même base** (`IClassFixture`). Un libellé par nom aurait été ambigu — `OrderBy(u => u.Nom).FirstOrDefaultAsync()` aurait pu retenir l'autre Carol. D'où le `UserName` unique.

**Les défauts A et B étaient masqués par le 500** : une fois le premier obstacle franchi, les tests allaient plus loin et échouaient ailleurs. Les faire passer en ajustant une assertion qui « marche » aurait masqué la cause réelle — ils ont été **diagnostiqués** avant toute modification d'assertion.

**Résultat mesuré** : `Passed! - Failed: 0, Passed: 65, Skipped: 0, Total: 65` — stable sur **3 exécutions consécutives** (12 s / 12 s / 19 s).

## Vérifications
```
Backend:  dotnet build .sln        ✅ (compilait auparavant en ÉCHEC — NETSDK1045 résorbé)
          dotnet test  .sln        ✅ 65 / 65 — stable sur 3 exécutions consécutives
                                  (état précédent : 57 PASS / 8 échecs)
Frontend: npm run typecheck ✅ | npm run lint ✅ | npm run build ✅
```

## Points laissés ouverts (Lot 2 — hors périmètre immédiat)
- Phase 3 : N+1 QualiteService (code batch écrit, `QualiteController` en erreur — navigation `EnvoiRetouche.ControleSource` au lieu de `ControleQualite`)
- **`ResolveLigneResponsableAsync` reste fragile** (`TacheProductionController.cs:944-959`) : aucune normalisation de casse (comparaisons `==` sensibles à la casse en PostgreSQL), repli silencieux sur le créateur si 0 correspondance, choix **arbitraire** parmi les homonymes (`OrderBy(Nom).FirstOrDefault()`), format 4 dégradé si `Prenom` est null (`"C "` avec espace final). Libellé réellement libre côté UI (`groupes-tab.tsx:145`). **Règle métier à trancher** (refus explicite sur 0 match ? refus listant l'ambiguïté ?) avant implémentation.
- Phase 4 : Descente OF-level (nécessite décision métier `ModePilotage`: Standard / DonneurOrdreSousTraitance / SousTraitantPourTiers)
- Phase 5 : Module Production (gamme opératoire, étapes, machine, charge, rebuts)
- Phase 6 complet : Forgot/Reset/Change password, Email confirmation, Logout/revocation
- Phase 9 : Backlog technique (Lecteur, Devise, Precision drift, IMS rename, CI/CD, Machines)
- Phase 10-12 : Migrations, Tests complets, Isolation Alice/Bob/Admin
- Phase 14-16 : Décisions métier, Rapport final
---

# LOT COURRIELS — Composeur unifié (Lot B)

**Date** : 2026-10-04
**Commits** : Backend `15aa5aa` (composeur unifié) + `df4b084` (tests) — Frontend `2ede42a` (composeur + mise en page) + `50513a5` (Playwright)
**Lots précédents du module** : `b895fc3` (A1), `00a8d4c` (A2), `ea986e5` (A3), `8c53c33` (A4), `d7c8c0b` (A2b)

## Résumé — un seul composeur pour les quatre modes

| # | Élément | Mise en œuvre | Preuve |
|---|---------|---------------|--------|
| 1 | **Trois écrans de rédaction dupliqués** (nouveau, réponse, réponse à tous) aux comportements divergents | Composant unique `message-composer.tsx` ; la barre est identique partout, seul l'ensemble des actions permises change | `src/components/courriels/message-composer.tsx` |
| 2 | **Citation éditable** — l'utilisateur pouvait modifier ou supprimer le contexte d'origine | La citation s'affiche **hors** de la zone de rédaction et le serveur la reconstruit à l'envoi | `GET /api/gmail/compose/prefill`, reconstruction à l'envoi |
| 3 | **« Répondre à tous » plaçait tout le monde en destinataire** | Expéditeur seul en `To`, autres participants en `Cc`, adresse du compte connecté et doublons exclus (casse ignorée) | `Prefill_repondre_a_tous_met_les_participants_en_copie_sans_soi_meme` |
| 4 | **Mode de composition silencieusement retombe sur « Reply »** | Mode transporté en chaîne et validé explicitement (`ParseComposeMode`) : valeur inconnue **refusée**, jamais de repli | `GmailController.ParseComposeMode` |
| 5 | **« Générer » impossible sur un message neuf** | Texte facultatif (`AllowEmptyStrings`), refus déplacé dans le contrôleur : Generate exige une consigne, reformuler/traduire exigent un texte | `La_generation_sans_fil_exige_une_consigne` |
| 6 | **Trace IA confondait « proposé » et « envoyé »** | Colonnes additives **nulles** `SentBody` / `SentSubject` : le texte réellement parti est distingué du texte généré | migration `20261002205456_TraceTexteEnvoyeParPropositionIA` |
| 7 | **Mise en page** | Liste 320–340 px + détail ; sous 1024 px, deux panneaux successifs | `courriels/page.tsx`, `thread-detail.tsx` |
| 8 | **Styles du mail imposés à l'application** | Police, taille et largeurs neutralisées en `!important` (un `font-size:24px` en ligne cassait la mise en page) | test Playwright « la typographie du mail ne dicte pas celle de l'application » |

## Trois défauts trouvés pendant la validation

| # | Symptôme | Cause racine | Correction |
|---|----------|--------------|------------|
| 1 | **Tous les destinataires en copie disparus** dans « répondre à tous » | `ExtractEmailAddress` testait son expression régulière sur la chaîne **non coupée** : l'espace laissée par la découpe d'en-tête (`a@b.fr, c@d.fr`) faisait échouer la comparaison → aucune adresse extraite | Coupe préalable de la valeur avant test |
| 2 | **400 avant le contrôleur** sur « Générer » depuis un message neuf | `[Required]` rejette la chaîne vide : la validation automatique répondait avant que le contrôleur ne distingue Generate de Rewrite | `[Required(AllowEmptyStrings = true)]` |
| 3 | **Test IA lisant l'appel du test précédent** | Le double ne vidait que le journal des générations, pas celui des reformulations | `Reset()` vide les deux journaux |

## Vérifications

```
Backend:  dotnet build ✅ 0 erreur (14 avertissements préexistants, aucun nouveau)
          suite complète ✅ 284 / 284  (dont 126 Gmail, dont 28 composeur)
Frontend: npx tsc --noEmit ✅ | npx eslint src e2e ✅
          npx next build ✅ 45 / 45 pages
          npx playwright test e2e/lot-b-composeur.spec.ts ✅ 6 / 6 (build de production)
Réseau:   aucun appel Google ni Groq — hôte de test à doubles et API doublée par page.route
Protocole: git diff --stat → AUCUNE méthode protégée modifiée
           migrations additives (Up = AddColumn nullable) ; Down conserve le retrait
```

**Contournement d'environnement** : le runtime .NET 9 est absent de l'hôte et le restore dans le conteneur `sdk:9.0` échoue (`ims-nuget` vide). Les tests sont donc exécutés par `dotnet vstest` **sur les binaires déjà construits**, projet monté au même chemin absolu dans l'image SDK 9, avec PostgreSQL sur le réseau `ims-test-net` — 284 tests en ~1 min.

## Décisions retenues

- **Mode en chaîne + parsing explicite** plutôt qu'un convertisseur JSON global : le refus d'une valeur inconnue est un comportement non désiré du serveur, pas une conversion silencieuse.
- **La citation n'est jamais du texte saisi** : elle est renvoyée à part et reconstruite par le serveur, donc l'IA ne peut ni l'envoyer ni la perdre.
- **Transfert sans destinataire pré-rempli** : le destinataire d'un transfert est un choix de l'utilisateur, pas une évidence du message reçu.
- **Playwright** (retenu expliciment) plutôt qu'un autre runner : l'API est doublée par `page.route`, aucun serveur ni Gmail réel requis.
- **Typographie par propriétés arbitraires** : en Tailwind v4, `text-[inherit]` désigne une **couleur**, pas une taille — d'où `[&_*]:[font-size:inherit]!`.

## Points laissés ouverts

- `e2e/lot-courriels.spec.ts` (scénario préexistant) exige la stack réelle et la base amorcée (`e2e/README-courriels.md`) : **non exécuté**, le composeur n'a donc pas été vérifié de bout en bout contre un vrai Gmail.
- **Aucun envoi réel** vérifié (nécessite un compte Gmail connecté) : le comportement repose sur le double enregistreur.
- Limites Gmail ~500 messages/jour sur un compte gratuit, non applicable au composeur mais rappelées ici.

---

# LOTS CALCULATRICE + STOCK + COUPE

**Date** : 2026-10-04
**Commits** : Backend `db22888` (stock) + `46e0d53` (coupe C1) — Frontend `0e19dd9` (calculatrice) + `bc1d18a` (stock)
**Lot précédent** : composeur unifié (backend `15aa5aa`, `df4b084` ; frontend `2ede42a`, `50513a5`)

## Résumé

| # | Élément | Mise en œuvre | Preuve |
|---|---------|---------------|--------|
| 1 | **Calculatrice : `%` au sens Windows** | `200+10%` = 220, `200*10%` = 20, `%` seul = division par 100 ; chaque `%` se rattache au total courant | `src/lib/calculatrice.test.ts` |
| 2 | **Calculatrice : dérive des flottants** | Virgule fixe sur `bigint`, échelle `10^18`. `0.1+0.2` = 0.3 exactement ; `16960,4-(4717*0,9)` = 12715,1 là où le flottant ressort `12715.100000000002`. Seules les divisions restent approximatives (tronquées à 18 décimales, affichage arrondi à 12) | `calculatrice.test.ts`, `horsEchelle()` |
| 3 | **Calculatrice : `eval` interdit** | Analyse écrite à la main : tokenizer → shunting-yard → RPN. Expression invalide → erreur dédiée, division par zéro comprise, jamais `Infinity` ni `NaN` | `src/lib/calculatrice.ts` |
| 4 | **Calculatrice : widget** | Chargé en `ssr: false`, ne persiste rien | `calculator-loader.tsx` |
| 5 | **Stock : liste chargée en mémoire** | Toute la liste partie en JSON, ce qui cassait le cast PostgreSQL de `TypeStock` (enum stocké en texte) | `StockController` : projection après filtrage, `TypeStock = s.TypeStock.ToString()` |
| 6 | **Stock : `TypeStock` affiché en clair** | `Libre` / `Reserve` / `Importe` | `TYPE_STOCK_LABEL` |
| 7 | **Stock : filtres et pagination serveur** | Tabulations, recherche différée, filtres et pagination déplacés côté API au lieu de filtrer le client | `useGetStockListe`, `PaginationBar` |
| 8 | **Partage : filtres croisés incohérents** | `ArticleIds`, `Categorie` et `TypeStock` sont croisés : le lien ne partage que ce qui satisfait **tous** les filtres, pas seulement le premier | `ValiderFiltres` (strict), `PartageShareLinkTests` |
| 9 | **Partage : permission trop large** | Règle A : `PeutPartagerLiens` **et** `PeutGererStock`. Un rôle avec le premier seulement ne partage plus rien | `PeutGererStock` |
| 10 | **Coupe : moteur d'échelle** | Fonction pure : `reste(t,g) = reste(t-1,g) - plis(t) × occ(t,g)`, restes négatifs conservés, invariant `total = commande + surplus - manque` | `Services/Coupe/MoteurCoupe.cs` |
| 11 | **Coupe : grain de l'ordre de coupe** | Un ordre par commande, un par couple modèle/couleur. Index unique sur `(CommandeClientId, ArticleId, Couleur)`, couleur normalisée. Le `manque` n'est **pas stocké** | `Models/BesoinCoupe.cs` |
| 12 | **Coupe : la quantité commandée n'est pas la cellule « QT »** | Le moteur lit la somme des quantités par gabarit. Sur `PDR98 D649-BEIGE` et `PDR98 D649-BLEU GRISE`, la cellule « QT » saisie à la main (76, 294) ne vaut pas la somme que le classeur calcule lui-même (75, 291) | `QuantiteCommandee_LitLaSommeDesQuantites_PasLaCelluleQT` |
| 13 | **Coupe : couverture alignée sur le BOM existant** | `TypeStock.Importe` sur les mêmes quatre portées (commande, client, plateforme, groupe). Corrigé au passage : le filtre de type de stock manquait, et le métrage annoncé comptait tout le stock importé non rattaché | `BesoinCoupeService.CouvertureAsync` |

## Trois contradictions trouvées dans les données du dossier

| # | Constat | Vérification | Traitement retenu |
|---|---------|--------------|-------------------|
| 1 | **OVITA : le classeur se contredit avec lui-même** | `QT COUPE` (J4) = 4717, car le nombre de pièces de la 7ᵉ passe n'y est pas renseigné (0). L'échelle REPARTITION lui affecte pourtant 4 m de T44. En revanche `U4` = 4721 alimente la chaîne tissu : `MT UT` = 4248,9 → `J17` = 12711,5, et `U5` = 127 | **4721** est la seule valeur que le classeur utilise lui-même pour le tissu. La cellule QT COUPE est celle qui est incomplète |
| 2 | **Le test de la calculatrice affirmait l'inverse** | `calculatrice.test.ts` portait le commentaire « 12711,5, qui correspondait au total 4721 **erroné** repris du classeur » | Commentaire **corrigé**. Le test de dérive flottante est conservé (il est vrai), mais ancré sur 4721 → 12711,5 |
| 3 | **Deux cellules « QT » contredisent leur propre somme** | `PDR98 D649-BEIGE` : QT 76 vs somme 75. `PDR98 D649-BLEU GRISE` : QT 294 vs somme 291 | Le moteur lit la somme. L'écart est figé par un test pour rester visible plutôt que corrigé en silence |

## Deux limites assumées, figées par les tests

- **`surplus` et `manque` sont globaux**, pas calculés gabarit par gabarit. Un surplus global masque donc un gabarit non couvert, et un manque global peut coexister avec un gabarit intact. Les restes par gabarit restent visibles dans le résultat.
- **L'ordre des passes n'a aucun effet** sur le total ni sur les restes finaux : ce sont des sommes de retraits. Seules les lignes intermédiaires de l'échelle changent. Un test qui affirmait l'inverse a été corrigé.

## Vérifications

```
Backend:  dotnet build ✅ 0 erreur
          suite complète ✅ 322 / 322  (284 avant ces lots, +38)
          AddBesoinCoupe : migration additive (1 table, 2 index, 2 FK), aucun backfill
Frontend: pnpm typecheck ✅ | pnpm lint ✅
          pnpm test ✅ 29 / 29  (dont 20 calculatrice)
          pnpm build ✅ 45 / 45 pages
Réseau:   aucun appel externe — base PostgreSQL jetable sur ims-test-net
```

**Contournement d'environnement** : le runtime .NET 9 est absent de l'hôte (seul `Microsoft.NETCore.App` est présent, pas `Microsoft.AspNetCore.App`), donc `dotnet build`, `dotnet test` **et `dotnet ef`** ne peuvent pas y démarrer. Tout passe en conteneur `sdk:9.0`, avec le projet **et** le cache NuGet montés sur leurs chemins absolus d'origine : `backend.Tests/obj/project.assets.json` contient des chemins absolus et `--no-restore` échoue en `NETSDK1064` si le conteneur ne les reproduit pas. `dotnet test` se coinçait en outre indéfiniment dans la cible VSTest après le build (aucun `testhost` dans `/proc`) : le build et `dotnet vstest` sont donc lancés séparément, ce qui supprime toute évaluation MSBuild pendant les tests.

## Décisions retenues

- **Le classeur n'est pas importé.** Les 54 feuilles ne servent que de cas de test du moteur, extraites une fois dans un JSON versionné. Un « rapport d'import » n'a donc rien à faire en production : `RapportDivergence` vit dans le projet de test.
- **`enTeteClasseur` est informatif** et n'est jamais comparé au moteur. Le rapport de divergence est le seul endroit où il apparaît ; sur les 54 feuilles, **OVITA est la seule divergence**.
- **Les tests ne lisent jamais le `.xlsx`** : un test qui lirait le classeur serait vert chez moi et rouge ailleurs, et lierait la suite à un binaire difficile à relire en diff.
- **Le `manque` n'est pas stocké** : recalculé à la lecture, il ne peut pas devenir faux. Idem pour la couverture, recalculée sur le périmètre du BOM existant pour que les deux chiffres ne se contredisent pas.
- **Migration additive uniquement**, sans backfill historique.

## Points laissés ouverts

- **L'écran d'ordre de coupe n'existe pas.** Le grain C1 et le moteur sont livrés, mais la création/édition préremplie depuis la commande, un par couple modèle/couleur, reste à faire — c'est le prochain lot.
- **27 des 54 feuilles ont des libellés de taille positionnels** (`'0','1','2','3'`) dans le classeur : ce ne sont pas des tailles de tissu. Les quantités extraites sont bonnes (la somme correspond au QT pour 25 d'entre elles), mais **le test « le moteur reproduit le classeur sur 54 feuilles » est partiellement circulaire** — seul OVITA est vérifié cellule par cellule.
- **La couverture `BesoinCoupe` n'est pas couverte par un test de base** : les tests C1 portent sur le moteur, qui est pur. Le service et la migration sont validés par la suite complète (322 tests, `MigrateAsync` réel), pas par des tests dédiés.
- La chaîne de 7ᵉ passe d'OVITA repose sur le nombre de pièces laissé vide dans le classeur : si l'opérateur le renseigne, `QT COUPE` passera à 4721 et la divergence disparaîtra. Les tests le signaleront.

---

# LOT DISCUSSION — Module de messagerie interne (PHASE E : E1 backend + E2 frontend + E3 discussions contextuelles)

**Date** : 2026-10-11
**Commits** : Backend `20ac87e` (drapeaux + comptes internes), `5390e7b` (entités), `5a0f2d6` (notifications + migration), `413615a` (endpoints E1), `b529bf2` (tests E1), `392f41f` (tests admin/modérateur sans accès aux DM/groupes privés) — Frontend `4c7b458` (drapeaux dans le formulaire de rôle), `068d967` (page, polling `/sync`, badge de non-lus), `feb6407` (apostrophes du texte d'aide Discussion, eslint)
**Commits E3** : Backend `954511e` (discussions contextuelles : service de références, endpoints `/contexte` et `/references`, notifications de réponse, `DiscussionContexteTests`) — Frontend `e8eccbb` (onglet Discussion des fiches commande/achat/OF/tâche, cartes de référence, autocomplétion, cloche, e2e `lot20`)

## Résumé — un fil de discussion interne sans SignalR

| # | Élément | Gravité | Mise en œuvre | Preuve |
|---|---------|---------|---------------|--------|
| 1 | **Messagerie interne** (DM, groupe, canal) | — | Entités `Conversation` / `ConversationMembre` / `MessageDiscussion` ; enum `TypeConversation` **stocké en texte** | `Models/Discussion/DiscussionModels.cs`, migration `20261010222413_AddDiscussion` |
| 2 | **Auteurs sans FK vers `AspNetUsers`** | Haute | `CreeParUserId` / `AuteurUserId` = `string` **sans** navigation ni FK (`UserController.Delete` est un hard delete) ; `ConversationMembre.User` garde la FK `CASCADE` | `ApplicationDbContext.cs` |
| 3 | **Cloisonnement d'accès** | Haute | Non-membre → **404** (jamais 403, pour ne pas confirmer l'existence) ; `[RequireModulePermission("discussion")]` de classe → 403 sans le drapeau | `DiscussionController.cs`, `DiscussionTests` |
| 4 | **Comptes externes** | Haute | `ApplicationUser.EstCompteInterne` (NOT NULL, défaut **true**) + `RequireCompteInterneAttribute` (vérification **en base**) : 403 sur les comptes externes — « en cas de doute, refus » | `Filters/RequireCompteInterneAttribute.cs` |
| 5 | **Modération des canaux publics** | Moyenne | `PeutGererDiscussion` (capacité transverse) n'ouvre que la création d'un **canal public** ; le droit d'accès à la page reste le module `discussion` | `PermissionService.cs` (module `discussion` ⇒ `PeutUtiliserDiscussion`), `nouvelle-conversation.tsx` |
| 6 | **Diffusion sans SignalR** | Moyenne | Séquence `message_discussion_revision_seq` + trigger `trg_discussion_message_revision` (`BEFORE INSERT OR UPDATE`) : `Revision` **monotone** ; `GET /sync?depuis=` renvoie les messages de `Revision > depuis`, **suppressions incluses** (contenu vidé) et un `curseur` = MAX(revision) ou `depuis` si vide | `Migrations/20261010222413_AddDiscussion.cs`, `DiscussionController.Sync` |
| 7 | **Anti-abus** | Moyenne | Envoi limité à **30 messages/min** → **429** | `DiscussionController` |
| 8 | **Mentions** | Basse | Notification `MentionDiscussion` posée par `NotificationService.NotifierMentionAsync` | `Services/NotificationService.cs` |
| 9 | **Anti-régression des rôles** | Haute | Les 2 drapeaux Discussion (`PeutUtiliserDiscussion`, `PeutGererDiscussion`) présents dans `Role.cs` **et** `RoleDto`/`CreateRoleDto`, réaffectés dans `RoleController.Update` : un `GET → PUT /api/roles/{id}` ne les perd pas | `RoleController.cs`, `DiscussionTests` |
| 10 | **Transport frontend unique** | Haute | Polling par `/api/proxy` (`apiClient`, `PROXY_BASE`) : **aucune** connexion directe au backend, aucun `NEXT_PUBLIC_API_URL`. Intervalle **adaptatif** : 3 s page ouverte et onglet visible, 25 s sinon, **suspendu** onglet caché (`refetchIntervalInBackground: false`) | `hooks/use-discussion.ts` |
| 11 | **Source de vérité unique** | Moyenne | `useDiscussionSync` écrit le **snapshot** conversations et fusionne les messages dans le flux ; la vue fusionne historique (`GET messages`) **+** flux, séparateur « nouveaux messages » posé sur le premier non-lu | `use-discussion.ts`, `conversation-view.tsx` |
| 12 | **Envoi optimiste** | Basse | Message à `id` temporaire **négatif** puis remplacé par la réponse ; marquage lu optimiste (`nonLus = 0`) | `use-discussion.ts`, `composer.tsx` |
| 13 | **Mise en page Discussion** | Basse | Deux panneaux (liste 320–340 px + conversation) ; sous 1024 px, liste puis détail avec bouton retour ; badge de non-lus dans la topbar | `app/(dashboard)/discussion/page.tsx`, `discussion-bell.tsx` |
| 14 | **Texte brut** | Basse | Aucun `dangerouslySetInnerHTML` ; seules les URL http(s) deviennent des liens | `conversation-view.tsx` |
| 15 | **Discussions contextuelles (E3)** | Moyenne | Conversation rattachée à un objet (`ContexteType`/`ContexteId`) ; unicité garantie par index filtré ; ouverture **idempotente** `POST /api/Discussion/contexte/{segment}/{id}` (get-or-create) | `DiscussionController.OuvrirContexte`, migration `20261010222413_AddDiscussion`, `DiscussionContexteTests` |
| 16 | **Accès par le module de l'objet** | Haute | L'accès suit le module de l'objet (`commandes`/`production`/`taches`/`achats`), **pas** l'appartenance ; participants = rédacteurs + responsables assignés ; contextuelles **exclues** de la liste globale et de `/sync` | `PermissionService.UtilisateursAvecAccesModuleAsync`, `DiscussionController` |
| 17 | **Références `#CMD-123`…** | Moyenne | Cartes calculées **côté serveur pour le lecteur** : `montant`/`devise`/`tiers` émis **seulement** avec le droit d'écriture du module ; objet inaccessible → **aucune** carte ; autocomplétion `GET /references?q=` restreinte aux objets accessibles | `Services/ReferenceDiscussionService.cs` |
| 18 | **Notifications réponse / mention** | Basse | `NotifierReponseAsync` (+ `MentionDiscussion` existant) ; la projection cloche expose `messageDiscussionId`, **jamais** la conversation → route `/discussion` | `Services/NotificationService.cs`, `NotificationController.NotificationDto` |
| 19 | **Onglet Discussion des fiches** | Moyenne | Panneau `DiscussionContextuelle` monté sur commande, achat, OF (dialogue) et tâche (dialogue) ; **403** → état dédié ; repoll 5 s (exclues de `/sync`) | `components/discussion/discussion-contextuelle.tsx`, fiches commandes/achats/tâches/OF |

## Détails techniques

### Backend (E1)
- **Nouveaux** : `Models/Discussion/DiscussionModels.cs`, `Dtos/Discussion/DiscussionDtos.cs`, `Controllers/DiscussionController.cs`, `Filters/RequireCompteInterneAttribute.cs`, `backend.Tests/DiscussionTests.cs` (16 tests).
- **Non-membre, y compris admin** : un `EstAdministrateur` **ou** un `PeutGererDiscussion` non-membre reçoit **404** sur un DM / un groupe privé d'autrui — l'appartenance prime sur tout privilège transverse ; `PeutGererDiscussion` n'ouvre que les **canaux publics**. Le contenu d'un message supprimé est vérifié **vidé en base**, pas seulement masqué par la projection API (`Admin_PeutGerer_NeLitPasLeDmDAutrui`, `Moderateur_PeutGerer_NeLitPasLeGroupePriveDAutrui`, `Message_ModificationEtSuppression_PropresMessagesSeulement`).
- **Modifiés** : `Models/Role.cs` (+`PeutUtiliserDiscussion`, +`PeutGererDiscussion`), `Models/ApplicationUser.cs` (+`EstCompteInterne`), `Models/Notification.cs`, `Data/ApplicationDbContext.cs`, `Data/SeedData.cs` (Admin `true`/`true`, Lecteur `true`/`false`), `Services/PermissionService.cs`, `Services/NotificationService.cs`, `Controllers/{Role,Permission}Controller.cs`, `backend.Tests/TacheApiFactory.cs` (`SetRoleFlagsAsync`, `SetCompteInterneAsync`).
- **Migration** : `20261010222413_AddDiscussion` — **additive** (3 tables, colonnes, séquence, trigger). Enums convertis en texte via `HasConversion<string>()` ; DTO `type` en chaîne (`"Direct" | "Groupe" | "Canal"`).
- **Routes** (JSON camelCase) : `GET/POST /api/Discussion`, `GET /api/Discussion/utilisateurs`, `GET /api/Discussion/{id}/messages?avantId=` (30/page), `POST /api/Discussion/{id}/messages` (`{contenu, reponseAId}`), `POST /api/Discussion/{id}/lire` (`{jusquaMessageId}`), `PUT/DELETE /api/Discussion/{id}/messages/{msgId}`, `POST /api/Discussion/{id}/membres`, `DELETE /api/Discussion/{id}/quitter`, `GET /api/Discussion/sync?depuis=`.

### Frontend (E2)
- **Nouveaux** : `types/discussion.ts`, `hooks/use-discussion.ts` (sync par curseur, intervalle adaptatif, écritures optimistes, présence par `useSyncExternalStore`), `lib/discussion.ts`, `components/discussion/{conversation-list,conversation-view,composer,nouvelle-conversation}.tsx`, `components/layout/discussion-bell.tsx`, `app/(dashboard)/discussion/page.tsx`, `e2e/lot18-discussion.spec.ts`.
- **Modifiés** : `types/permission.ts`, `hooks/use-permissions.ts` (`useCanUtiliserDiscussion`, `useCanGererDiscussion` → lisent `data[0].peutXxx`, **fermé** tant que non chargé), `components/layout/sidebar.tsx` (entrée `module: 'discussion'`), `components/layout/topbar.tsx` (`DiscussionBell`).

### Backend (E3) — discussions contextuelles
- **Nouveaux** : `Services/ReferenceDiscussionService.cs` (`IReferenceDiscussionService` : parsing `#(CMD|OF|TAC|ACH)-(\d+)`, résolution **par lot**, champs sensibles filtrés par le droit d'écriture, autocomplétion), `backend.Tests/DiscussionContexteTests.cs` (11 tests).
- **Modifiés** : `Models/Discussion/DiscussionModels.cs` (+`TypeConversation.Contexte`, +`TypeContexte`), `Models/Notification.cs` (+`ReponseDiscussion`), `Dtos/Discussion/DiscussionDtos.cs` (+`MessageDto.References`, `ReferenceDto`, `ResultatReferenceDto`), `Services/NotificationService.cs` (`NotifierReponseAsync`), `Services/IPermissionService.cs` + `PermissionService.cs` (`UtilisateursAvecAccesModuleAsync` : actifs **et** internes avec accès lecture module), `Controllers/DiscussionController.cs` (endpoints contexte + références, exclusions `/sync`, notifications de réponse), `Program.cs` (`AddScoped<IReferenceDiscussionService>`).
- **Routes ajoutées** : `POST /api/Discussion/contexte/{segment}/{id}` (idempotent, contrôle du module de l'objet, amorçage des participants — ouvreur inclus s'il est responsable, course gérée par `DbUpdateException`), `GET /api/Discussion/references?q=`.
- **Migration** : `20261010222413_AddDiscussion` — colonnes `ContexteType` (varchar50) / `ContexteId` (int) + index unique filtré `IX_Conversations_ContexteType_ContexteId` (`filter: "ContexteType" IS NOT NULL`). Enums en texte, **aucune** migration supplémentaire.
- **Cloisonnement** : `AjouterMembres` / `Quitter` refusent (**400**) une conversation contextuelle **avant** le contrôle d'appartenance (piloté par le module) ; `Lire` est un no-op pour un non-membre contextuel.

### Frontend (E3) — onglet Discussion des fiches
- **Nouveaux** : `components/discussion/discussion-contextuelle.tsx` (panneau réutilisable : `useAuth` + `useCanUtiliserDiscussion` + `useDiscussionContexte`, état **403** dédié), `components/discussion/carte-reference.tsx`, `e2e/lot20-discussion-contexte.spec.ts` (4 tests).
- **Modifiés** : `types/discussion.ts` (+`'Contexte'`, `TypeContexte`, `SegmentContexte`, `ReferenceDiscussion`, `ResultatReference`, `MessageDiscussion.references`), `types/notification.ts` (+`MentionDiscussion`/`ReponseDiscussion`, +`messageDiscussionId`), `hooks/use-discussion.ts` (`useDiscussionContexte`, `useReferencesAutocomplete`, `refetchInterval` sur l'historique), `hooks/use-notifications.ts` (route `/discussion`), `lib/discussion.ts`, `components/discussion/{conversation-view,composer}.tsx`, `components/layout/notification-bell.tsx` (icônes dédiées), fiches `commandes/[id]`, `achats/[id]`, `ordres-fabrication-section.tsx` (OF), `taches/page.tsx` (carte → dialogue Discussion).

### Décisions retenues
- **Polling plutôt que SignalR** : transport unique par le proxy Next, un seul point d'autorisation ; l'intervalle s'adapte à la page ouverte et à la visibilité de l'onglet.
- **Auteurs non rattachés par FK** : la suppression d'un utilisateur ne réécrit pas l'historique ; l'affichage retombe sur le nom d'auteur figé dans le message.
- **404 pour non-membre**, jamais 403 : ne pas divulguer l'existence d'une conversation.
- **`PeutGererDiscussion` transverse** : n'élargit pas l'accès à la page (module), seulement la création de canaux publics.
- **`Revision` par trigger** plutôt qu'un compteur applicatif : la monotonie est garantie au niveau base, y compris sur `UPDATE`.
- **Accès contextuel = module de l'objet** : une discussion rattachée n'a pas de « membres » au sens d'accès — on la voit si l'on a le module de l'objet. Elle est donc **hors** de la liste globale et de `/sync` (son historique est repollé tant que la fiche est ouverte).
- **Création à la demande** : la conversation n'est créée qu'à l'ouverture de l'onglet (commande/achat) ou du volet Discussion (tâche/OF) — consulter ou éditer la fiche ne génère aucune conversation vide.
- **Cartes de référence calculées pour le lecteur** : `montant`/`devise`/`tiers` ne sont **jamais** émis sans le droit d'écriture du module ; un objet hors permissions ne produit **aucune** carte (le client ne masque rien).
- **Notification sans identifiant de conversation** : la cloche ne stocke que `MessageDiscussionId`, elle route donc vers `/discussion` (pas de lien profond vers une conversation contextuelle précise).

## Vérifications
```
Backend:  dotnet build API + backend.Tests séparément ✅ 0 erreur
          DiscussionTests ✅ 16 / 16
          suite complète ✅ 354 / 354
EF:       migration 20261010222413_AddDiscussion additive (Up = création)
Frontend: pnpm run typecheck ✅
          npx eslint (fichiers Discussion) ✅
          npx playwright test e2e/lot18-discussion.spec.ts ✅ 5 / 5
E3:       dotnet build ✅ 0 erreur (16 avertissements, aucun dans les fichiers E3)
          DiscussionContexteTests ✅ 11 / 11
          filtre Discussion (E1/E2+E3) ✅ 28 / 28
Frontend: pnpm run typecheck ✅   npx eslint (fichiers E3) ✅
          npx playwright test e2e/lot20-discussion-contexte.spec.ts ✅ 4 / 4
Protocole: git diff --stat → AUCUNE méthode protégée touchée
           (ValiderRessources, Calculer, LivrerAchat, RecevoirImportation, RecevoirPartiel)
```

## Points laissés ouverts
- **Aucun SignalR** : la réception repose sur le polling (3 s page ouverte). Assumé pour l'E2 ; passage temps réel possible plus tard sans changer le modèle de données.
- **Tests Playwright à API interceptée** (`page.route` + JWT factice) : ils pilotent l'interface et le transport, pas le contrôle des droits serveur — celui-ci est couvert par `DiscussionTests`.
- **Notifications de mention** : la notification est créée côté serveur ; la cloche ne connaît que `messageDiscussionId` (pas la conversation) et route donc vers `/discussion` — vérifié de bout en bout par `lot20-discussion-contexte.spec.ts` (icône + route).
- **Reprise du lot suivant** : l'écran d'ordre de coupe (C2) fait l'objet d'un lot séparé et n'entre pas dans ce journal.

# LOT DISCUSSION — Temps réel SignalR (amélioration du polling)

**Date** : 2026-10-11
**Commits** : Backend `d820c75` (hub, tickets à usage unique, diffusions ciblées) — Frontend `05e8b5f` (client `useDiscussionHub`), `665e12e` (attente d'hydratation de session dans lot20)

## Résumé — le polling reste le repli, SignalR vient en plus

| # | Élément | Gravité | Mise en œuvre | Preuve |
|---|---------|---------|---------------|--------|
| 1 | **Aucun groupe choisi par le client** | Haute | Le hub n'expose **aucune** méthode cliente (`RejoindreConversation` n'existe pas). À `OnConnectedAsync`, le serveur déduit seul le groupe personnel `utilisateur:{id}` + un groupe `conversation:{id}` par appartenance **active** (`ConversationMembre.AQuitteLe == null`) | `Hubs/DiscussionHub.cs`, `DiscussionHubTests.Hub_sans_methode_cliente_ni_rejoindre_conversation` |
| 2 | **Ticket à usage unique (60 s)** | Haute | Jeton **opaque** aléatoire (256 bits, base64url), lié au `UserId` **et** au `SecurityStamp`, TTL **60 s**, consommé atomiquement (`TryRemove`) → réutilisation refusée | `Services/Auth/HubTicketService.cs`, `DiscussionHubTests.Ticket_usage_unique_et_liaison` / `Ticket_expire_apres_60s` |
| 3 | **Ticket émis par route protégée** | Haute | `POST /api/Discussion/hub-ticket` (JWT + module `discussion` + compte interne) ; sans session → **401**, sans le module → **403** | `DiscussionController.HubTicket`, `DiscussionHubTests.Hub_ticket_*` |
| 4 | **Ticket refusé par tout le REST** | Haute | Le ticket n'est **pas** un JWT : les routes REST n'authentifient qu'en JWT, donc le ticket (query `access_token` **ou** en-tête `Bearer`) est refusé | `DiscussionHubTests.Ticket_refuse_par_les_routes_REST_*` |
| 5 | **Aucun JWT principal dans l'URL** | Haute | Le JWT reste en cookie httpOnly côté Next ; le hub ne reçoit que le ticket opaque. Schéma d'auth dédié **`HubTicket`** (`[Authorize(AuthenticationSchemes="HubTicket")]`) qui lit `access_token` | `Authentication/HubTicketAuthenticationHandler.cs` |
| 6 | **Compte désactivé / stamp périmé** | Haute | Le handler relit le compte à **chaque** connexion : inactif, non interne, ou `SecurityStamp` divergent → refus immédiat | `HubTicketAuthenticationHandler`, `DiscussionHubTests.Handler_refuse_un_compte_desactive` / `Handler_refuse_un_security_stamp_perime` |
| 7 | **Pokes sans contenu** | Moyenne | Diffusions `DiscussionChanged` (par conversation) et `NotificationChanged` (par compte) ; le client **invalide**, l'API applique les droits du lecteur (aucune fuite) | `DiscussionController.DiffuserConversationAsync` / `DiffuserUtilisateurAsync` |
| 8 | **Opt-in par variable publique** | Moyenne | `NEXT_PUBLIC_DISCUSSION_HUB_URL` : si définie, le hook se connecte ; sinon **aucun** hub et le polling `/sync` reste seul | `hooks/use-discussion-hub.ts`, `.env.local.example` |
| 9 | **Reconnexion automatique** | Moyenne | `withAutomaticReconnect` **illimité** (délais exponentiels plafonnés à 15 s) : Render gratuit s'endort, le client retente ; `onreconnected` réinvalide → resync par l'API | `hooks/use-discussion-hub.ts` |
| 10 | **WebSockets sans négociation** | Moyenne | `skipNegotiation: true` + `HttpTransportType.WebSockets` : le ticket à usage unique n'est présenté qu'**une** fois (en LongPolling, il serait rejoué à chaque requête) | `hooks/use-discussion-hub.ts` |
| 11 | **CORS avec credentials** | Moyenne | Hub monté avec `RequireCors("AllowFrontend")` (origines fixes + `AllowCredentials`) ; pas de `RequireAuthorization()` global (qui exigerait le JWT) | `Program.cs` |

## Détails techniques

### Backend — hub et tickets
- **Nouveaux** : `Hubs/DiscussionHub.cs`, `Services/Auth/HubTicketService.cs` (`IHubTicketService`, `HubTicket`, singleton en mémoire + `TimeProvider`), `Authentication/HubTicketAuthenticationHandler.cs` (`HubTicketAuthenticationOptions`, schéma `HubTicket`), `backend.Tests/DiscussionHubTests.cs` (16 tests).
- **Modifiés** : `Controllers/DiscussionController.cs` (endpoint `hub-ticket`, injections `IHubTicketService` + `IHubContext<DiscussionHub>`, diffusions sur `Envoyer`/`Modifier`/`Supprimer`/`Creer`/`AjouterMembres`/`Quitter`, poke personnel sur mention/réponse), `Program.cs` (`AddScheme<HubTicketAuthenticationOptions, HubTicketAuthenticationHandler>`, `AddSingleton(TimeProvider.System)`, `AddSingleton<IHubTicketService>`, `MapHub<DiscussionHub>("/hubs/discussion").RequireCors("AllowFrontend")`).
- **Route ajoutée** : `POST /api/Discussion/hub-ticket` → `{ ticket, expiresInSeconds: 60 }`.
- **Sans migration** : aucune donnée persistée pour les tickets (magasin mémoire, TTL 60 s).

### Frontend — hook temps réel
- **Nouveaux** : `hooks/use-discussion-hub.ts`.
- **Modifiés** : `components/layout/discussion-bell.tsx` (`useDiscussionHub(peutUtiliser)` — la cloche porte le polling global et devient le point de connexion unique), `.env.local.example` (documentation de la variable).

### Décisions retenues
- **Le polling n'est jamais désactivé** : SignalR n'est qu'un accélérateur. Si la variable publique est absente, si le backend dort ou si la connexion tombe, `/sync` continue de tout rattraper.
- **Ticket opaque plutôt que JWT dans l'URL** : le JWT principal (cookie httpOnly) n'est jamais exposé ; le ticket ne vit que 60 s, ne sert qu'une fois et n'est compris que par le hub.
- **Poke, pas contenu** : les broadcasts ne transportent qu'un identifiant — les cartes de références (E3), calculées par lecteur, restent produites par l'API, donc un broadcast ne peut pas divulguer ce qu'une réponse HTTP refuserait.
- **WebSockets obligatoires** pour un ticket à usage unique : en LongPolling, le token d'accès est rejoué à chaque poll et serait rejeté dès la deuxième requête.

## Vérifications
```
Backend:  dotnet build ✅ 0 erreur (16 avertissements, aucun dans les fichiers du lot)
          DiscussionHubTests ✅ 16 / 16
          filtre Discussion (E1/E2/E3 + temps réel) ✅ 44 / 44
Frontend: pnpm run typecheck ✅
          npx eslint (use-discussion-hub.ts, discussion-bell.tsx) ✅
          node --test (suite unitaire) ✅ 29 / 29
          npx playwright test e2e/lot18-discussion.spec.ts ✅ 5 / 5
          npx playwright test e2e/lot20-discussion-contexte.spec.ts ✅ 4 / 4
          e2e discussion combiné ✅ 9 / 9
```

**Correctif de test (lot20)** : `e2e/lot20-discussion-contexte.spec.ts` cliquait la cloche/les fiches
juste après `page.goto('/taches')`. La résolution tardive de `useAuth()` remonte la topbar et
remettait à zéro l'état local des panneaux (cloche, dialogue Discussion), d'où des échecs de
visibilité **déterministes mais sans lien avec le temps réel** (reproduits sur le commit de base,
la variable publique absente rendant `useDiscussionHub` inerte). Ajout d'un helper
`ouvrirTaches(page)` qui attend l'hydratation de la session (nom « Meme Moi » dans le `header`)
avant toute interaction → 4 / 4.

## Points laissés ouverts
- **Magasin de tickets mono-instance** : en mémoire, à usage unique. Une montée en charge multi-instances exigerait un cache partagé (hors périmètre Render mono-instance).
- **Groupes figés à la connexion** : un membre ajouté à une conversation en cours de session reçoit un poke **personnel** (liste rafraîchie) mais n'entre dans le groupe de la conversation qu'à la reconnexion ; le polling couvre l'intervalle.
- **Conversations contextuelles** : les pokes suivent les **appartenances** (participants) ; un utilisateur qui accède par le module mais n'est pas encore participant reste servi par le repoll 5 s de la fiche.



# LOT COUPE C2 — AUDIT DE VÉRITÉ + complétion du verrou (C0)

**Date** : 2026-10-11
**Commits** : Backend `41e280d` (API ordre de coupe), `b33a2bd` (tests API C2, 15), `9aabb39` (verrou étendu à toute la feuille), `dd5924b` (tests verrou étendu, 5) — Frontend `aaefc67` (client API/hooks), `baabe19` (écran type feuille Excel), `b8386fa` + `5c8ce4b` (Playwright C2, 4), `9d56954` (add matière respecte le verrou), `e2379ba` (e2e ordre figé, 1)

## Résumé — audit de l'ordre de coupe C2, un seul écart complété

| # | Exigence | Avant | Après | Preuve |
|---|----------|-------|-------|--------|
| a | CRUD ordre / tailles / plans / matières | FAIT | FAIT | `Controllers/OrdreCoupeController.cs:128-211, 323-442, 446-522, 526-588` |
| b | Préremplissage explicite depuis les besoins de la commande | FAIT | FAIT | `POST {id}/Preremplir` `OrdreCoupeController.cs:223-316` |
| c | Surcharge manuelle jamais écrasée par un recalcul | FAIT | FAIT | remplissage **NULL seulement** `:240-301` ; restes jamais persistés `:699-701` |
| d | Verrou après première coupe | **PARTIEL** | **FAIT** | avant : seulement les plans (`:631-638`) ; après : +`OrdreVerrouilleAsync` → `409` sur en-tête, préremplissage, tailles, plans et matières (`:612-625`) |
| e | Restes par taille (moteur C1, jamais stockés) | FAIT | FAIT | `Services/Coupe/MoteurCoupe.cs:25-90` |
| f | Surplus, manque, alerte tissu (stock restant négatif) | FAIT | FAIT | `OrdreCoupeController.cs:720-724, 774-797` |
| g | Matières libres (laize, conso client/réelle, écart, retrait %, mètres reçus) | FAIT | FAIT | `OrdreCoupeController.cs:774-797` |
| h | Chaîne de production sélectionnable (internes + sous-traitants) | FAIT | FAIT | `OrdreCoupeController.cs:139-141` ; select `ordre-coupe-sheet.tsx:459-477` |
| i | Écran : route, page, composants | FAIT | FAIT | `app/(dashboard)/coupe/[commandeId]/ordre-de-coupe/page.tsx`, `components/coupe/ordre-coupe-sheet.tsx` |
| j | Tests backend C2 / Playwright | 15 / 4 | **20 / 5** | `backend.Tests/OrdreCoupeC2Tests.cs` (20 `[Fact]`), `e2e/c2-ordre-coupe.spec.ts` (5) |

## Détails techniques

### Complétion du verrou (l'écart trouvé par l'audit)
- **Avant** : seuls `ModifierPlan` / `SupprimerPlan` refusaient l'écriture après une coupe réelle. L'en-tête, les tailles, le **préremplissage** et les matières restaient modifiables via l'API alors que l'écran les désactivait.
- **Après** : `OrdreVerrouilleAsync(ordreId)` (vrai si un plan est rattaché à un matelas portant un `LotCoupe`) + `409 VerrouOrdreErreur` sur `Modifier`, `Preremplir`, `Ajouter/Modifier/SupprimerTaille`, `AjouterPlan`, `Ajouter/Modifier/SupprimerMatiere`. **Aucune migration** (pur serveur), **aucune méthode protégée** modifiée (helpers privés).
- **Frontend** : la ligne « Ajouter la matière » (`bouton + 3 champs`) désactivée quand l'ordre est figé — l'API et l'interface refusent désormais **les mêmes** écritures.

### Tests ajoutés (commit `dd5924b`, 5)
`Verrou_Refuse_Modification_De_L_Entete`, `Verrou_Refuse_Ajout_De_Taille`, `Verrou_Refuse_Modification_Et_Suppression_De_Taille`, `Verrou_Refuse_Preremplissage`, `Verrou_Refuse_Ajout_Et_Modification_De_Matiere` — tous `409` après la première coupe.

### Tests Playwright ajoutés (commit `e2379ba`, 1)
`un ordre figé après coupe bloque la saisie de chaque cellule` : libellé/quantité/occurrences, en-tête, préremplir, ajouter un plan et ajouter une matière tous `disabled`.

## Vérifications
```
Backend:  OrdreCoupeC2Tests ✅ 20 / 20 (worktree isolé à HEAD + commits C2)
          suite complète   ✅ 376 / 376 (objet isolé #IMS_TEST_PG, 1m27s)
          attributs [Fact]/[Theory] : 299 avant C2 → 330 à HEAD → 335 après
          git diff --stat avant chaque commit : AUCUNE méthode protégée touchée
Frontend: pnpm typecheck ✅   eslint (fichiers C2) ✅
          npx playwright test e2e/c2-ordre-coupe.spec.ts ✅ 5 / 5 (49s)
          total Playwright : 62 (dont 5 C2)
```

## Points laissés ouverts
- **WIP Discussions E3 finalisé** : l'ancien WIP concurrent (`Services/ReferenceDiscussionService.cs`, `Controllers/DiscussionController.cs`, `Dtos/Discussion/*`, `Models/Notification.cs`, `Services/{Notification,Permission}Service.cs`, `Program.cs`, `backend.Tests/DiscussionContexteTests.cs`) qui rompait le build est **désormais complet** : `dotnet build` ✅ 0 erreur, `DiscussionContexteTests` ✅ 11/11 (voir le LOT DISCUSSION, E3).
- **Migration en double** : aucune (une seule `20261007184526_AddOrdreCoupe`, une seule `20261004125935_AddBesoinCoupe`, toutes avec Designer).
- **Branche `feat/production-readiness-updates`** : aucun commit hors main (ancêtre), rien à pousser ni fusionner — à abandonner.

# LOT COUPE C3 — rattachement des matelas, moteur unique, exports

**Date** : 2026-10-11
**Commits** : Backend `96cbc6e` (FK `OrdreCoupeId` sur Matelas/LotCoupe), `fe4af89` (moteur unique `RapportCoupeService` + lecture `RapportCoupe`/`OrdreDeCoupe`), `c4a9926` (rattachement + exports XLSX/PDF, tests) — Frontend `d303e98` (boutons Exporter/Imprimer + dialogue Matelas), `4455112` (e2e exports + rattachement)

## Résumé — les 6 exigences C3

| # | Exigence | Mise en œuvre | Preuve |
|---|----------|---------------|--------|
| 1 | **Rattachement manuel** d'un matelas à un ordre de coupe | Endpoints `GET /api/OrdreCoupe/commande/{id}/matelas-orphelins`, `GET /api/OrdreCoupe/{id}/matelas`, `POST`/`DELETE .../matelas/{matelasId}` ; auto-rattachement dès qu'un **plan** de l'ordre référence le matelas | `OrdreCoupeController` ; `OrdreCoupeC3Tests` (rattachement) |
| 2 | **Un seul moteur** pour `RapportCoupe` et `OrdreDeCoupe` | `Services/Coupe/RapportCoupeService.cs` : une passe unique (gabarits `ConfigTailles` + passes des matelas) alimente les deux lectures et la saisie de coupe | `OrdreCoupeC3Tests.Moteur_Unique_*` (3 feuilles) |
| 3 | **Pas de ligne THERMO codée en dur** | Les exports n'émettent que les `OrdreCoupeMatieres` libres ; `THERMO` n'existe nulle part dans le code | assertion XLSX (aucune cellule « THERMO ») |
| 4 | **Exports XLSX + PDF** de l'ordre de coupe | `ExcelExportService.ExportOrdreCoupe` (ClosedXML, sans gabarit), `PdfExportService.ExportOrdreCoupe` (QuestPDF) ; routes `GET /api/OrdreCoupe/{id}/Export` et `/ExportPdf` | `OrdreCoupeC3Tests` (B1/D1/J1/L1, `%PDF`), e2e téléchargements |
| 5 | **Bouton « Imprimer / Exporter »** (permissions coupe) | `ordre-coupe-sheet.tsx` : boutons « Exporter (Excel) » / « Imprimer (PDF) » via `downloadViaProxy`, sous `PermissionGate module="coupe"` | `e2e/c2-ordre-coupe.spec.ts` (téléchargement nommé) |
| 6 | **Tests** (exports, moteur unique, interface) | 13 tests backend + 2 tests Playwright (exports, rattachement) | `backend.Tests/OrdreCoupeC3Tests.cs`, `e2e/c2-ordre-coupe.spec.ts` |

## Détails techniques

### Migration (additive, sans backfill)
- `20261011120706_AddOrdreCoupeIdSurMatelasEtLotCoupe` : colonne **nullable** `OrdreCoupeId` + FK `OnDelete SetNull` + index sur `Matelas` et `LotCoupe`. Les lignes existantes restent sans rattachement ; aucun champ d'API n'est supprimé ou renommé.

### Moteur unique
- `RapportCoupeService` (ex-`MoteurCoupe`) conserve les règles calibrées : quantité coupée réelle par taille (tous les lots de la commande, y compris sans matelas), `TotalCoupeReelle` L2 = lots rattachés à des matelas, seuils comparés non arrondis / affichés arrondis à 2 décimales (`AwayFromZero`), ordre des matelas par `DateMatelas, NumeroMatelas`.
- La saisie `POST`/`PUT` de coupe résout `OrdreCoupeId` (fourni, sinon déduit du matelas) et refuse une incohérence de commande (`400`).

### Rattachement
- Orphelin = matelas de la commande sans `OrdreCoupeId` et non référencé par un `OrdreCoupePlan`.
- Verrou après coupe (`409`) sur rattacher/détacher ; détachement refusé (`409`) si un plan de l'ordre le référence ou si des `LotCoupe` existent pour ce matelas.
- Un plan `create`/`update` rattache automatiquement le matelas.

### Exports
- XLSX : en-tête (MODELE/COL/CHAINE/OF, QT, QT COUPE, SURPLUS, MANQUE, TIS RECU), tableau par matelas (restes après chaque passe, négatifs en rouge), puis bloc **MATIERES LIBRES** (laize, conso client/réelle, retrait %, mètres reçus, stock). Aucune ligne THERMO.
- PDF : impression QuestPDF de la même feuille.

## Vérifications
```
Backend:  dotnet build ✅ 0 erreur
          suite complète ✅ 400 / 400 (objet isolé #IMS_TEST_PG, 1m50s)
          OrdreCoupeC3Tests ✅ 13 / 13  ·  filtre C2/moteur/RapportCoupe ✅ 42 / 42
          git diff --stat avant chaque commit : AUCUNE méthode protégée touchée
Frontend: pnpm run typecheck ✅   npx eslint ✅
          npx playwright test e2e/c2-ordre-coupe.spec.ts ✅ 6 / 6
```

## Points laissés ouverts
- **Migration non backfillée** : les matelas/lots historiques restent non rattachés jusqu'à rattachement manuel (ou via le prochain plan).
- **WIP concurrent préservé** : `backend/docs/final-test.md` portait une section « LOT DISCUSSION — Temps réel SignalR » non commitée (auteur différent) ; seule la présente section C3 est committée, la section SignalR reste dans le working tree.
- **`feat/production-readiness-updates`** : toujours à abandonner (ancêtre de main).
