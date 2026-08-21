# IMS Backend — Système de Gestion d'Atelier Textile

API REST développée en **ASP.NET Core 9** pour la gestion complète d'un atelier de confection textile : stock multi-niveaux, achats, importations, commandes clients avec calcul automatique de faisabilité (BOM), production, et un assistant IA avec accès contrôlé aux données métier.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon-336791?logo=postgresql)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-blueviolet)
![JWT](https://img.shields.io/badge/Auth-JWT-black)
![Groq](https://img.shields.io/badge/AI-Groq%20%2B%20Tool%20Use-orange)

---

## 🎯 Contexte

Ce backend pilote la gestion opérationnelle d'un atelier de confection : de la réception de matière première (achat local ou importation, parfois fournie directement par le client — modèle CMT/Façon) jusqu'à la production, en passant par le calcul automatique de faisabilité d'une commande à partir de sa nomenclature (BOM) et des tailles demandées.

Le projet a été construit et fait évoluer de façon incrémentale : chaque fonctionnalité a été développée avec une phase de vérification du code existant *avant* modification, pour éviter les régressions sur une base déjà en production.

---

## 🧱 Stack technique

| Composant | Choix |
|---|---|
| Framework | ASP.NET Core 9 (Web API) |
| ORM | Entity Framework Core 9 |
| Base de données | PostgreSQL (hébergée sur Neon) |
| Authentification | ASP.NET Core Identity + JWT Bearer |
| Documentation API | Swagger / OpenAPI |
| IA conversationnelle | Groq API (`openai/gpt-oss-120b` par défaut, modèle surchargable via `GROQ_MODEL`), function calling (tool use) |
| Déploiement | Docker → Render |

---

## 🏗️ Architecture métier — points clés

### Stock à portée hiérarchique (`TypeStock` : Libre / Réservé / Importé)

Un même article peut avoir du stock réservé à trois niveaux différents, consommés de façon additive lors du calcul de faisabilité d'une commande :

```
Stock scopé à une Commande précise
        +
Stock scopé à une Marque (partagé par toutes ses commandes)
        +
Stock scopé à une Plateforme (partagé par toutes ses marques)
        +
Stock libre (non affecté)
```

Ce modèle permet de représenter fidèlement des cas réels comme le **CMT (Cut-Make-Trim)** : un client peut fournir lui-même sa matière première pour plusieurs commandes ou plusieurs marques d'une même plateforme, sur un seul bon de réception contenant des lignes destinées à des niveaux différents.

### Calcul de faisabilité (BOM)

Deux mécanismes de calcul distincts et volontairement séparés :
- **`ValiderRessources`** : détermine si une commande passe au statut *Prête* (comparaison besoins vs. ressources scopées, tous niveaux confondus)
- **`Calculer`** : calcul détaillé par article avec marge de sécurité configurable, à partir d'une nomenclature (BOM) et d'une configuration de tailles dynamique

Les deux calculs interrogent la même source de vérité (stock physiquement reçu, pas les quantités simplement commandées) — après correction d'une incohérence où l'un des deux comptait de la matière non encore réceptionnée.

### RBAC — permissions appliquées à 3 niveaux

La restriction par rôle n'est pas qu'un affichage conditionnel côté client : elle est vérifiée à chaque couche qui accède aux données.

1. **API REST** — un `ActionFilter` (`RequireModulePermissionAttribute`) vérifie les droits d'accès/écriture par module avant l'exécution de chaque action, sur l'ensemble des controllers métier
2. **Assistant IA** — chaque outil du chatbot (lecture seule) vérifie le rôle de l'utilisateur authentifié avant d'interroger la base ; aucun accès anonyme n'est exposé
3. **Frontend** — les éléments d'interface sont conditionnés par les mêmes permissions

---

## 📦 Modules exposés

| Module | Fonctionnalités clés |
|---|---|
| **Articles / Stock** | CRUD, recherche insensible à la casse, seuils d'alerte, référence auto-générée |
| **Mouvements de stock** | Traçabilité entrée/sortie/transfert/ajustement avec statistiques |
| **Achats** | Machine à états (Brouillon → Soumis → Confirmé → Livré), lignes multi-destination |
| **Importations** | Machine à états (Brouillon → Soumise → Validée → Reçue), origine Fournisseur ou Client (CMT), lignes multi-destination |
| **Commandes clients** | Configuration de tailles dynamique, nomenclature (BOM), calcul de couverture des ressources, génération de tâches de production |
| **Documents joints** | Factures / bons de livraison attachés aux achats et importations (stockage en base) |
| **Tâches de production** | Suivi d'avancement, blocage/déblocage avec motif |
| **Partenaires** | Plateformes, Marques/Clients, Fournisseurs, avec historique d'activité |
| **Utilisateurs & Rôles** | Comptes créés exclusivement par un administrateur, matrice de permissions par module |
| **Assistant IA** | Chat avec function calling (Groq), lecture seule, respectant les permissions de l'utilisateur connecté |

---

## 🚀 Démarrage local

### Prérequis
- .NET 9 SDK
- PostgreSQL 12+ (ou une base Neon)

### Installation

```bash
git clone https://github.com/Elprofessor-git/IMS-backend.git
cd IMS-backend

# Configurer la chaîne de connexion dans appsettings.Development.json
# ou via variables d'environnement (.env)

dotnet restore
dotnet ef database update
dotnet run
```

L'API est alors disponible sur `https://localhost:5001` (ou le port configuré), avec la documentation Swagger sur `/swagger`.

### Variables d'environnement principales

| Variable | Rôle |
|---|---|
| `ConnectionStrings__DefaultConnection` | Chaîne de connexion PostgreSQL |
| `Jwt__Key` / `Jwt__Issuer` | Configuration du token JWT |
| `GROQ_API_KEY` | Clé API pour l'assistant IA |
| `AllowedOrigins` | Origines CORS autorisées (frontend) |

---

## 🔒 Sécurité

- Authentification JWT, tokens jamais exposés côté navigateur (gérés via cookie httpOnly côté frontend)
- Création de comptes réservée aux administrateurs (`POST /api/Auth/register` protégé)
- Permissions vérifiées côté serveur pour chaque action d'écriture, pas seulement côté interface
- Aucun accès anonyme aux données métier (y compris via l'assistant IA)

---

## 🗄️ Migrations

Le projet utilise Entity Framework Core Migrations. Les migrations s'appliquent automatiquement au démarrage de l'application (`Database.MigrateAsync()`), adapté au contexte de développement actuel — à revoir avant une mise en production avec données réelles (préférer une application contrôlée manuellement).

```bash
dotnet ef migrations add NomDeLaMigration
dotnet ef database update
```

---

## 📄 Licence

Projet développé dans le cadre d'un Projet de Fin d'Études (PFE) — Licence en Technologie Informatique, ISET Sfax.
