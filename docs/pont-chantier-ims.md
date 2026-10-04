# Pont — Chantier IMS (Système de Gestion Textile)
*Document de passation, généré le 30/09/2026, à joindre à une nouvelle conversation pour continuer.*

## Contexte du projet
- ERP textile custom (.NET 9 / Next.js 15 / PostgreSQL Neon), deux dépôts : `IMS-backend`, `IMS-frontend-next`.
- Sof est product owner + lead dev. Claude (ici) sert de couche de conception/supervision : audits, décisions, prompts précis avec garde-fous. Claude Code (appelé aussi "El Professor" dans certaines sessions) fait l'implémentation réelle et renvoie des rapports avec preuves (builds, tests, git diff).
- Protocole non-négociable : investigation en lecture seule d'abord, preuves `file:line`, jamais de méthode protégée touchée sans autorisation explicite (`ValiderRessources`, `Calculer`, `LivrerAchat`, `RecevoirImportation`, `RecevoirPartiel`), `git diff --stat` systématique avant tout commit, aucun push sans feu vert explicite de Sof.

---

## ✅ Terminé et poussé

- **Coupe, Production, Qualité** (piliers du chantier initial) : considérés professionnels au 22/09/2026 — module Coupe refondu (matelas = longueur × plis + plan de coupe par matelas, Ordre de coupe = Plan de coupe unifiés en un seul écran, dashboard temps réel), Fournitures/sous-traitance, Planning (bugs réactivation chaîne / cloche scrollable / clic-marque-lu résolus), navigation consolidée.
- **LOT18 §1** — permissions silencieusement révoquées via `PUT /api/roles/{id}` : corrigé (contrat patchable backend + garde d'exhaustivité compile-time côté UI), avec test de non-régression.
- **LOT18 §2** — contournement de `PeutAssignerTaches` via `POST /GroupeTache/{id}/Appliquer` : corrigé (`CanAssignerTachesAsync` vérifié dans `ResolveLigneResponsableAsync`), tests Alice/Carol (403 sans droit / 200 avec droit).
- **Bug 32** — garde de transition de scope Stock (`PUT /api/Stock/{id}`) : corrigé, 9 tests de transition (G→C, C→G, Client→Plateforme, etc.).
- **`CommencerDialog`** — champ fantôme "Responsable assigné" retiré (le endpoint `POST /{id}/Commencer` n'attendait plus de body).
- **E2E LOT9** — sélecteur flaky (`.last()`) corrigé, scopé au bon groupeCard.
- **Retrait de `net10.0`** du `.csproj` de `backend.Tests` — le `.sln` ne compilait plus du tout en SDK 9 (`NETSDK1045`), donc `dotnet test` était **impossible** avant ce correctif, pas juste lent.
- **Diagnostic 57→65** — 8 échecs révélés par le fix net10.0, tous des défauts de **test** (FK non seedées), aucun code applicatif touché.
- **LOT19** — résolution fiable du responsable (`ResolveLigneResponsableAsync`) : refus explicite si 0 match ou homonymes ambigus (au lieu d'un repli silencieux sur le créateur ou d'un choix arbitraire), normalisation casse + accents côté C# (pas d'extension PostgreSQL `unaccent`, décision définitive). 65→71 tests.
- **Module Courriels (Gmail) — version complète** : OAuth par utilisateur (state signé anti-CSRF, refresh token chiffré AES-GCM), vue par fils de discussion (threading), HTML assaini + images `cid:` via proxy, pièces jointes (lecture + envoi, plafond 20 Mo), composeur direct, IA : suggestion de tâche avec validation humaine, génération de réponse, **reformulation + traduction FR/EN/AR du brouillon**, refus d'une réponse IA = suppression du brouillon Gmail, recherche corrigée (From/Subject/Snippet/Body, insensible à la casse), synchronisation automatique + manuelle (jusqu'à 50 messages), actions Gmail au niveau fil (lu/étoile/archive/corbeille). 71→145 tests, confirmé qu'aucun test ne touche la vraie API Google (suite rejouée avec egress coupé).
- **Bug infra découvert et corrigé pendant le lot Courriels** : `.env` jamais lu malgré `dotenv.net` en dépendance (jamais câblé) → câblé proprement avec `WithoutOverwriteExistingVars()`. Secret JWT sous le plancher HMAC-SHA256 (168 bits) → garde au démarrage ajoutée + régénéré à 352 bits en dev local (aucune session invalidée, le secret n'était jamais consommé avant la correction du `.env`).
- **Bug threading** : `IsStarred`/`HasAttachments` lus sur le dernier message du fil au lieu de l'ensemble → corrigé (`Any(...)` sur tout le fil), test `G42`.
- **Décisions Auth validées** (prompt écrit, **pas encore exécuté** — voir "En attente" ci-dessous) : API Gmail derrière `IEmailSender`, comptes admin créés via lien "choisir mon mot de passe", `SecurityStamp` vérifié dans le JWT pour invalider les sessions, pas de refresh tokens, pas de flux "confirmer mon email" séparé.

## ⏳ En attente de ta décision ou d'une action de ta part

1. **Secret JWT du `.env` racine** (celui utilisé par `docker-compose`) — à 128 bits, va maintenant bloquer le démarrage à cause du nouveau garde-fou. À régénérer avec `openssl rand -base64 32` (vérifié en **octets**, pas en caractères) — tu avais commencé à en discuter, pas encore tranché/exécuté.
2. **`.dockerignore` manquant** — `backend/.env` est recopié dans l'image Docker (secrets qui voyagent dans l'image). À corriger.
3. **Validation manuelle du module Courriels** — envoi réel + pipeline IA de bout en bout, à faire toi-même avec ton vrai compte Gmail (volontairement jamais automatisé avec un jeton de test).
4. **`final test.md`** — sections LOT19 et "Clôture Courriels" rédigées et prêtes à coller (voir message précédent de la conversation), mais les commits backend/frontend du lot Courriels n'étaient pas encore confirmés poussés au moment de la rédaction — à vérifier et compléter les hashes.

## 🔜 Prêt (prompt écrit, validé) mais pas encore lancé

- **Auth complète** (mot de passe oublié, réinitialisation, changement, comptes admin par lien d'invitation, invalidation de session via `SecurityStamp`) — prompt complet déjà rédigé dans cette conversation, décisions validées, prêt à envoyer à Claude Code.

## 📋 Backlog restant, par ordre de priorité suggéré

1. **Phase 2 / Couverture au niveau OF** — le plus gros chantier, volontairement gardé pour la fin car il touche jusqu'à 5 méthodes protégées (`ValiderRessources`, `Calculer`, `LivrerAchat`, `RecevoirImportation`, `RecevoirPartiel`). Aujourd'hui `Calculer`/`ValiderRessources` agrègent au niveau `CommandeClient` (`ConfigTaille`), jamais `OrdreFabricationTailleLigne`. **Dépendance bloquante non résolue** : comment `ModePilotage` (Standard / DonneurOrdreSousTraitance / SousTraitantPourTiers) doit influencer la répartition par OF — **décision métier à trancher avec Sof avant tout prompt correctif**, pas à deviner.
2. **Module Production — refonte** : verdict de l'audit = "prototype fonctionnel mais incomplet". Manque : gamme opératoire multi-étapes, affectation machine/poste par étape, calcul de charge/capacité réel, traçabilité rebuts/reprises en cours de production (pas seulement en aval via Qualité), dashboard temps réel granulaire (aujourd'hui = compteurs statiques). Nécessite une décision de conception (étendre `TacheProduction` vs nouvelles entités `EtapeOperation`) avant codage.
3. **N+1 sur `QualiteService.CalculerSoldeAsync`** — confirmé structurel (Q = 4 + 10·N requêtes), volume production non mesurable (pas d'accès Neon). Pas urgent tant que ça ne cause pas de lenteur perceptible.
4. **`GET /UtilisateursAssignables`** — protégé seulement par le droit de lecture Tâches, pas par `PeutAssignerTaches` → fuite d'annuaire interne. Faible gravité, correction rapide.
5. **`GET /api/Notification/me`** — aucune pagination, recharge tout toutes les 25s par utilisateur connecté. Volume non mesurable, à surveiller.
6. **Backlog technique consolidé** (aucun commencé) :
   - Rôle "Lecteur" — permissions à décider/créer.
   - Devise par défaut sur nouvelle commande + fallback taux de change — partiellement harmonisé (repli neutre à 1 si absent), pas uniformisé partout.
   - 5 items de drift de migration (dont `QuantiteRecue` precision `numeric(18,4)` vs modèle sans `[Precision]`) — migration de nettoyage dédiée à écrire.
   - Renommage IMS → "Système de Gestion Textile" (2e passe : cookie `ims_token`, issuer/audience JWT, READMEs, CI/CD) — non fait.
   - Module Machines à Coudre — scaffold existant (écran + API), registre de maintenance complet non finalisé, en attente d'un fichier Excel/JSON de référence de Sof.
7. **WhatsApp ("AI Communication Center")** — explicitement mis de côté pour l'instant. Architecture différente de Gmail (compte Meta Business au niveau entreprise, pas OAuth par utilisateur ; fenêtre de 24h ; coût par message depuis le 1er oct. 2026, 1000 messages de service gratuits/mois/numéro). Backlog, à reprendre une fois le reste stabilisé.

## 🧹 Hygiène d'environnement à surveiller
- Worktree fantôme (`backend/.claude/worktrees/...`) trouvé et supprimé une fois — vérifier qu'il n'y en a pas d'autres avant de faire confiance à un `git status` qui semble anormal.
- Conteneurs Docker de test qui s'accumulent entre les lots (`docker container/image/volume prune -f` entre deux lots, pas systématiquement fait).
- Toujours vérifier `pwd` / `git remote -v` en tout début de rapport Claude Code si un doute surgit sur l'environnement d'exécution.

## Comment reprendre
Joins ce fichier dans la nouvelle conversation et dis par quel point tu veux commencer — probablement : (1) trancher le secret `.env` racine + `.dockerignore` rapidement, (2) lancer le prompt Auth déjà prêt, ou (3) ouvrir la décision `ModePilotage` pour débloquer Phase 2/OF.
