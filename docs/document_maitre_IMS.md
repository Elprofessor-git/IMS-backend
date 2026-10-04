# IMS — Document maître : état actuel + architecture cible complète

Document unique et autonome — conçu pour être joint tel quel à une nouvelle conversation et servir de base à une investigation complète, puis à une conception totale, puis à des prompts de correction clairs et cohérents.

**Principe directeur pour toute ambiguïté non tranchée ci-dessous : se référer au comportement des grands ERP textile (Divalto, Sylob, Crystal ERP, BlueKaktus, Coats Digital FastReact).**

**Objectif de l'atelier : terminer ce chantier de mise à niveau le plus tôt possible. Après cette phase, seuls les bugs de production seront traités.**

---

## PARTIE 1 — Ce qui est fait, testé, en production

| Domaine | Détail |
|---|---|
| Achats & Importations | Cycle complet, réception partielle, sur-réception, correction après validation |
| Commandes clients | Nomenclature (BOM), calcul de couverture de stock |
| Stock | Multi-scope (commande/client/plateforme/groupe), traçabilité complète |
| Facturation | Cycle complet, montants figés à l'émission, export Excel + PDF |
| Rapport de Coupe | Suivi de la coupe par commande |
| Multi-devises | TND/EUR, conversion figée en base (Phase 4 terminée) |
| Permissions | Rôles granulaires par module |
| Module Fournitures/Sous-traitance | Matelas, 3 chaînes de production (interne + sous-traitants), accessoires par taille |
| Parc Machines à Coudre | Inventaire + maintenance, complet |
| Ordre de Fabrication (Phase 1) | Structure + étiquetage, rattaché aux commandes, testé en non-régression réelle |
| Marge de sécurité dans ValiderRessources | **Déjà fait** — `ValiderRessources` applique `besoinFinal = QuantiteTotale × (1 + MargeSecuriteDefaut/100)`. La marge est persistée sur `CommandeClient.MargeSecuriteDefaut` à chaque appel de `Calculer`. |
| Correctifs sécurité/données | Garde-fou de scope stock, correction erreurs mot de passe, correction stock de groupe mal capturé (OCELIA), désynchronisation permissions/sidebar |

---

## PARTIE 2 — Le problème qui bloque la clarté du reste (priorité de correction n°1)

`ConfigTaille` (répartition par taille au niveau commande) et `OrdreFabricationTailleLigne` (répartition par taille au niveau OF) coexistent sans hiérarchie claire — deux endroits pour la même information, reliés seulement par un avertissement ignorable. C'est la cause de la confusion sur l'écran Fournitures/Matelas.

**À corriger avant tout nouveau module qui dépend de "la répartition par taille".**

---

## PARTIE 3 — Les deux modes de fonctionnement de l'atelier (à clarifier et implémenter)

### Cas 1 — Standard (production classique)
Un bon de commande fixe la quantité par taille dès le départ. `ConfigTaille` est la source de vérité, connue à l'avance. La production (découpée ou non en plusieurs OF) respecte ce total. La marge de sécurité s'applique normalement au calcul de couverture.

### Cas 2 — Sous-traitance (l'atelier produit pour un tiers, ou fait produire par un tiers)
La quantité finale par taille **n'est pas connue à l'avance**. On coupe matelas par matelas, au fur et à mesure ; chaque matelas donne une quantité réelle par taille (`LotCoupe`), et c'est **ce cumul qui devient la référence vivante** — pas `ConfigTaille` fixée en amont. Les fournitures (accessoires) sont reçues **par tranches successives** au fil de la production (`ReceptionFourniture`, déjà cumulatif). C'est exactement le sens du champ **Mode de pilotage** déjà décidé (3 valeurs : Standard / Donneur d'ordre sous-traitance / Sous-traitant pour un tiers) — ce champ doit conditionner quel comportement d'écran et de référence de calcul s'applique.

### Une même commande peut mobiliser plusieurs chaînes à la fois — déjà supporté
Une partie de la production peut se faire en interne, une autre chez un ou plusieurs sous-traitants, **simultanément, pour la même commande**. C'est déjà supporté architecturalement : `EnvoiFourniture` et `LotExport` portent chacun leur propre `ChaineProductionId` — donc une même commande (ou un même OF) peut avoir plusieurs lignes d'envoi vers des chaînes différentes en parallèle. **Aucun nouveau modèle nécessaire pour ce point** — à vérifier/confirmer en investigation que l'écran le reflète clairement.

---

## PARTIE 4 — L'Ordre de Coupe (fichier Excel de référence) : comment il se réalise dans le système

Le document Excel partagé (Modèle/Couleur, plusieurs OF sur une même feuille, répartition par matelas et par taille, chaîne assignée, tissu reçu/consommé/retrait) **n'est pas une nouvelle donnée à modéliser** — c'est la combinaison, déjà construite, de :
- `OrdreFabrication` (numéro, chaîne assignée)
- `OrdreFabricationTailleLigne` (répartition par taille de l'OF)
- `Matelas` + `LotCoupe` (répartition physique réelle par matelas, rattachée à l'OF)

Une fois la Partie 2 réconciliée, produire un "Ordre de Coupe" imprimable/exportable devient un **rapport/export** combinant ces éléments existants — pas une nouvelle architecture de données.

### 4.1 — C1 : moteur de coupe et grain de l'ordre de coupe (livré)

**Périmètre.** L'application crée un ordre de coupe pour **chaque commande**, un par couple **modèle/couleur** de la commande, prérempli depuis la commande et modifiable. Le fichier Excel n'est **pas importé** : ses 54 feuilles ne servent que de cas de test du moteur.

**Grain.** `BesoinCoupe` est au grain `(CommandeClientId, ArticleId, Couleur)`, avec un index unique sur ce triplet. La couleur est normalisée (`Trim` + majuscules) pour que deux saisies libres de la même couleur ne créent pas deux lignes. Le `manque` n'est **pas stocké** : il se recalcule à la lecture, donc il ne peut pas devenir faux.

**Règle du moteur** (`Services/Coupe/MoteurCoupe.cs`, fonction pure, sans base ni horloge) :

```
reste(0, g) = quantiteCommandee(g)
reste(t, g) = reste(t-1, g) - plis(t) × occurrences(t, g)
totalPlanifie = Σ plis(t) × Σ_g occurrences(t, g)
surplus = max(0, totalPlanifie - quantiteCommandee)
manque  = max(0, quantiteCommandee - totalPlanifie)
invariant : totalPlanifie = quantiteCommandee + surplus - manque
```

Les restes négatifs sont **conservés** : un gabarit trop coupé n'est pas ramené à zéro. L'ordre des passes n'a aucun effet sur le total ni sur les restes (ce sont des sommes de retraits) ; seules les lignes intermédiaires changent.

**Deux limites assumées de la règle**, figées par les tests :

- `surplus` et `manque` se calculent sur les **totaux**, pas gabarit par gabarit. Un surplus global peut donc masquer un gabarit non couvert, et un manque global peut coexister avec un gabarit intact. Les restes par gabarit restent visibles dans le résultat.
- La quantité commandée est la **somme des quantités par gabarit**, pas la cellule « QT » saisie à la main. Sur 2 feuilles du classeur (`PDR98 D649-BEIGE`, `PDR98 D649-BLEU GRISE`) cette cellule ne vaut pas la somme que le classeur calcule lui-même ; le moteur lit la somme.

**Le classeur se contredit sur OVITA.** `QT COUPE` (J4) vaut 4717 parce que le nombre de pièces de la 7ᵉ passe n'y est pas renseigné, alors que l'échelle REPARTITION lui affecte 4 m de T44. Le total **4721** est celui que le classeur utilise lui-même pour le tissu (`U4` → `MT UT` 4248,9 → `J17` = 12711,5), avec un surplus `U5` = 127. Le moteur retient **4721**, applique toutes les passes, sans cas particulier.

**Corpus de test.** `backend.Tests/Fixtures/ordre-coupe-2026.json` : 54 feuilles extraites une fois, versionnées, **sans dépendance au `.xlsx`**. Les tests lisent le JSON, jamais le classeur. Le bloc `enTeteClasseur` est **informatif** : il n'est jamais comparé au moteur, et le rapport de divergence (`RapportDivergence`, côté test) est le seul endroit où il apparaît. Sur les 54 feuilles, **OVITA est la seule divergence**.

**Migration.** `AddBesoinCoupe` est purement additive : une table, deux index, deux clés étrangères, aucune modification d'une table existante. Aucun backfill historique.

---

## PARTIE 5 — Architecture cible complète, module par module

### 5.1 — Hiérarchie de référence

```
CommandeClient (engagement commercial)
 │
 ├─ ModePilotage (Standard / DonneurOrdreSousTraitance / SousTraitantPourTiers)
 ├─ ConfigTaille — promesse commerciale globale (Cas 1) ou vue dérivée du cumul réel (Cas 2)
 ├─ BomLigne — nomenclature UNIQUE par commande (jamais dupliquée par OF)
 │
 └─ OrdreFabrication (1 ou plusieurs par commande)
     ├─ OrdreFabricationTailleLigne — répartition réelle, fait autorité
     ├─ Besoin matière = BomLigne × sa répartition (calculé, jamais ressaisi)
     ├─ Gamme opératoire (étapes, voir 5.3)
     │   ├─ Coupe → Matelas/LotCoupe (déjà rattaché)
     │   ├─ Production → ChaineProduction (interne ET/OU externe, en parallèle possible)
     │   │              → Fournitures/accessoires (scopées OF)
     │   ├─ Contrôle Qualité (voir 5.4)
     │   └─ Expédition
     └─ Facturation (déjà existant)
```

### 5.2 — Achats : catalogue Article ↔ Fournisseur (multi-sourcing)

**Indépendant de la réconciliation BOM/OF — peut être construit en parallèle.**

Un même article physique (ex. bobine grise) acheté chez plusieurs fournisseurs à prix/référence différents oblige aujourd'hui à dupliquer la fiche `Article`.

```
ArticleFournisseur
  Id, ArticleId (FK), FournisseurId (FK)
  ReferenceFournisseur, PrixHabituel, DelaiApprovisionnementJours, EstActif
```

Un seul `Article` canonique, plusieurs fournisseurs enregistrés dessus. Le formulaire d'achat propose le fournisseur avec prix/référence pré-remplis. Le blocage existant de `SoumettreAchat` continue de fonctionner sans changement (même `ArticleId` toujours).

### 5.3 — Module Production : gamme opératoire (upgrade de `TacheProduction`)

Chaque étape de fabrication d'un OF devient un jalon suivi individuellement (temps théorique vs réel), au lieu d'une tâche plate par commande.

```
OrdreFabricationEtape
  Id, OrdreFabricationId (FK)
  TypeEtape (enum : Coupe, Production, ControleQualite, Expedition)
  Statut (enum : NonCommence, EnCours, Bloque, Termine — vocabulaire déjà existant de TacheProduction)
  ChaineProductionId (FK nullable, pertinent pour l'étape Production — plusieurs lignes possibles si plusieurs chaînes en parallèle, cf. Partie 3)
  DateDebutPrevue, DateFinPrevue, DateDebutReelle, DateFinReelle
  TempsTheoriqueHeures, TempsReelHeures
  ResponsableAssigne, Notes
```

**Contenu du module Production, complet** : planification par étape, affectation de chaîne(s), suivi d'avancement par étape (pas juste global), historique temps théorique vs réel par étape — c'est le niveau de détail standard chez Divalto/Sylob pour ce type d'atelier. `TacheProduction` (mécanisme de statut) est réutilisé tel quel, rattaché à chaque étape d'OF plutôt qu'à la commande globale.

### 5.4 — Contrôle Qualité / Retouches (nouveau module)

```
ControleQualite
  Id, OrdreFabricationEtapeId (FK, l'étape ControleQualite de l'OF)
  DateControle, QuantiteControlee, QuantiteDefauts
  Decision (enum : Accepte, Retouche, Rebut)
  EffectuePar, Notes

ControleQualiteDefautLigne
  Id, ControleQualiteId (FK), CodeDefaut (référence codifiée), Quantite, Notes

DefautCode (table de référence)
  Id, Code, Libelle, EstActif
```

### 5.5 — Correction du dépassement de coupe — doit intégrer la marge

**Nouveau point, non encore corrigé** : aujourd'hui, `LotCoupe` (module Coupe) bloque le dépassement en comparant la quantité coupée cumulée à `ConfigTaille.Quantite` **brute**. Il faut comparer à `ConfigTaille.Quantite × (1 + MargeSecuriteDefaut/100)` — le même champ marge déjà utilisé par `ValiderRessources` (Bug 29). Correction non protégée (le contrôleur de Rapport de Coupe n'est pas une des 5 méthodes protégées), mais doit utiliser exactement le même champ, pas en recréer un autre.

### 5.6 — Coûtage par style

Rapport de lecture seule, combine ce qui existe déjà (`HistoriquePrixArticle`, `PrixFacon`, achats scopés). Aucune dépendance.

### 5.7 — Numéro de bain (traçabilité teinture)

Champ optionnel sur `LigneAchat`/`LigneImportation`. Indépendant, effort minimal.

### 5.8 — Navigation : module Coupe séparé du module Commandes

**Décision recommandée** : oui, un module **Coupe** dédié dans la navigation principale (liste des matelas et OF en cours, tous commandes confondues, Rapport de Coupe), **en plus** de rester accessible depuis l'onglet dédié dans le détail d'une commande — c'est la présentation standard chez Divalto/Sylob (la coupe est une activité d'atelier transverse, pas seulement un sous-écran d'une commande). À confirmer en investigation avant implémentation.

### 5.9 — Phase 2 : calcul de couverture au niveau OF

Faire descendre `Calculer`/`ValiderRessources` au niveau OF pour la parité complète. **Touche les méthodes protégées** — dernier chantier, cycle complet dédié, seulement une fois 5.1 à 5.5 stabilisés.

---

## PARTIE 6 — Vérification finale face aux grands ERP — extras optionnels, non bloquants

Au-delà de tout ce qui précède, deux fonctionnalités existent chez certains grands ERP mais ne sont **pas indispensables** pour terminer ce projet — à ne considérer qu'après tout le reste, si le besoin se confirme réellement en usage :
- **Gestion des échantillons** (pré-production, avant lancement en série) — utile pour valider un modèle avant la coupe finale
- **Planification de charge/capacité** (visualiser la charge de travail de chaque chaîne dans le temps) — utile pour équilibrer interne vs sous-traitance

Ces deux points sont volontairement mis à part pour ne pas retarder la clôture du chantier principal.

---

## PARTIE 7 — Ce qui ne change pas

Achats (hors 5.2), Importations, Stock, Facturation, Permissions, Multi-devises, `Matelas`/`LotCoupe`/`ChaineProduction`/`FournitureCommandeLigne`/`ReceptionFourniture`/`EnvoiFourniture`/`OrdreFabricationEtiquette` — structure saine, aucune refonte au-delà de la Partie 2 et du bug du formulaire matelas.

---

## PARTIE 8 — Dette technique et points ouverts, à ne pas perdre

| Point | Statut |
|---|---|
| Connexion qui met email/mot de passe dans l'URL | Découvert, pas investigué |
| Élévation de privilèges admin (auto-promotion) | Confirmé réel, garde-fou simple décidé, pas encore implémenté |
| Garde-fou PUT Stock admin (double scope) | À revérifier s'il couvre bien ce cas précis |
| Branche git obsolète `silly-mestorf-96fec8` | À nettoyer si abandonnée |
| Seed admin échoue sur base 100% vide (FK Role) | Découvert pendant les tests OF, hors périmètre, à garder en tête |
| Bug formulaire Matelas (page Fournitures) | Signalé cassé, probablement lié à la Partie 2 |

---

## PARTIE 9 — Séquencement recommandé pour finir le chantier

1. **Partie 2 — Réconciliation BOM/OF** + correction du bug formulaire Matelas (bloque la clarté du reste)
2. **5.5 — Dépassement de coupe vs marge** (petit, rapide, à faire dans la foulée de 1)
3. **5.8 — Décision nav Module Coupe séparé** (à confirmer, impact UI seulement)
4. **5.2 — Article↔Fournisseur** (indépendant, en parallèle possible)
5. **5.7 — Numéro de bain** + **5.6 — Coûtage par style** (indépendants, faible effort)
6. **3 — Mode de pilotage** (les 2 cas)
7. **5.3 — Gamme opératoire / Production**
8. **5.4 — Contrôle Qualité**
9. **5.9 — Phase 2** (calcul niveau OF, en dernier, le plus encadré, méthodes protégées)
10. Extras optionnels (Partie 6) — seulement après tout le reste, si le besoin se confirme

---

## PARTIE 10 — Pour la prochaine conversation

Ce document est conçu pour être joint tel quel. Étapes suivantes attendues : investigation complète de l'état réel du code face à chaque partie ci-dessus, conception détaillée validée point par point, puis prompts de correction de code clairs, efficaces et cohérents pour boucler chaque point de la Partie 9 dans l'ordre indiqué.

---

## PARTIE 11 — Incident de production (20/09) et clôture du Module Coupe frontend

### Incident : crash de production sur la migration `AddCommandeIdToMatelas`

**Cause** : `Matelas.CommandeId` avait été rendu obligatoire (`NOT NULL`, `DEFAULT 0`) sans backfill réel ni validation contre une vraie copie de production — testé uniquement sur base Docker vide. Aucune commande d'id `0` n'existe → violation de contrainte FK au démarrage → boucle de crash Render sur 2 pushes consécutifs.

**Root cause plus profonde** : le champ obligatoire supposait qu'un matelas appartient toujours à une seule commande — alors que le système supporte déjà le partage d'un matelas entre plusieurs commandes d'un même groupe (Ordre de Fabrication). Rendre le champ `NOT NULL` était incompatible avec ce cas d'usage existant.

**Correctif livré** (backend `0ac5288`, frontend `dfb54ad`, poussés) :
- `Matelas.CommandeId` → **nullable**, sans `DEFAULT 0`.
- `POST /api/Matelas` continue d'exiger une commande à la création — contrainte applicative (DTO `[Required]`), pas contrainte de base.
- Migration réécrite : backfill best-effort uniquement pour les matelas mono-commande (`HAVING COUNT(DISTINCT CommandeId)=1`), les cas ambigus restent `NULL`. FK en `SetNull`.
- **Validé sur une vraie copie de production** (dump Neon réel → Docker jetable → `MigrateAsync()` réel) — chiffre réel : 0 matelas en prod n'avait de coupe au moment de l'incident, 6 matelas historiques sans coupe restent `NULL` après backfill. C'est la méthode de validation qui avait manqué au premier passage.
- E2E Playwright vert.

**Mesures de sécurité prises** : mot de passe de connexion Neon régénéré (l'ancien avait été exposé en clair dans des logs collés dans une conversation), variable d'environnement backend mise à jour.

**Leçon retenue pour la suite** : toute migration touchant une contrainte `NOT NULL`/FK sur une table qui a déjà des lignes réelles en production doit être validée contre un **vrai dump de production** avant déploiement — pas seulement une base Docker vide fraîche. À ajouter comme étape systématique du protocole de migration sur ce projet.

### Module Coupe frontend — livré et déployé

- Onglet Tailles/BOM de la commande : lecture seule dès qu'un OF existe.
- Formulaire de coupe : sélecteur de matelas existant + création à la volée.
- Formulaire d'export : sélecteur de chaîne de production.
- Page liste globale des matelas + entrée sidebar "Coupe".
- Production stable, backend et frontend synchronisés sur `origin/main`.

**Statut Partie 9 : ①②③ + extras = ✅ terminé et en production.**

---

## PARTIE 12 — Nouvelles demandes à cadrer (20/09, en attente d'investigation/prompt)

1. **Réorganisation par modules autonomes** : chaque domaine (Coupe, Production, Facturation, etc.) doit permettre de travailler depuis son propre module, sans dépendre du détail d'une commande pour tout. Le module Commandes se recentre sur : saisie de la commande (OF, quantités), définition des besoins (fournitures/tissu), vérification de faisabilité. Les rapports/documents opérationnels (Ordre de Coupe, Rapport de Coupe, etc.) vivent dans leurs modules respectifs si c'est ce que font les grands ERP textile — sinon rester dans Commandes. **À vérifier en investigation avant de trancher le design.**
2. **Regroupement Utilisateurs / Partenaires / Rôles** sous un sous-module "Paramètres".
3. **Nouveau module Planning** — un fichier de référence réel sera fourni, à améliorer selon les pratiques des grands ERP textile une fois reçu.
4. **Intégration email** — gestion du mot de passe par chaque utilisateur (réinitialisation en libre-service) — **livré** via l'API Gmail (emails système par `IEmailSender`, réutilisation du module Courriels) ; Resend abandonné (pas de domaine vérifié, SMTP bloqué par l'hébergement).

Ces 4 points ne sont pas encore investigués — prochaine étape une fois ce document repris.

---

## PARTIE 13 — Séquence ④⑤⑥ livrée + Module Coupe séparé de Commandes (20-21/09)

### Séquence ④⑤⑥ (Article↔Fournisseur, Numéro de bain, Coûtage, Mode de pilotage) — ✅ livré, testé, committé
Backend + frontend, migration unique appliquée réellement, 6 tests empiriques passés (dont non-régression `Calculer`/`ValiderRessources` prouvée entre 2 commandes à `ModePilotage` différent). Les 5 méthodes protégées confirmées absentes du diff.

**Zones d'incertitude reportées** (non bloquantes, à garder en tête) : devises du coûtage non harmonisées entre lignes (TND/EUR mixte) ; `ReferenceFournisseur` non recopiée sur la ligne d'achat (traçabilité à améliorer plus tard) ; `PutCommandeClient` écrase toujours les champs omis sauf `ModePilotage` (protégé par `??`) — comportement préexistant, pas une régression de ce lot.

### Module Coupe séparé de Commandes — ✅ livré, committé
- Permissions dédiées `PeutVoirCoupe`/`PeutGererCoupe`, `RequireModulePermissionAttribute` accepte désormais plusieurs modules (accès si ≥1 accordé).
- Commande détail : Rapport de Coupe en **lecture seule** (formulaires masqués, exports téléchargeables).
- Toute édition de coupe se fait désormais dans le module Coupe dédié (`/coupe/{commandeId}`).
- Sidebar et permissions rôles mis à jour partout.

**Statut Partie 9 : ①②③④⑤⑥ = ✅ tous terminés et en production.**

---

## PARTIE 14 — Nouvelles demandes en attente (20/09 soir)

1. **Vérification à refaire** : Sof observe que `ValiderRessources` semble ne pas prendre en compte la marge de sécurité choisie dans la simulation `Calculer` — **alors que ce point est censé être fait depuis Bug 29** (committé de longue date). Sof relie ça aussi au contrôle de dépassement de pièces coupées dans le Rapport de Coupe (Partie 5.5/C, livré dans la séquence ①②③ mais **jamais vérifié par un vrai test empirique en interface**, seulement affirmé en code). **À investiguer en priorité** : soit une vraie régression a été introduite entre-temps, soit c'est juste jamais été testé en conditions réelles et ça fonctionne déjà — il faut trancher avec preuve, pas supposer.
2. **Module Planning** — fichier de référence toujours pas partagé. **Dépendance architecturale identifiée** : un vrai module Planning (au sens grands ERP) a besoin de granularité par étape de production (5.3, Gamme opératoire / `OrdreFabricationEtape`) — **qui n'est pas encore construit**. Construire Planning avant 5.3 donnerait un planning superficiel (juste au niveau commande/OF, pas par étape/chaîne). À trancher : construire 5.3 d'abord, ou concevoir les deux ensemble une fois le fichier Planning reçu.
3. **Notifications temps réel** — nouvelle demande : notifier automatiquement tous les utilisateurs à chaque changement de planning. **Infrastructure entièrement nouvelle**, rien d'équivalent n'existe dans le système actuel. Décisions à prendre avant conception : mécanisme de livraison temps réel (SignalR vs sondage périodique), modèle de données (entité `Notification` par utilisateur, lu/non-lu), et surtout **portée réelle de "tous les utilisateurs"** — littéralement tout le monde sans filtre, ou seulement ceux ayant un accès de lecture au module concerné (cohérent avec le système de permissions déjà en place partout ailleurs) ?
4. **Regroupement Paramètres** (Utilisateurs/Partenaires/Rôles) — toujours pas fait, uniquement Coupe a été séparé cette session.
5. **Intégration email** (mot de passe self-service) — **livré** via l'API Gmail ; Resend abandonné (pas de domaine vérifié, SMTP bloqué par l'hébergement).
