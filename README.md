# API de Gestion Textile - IMS Backend

🔧 Backend d'un système de gestion d'entreprise (ERP-like) incluant la gestion des utilisateurs, tâches, stock, clients, fournisseurs et achats. 🤖 Intégration d'un assistant IA via une API dédiée (FastAPI) pour l'aide contextuelle et les requêtes en langage naturel.

## Description

Cette API REST développée en ASP.NET Core 9.0 permet la gestion complète d'une entreprise textile selon les spécifications fournies. Elle couvre 7 modules principaux :

1. **Gestion de Stock** - Contrôle précis des matières premières avec traçabilité
2. **Gestion des Commandes Clients** - Transformation des demandes en workflows industriels
3. **Gestion de Tâches** - Orchestration des activités de production
4. **Gestion Clients/Fournisseurs** - Centralisation des données partenaires
5. **Gestion des Achats** - Commandes fournisseurs liées aux besoins clients
6. **Gestion des Importations** - Enregistrement et traçage des importations
7. **Gestion des Utilisateurs et Rôles** - Contrôle des accès et responsabilités

## Technologies Utilisées

- **Framework** : ASP.NET Core 9.0
- **Base de données** : PostgreSQL
- **ORM** : Entity Framework Core 9.0
- **Authentification** : JWT Bearer Token
- **Documentation API** : Swagger/OpenAPI
- **Gestion des identités** : ASP.NET Core Identity

## Prérequis

- .NET 9.0 SDK
- PostgreSQL 12+
- Un éditeur de code (Visual Studio, VS Code, etc.)

## Installation et Configuration

### 1. Cloner le projet
```bash
git clone <repository-url>
cd Backend_Gestion_Magasin_API
```

### 2. Configuration de la base de données

Modifiez la chaîne de connexion dans `appsettings.json` :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=GestionTextileDB;Username=postgres;Password=your_password_here"
  }
}
```

### 3. Créer la base de données

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Configuration JWT

Modifiez les paramètres JWT dans `appsettings.json` :

```json
{
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyForJWTTokenGenerationThatShouldBeAtLeast32Characters",
    "Issuer": "Backend_Gestion_Magasin_API",
    "Audience": "Backend_Gestion_Magasin_API_Users",
    "ExpiryInHours": 24
  }
}
```

### 5. Lancer l'application

```bash
dotnet run
```

L'API sera accessible sur `https://localhost:5001` et la documentation Swagger sur `https://localhost:5001/swagger`

## Structure du Projet

```
Backend_Gestion_Magasin_API/
├── Controllers/           # Contrôleurs API
├── Models/               # Modèles de données
├── Data/                 # Contexte Entity Framework
├── Services/             # Services métier
├── Helpers/              # Classes utilitaires
├── Dtos/                 # Objets de transfert de données
└── Properties/           # Configuration du projet
```

## Endpoints Principaux

### Authentification
- `POST /api/Auth/register` - Inscription d'un utilisateur
- `POST /api/Auth/login` - Connexion et obtention du token JWT

### Gestion de Stock
- `GET /api/Stock` - Liste des stocks
- `GET /api/Stock/Alertes` - Stocks en alerte
- `POST /api/Stock/{id}/Valider` - Validation manuelle du stock
- `POST /api/Stock/{id}/Reserver` - Réservation de stock

### Gestion des Commandes
- `GET /api/CommandeClient` - Liste des commandes
- `POST /api/CommandeClient/{id}/ValiderRessources` - Validation des ressources
- `POST /api/CommandeClient/{id}/GenererTaches` - Génération des tâches de production

### Gestion des Tâches
- `GET /api/TacheProduction/Dashboard` - Tableau de bord des tâches
- `POST /api/TacheProduction/{id}/Commencer` - Démarrer une tâche
- `POST /api/TacheProduction/{id}/MettreAJourAvancement` - Mise à jour de l'avancement

### Gestion des Achats
- `POST /api/Achat/{id}/Soumettre` - Soumission d'un achat
- `POST /api/Achat/{id}/Confirmer` - Confirmation d'un achat
- `POST /api/Achat/{id}/Livrer` - Réception d'un achat

### Gestion des Importations
- `POST /api/Importation/{id}/Valider` - Validation d'une importation
- `POST /api/Importation/{id}/Recevoir` - Réception d'une importation
- `POST /api/Importation/{id}/AffecterCommandes` - Affectation aux commandes

### Gestion des Mouvements
- `GET /api/MouvementStock/Statistiques` - Statistiques des mouvements
- `POST /api/MouvementStock/Transfert` - Transfert entre stocks

## Workflow Principal

1. **Création d'une plateforme et clients**
2. **Ajout d'articles et constitution du stock**
3. **Création d'une commande client avec spécification des besoins**
4. **Validation des ressources** (stock importé → achats locaux → stock libre)
5. **Génération automatique des tâches de production**
6. **Suivi de l'avancement des tâches**
7. **Gestion des mouvements de stock pendant la production**

## Sécurité

- Authentification JWT obligatoire pour tous les endpoints
- Gestion des rôles et permissions par module
- Validation des données d'entrée
- Protection contre les injections SQL via Entity Framework

## Fonctionnalités Avancées

### Traçabilité Complète
- Historique de tous les mouvements de stock
- Suivi des modifications avec horodatage
- Liens entre commandes, achats, importations et stock

### Alertes et Notifications
- Alertes automatiques pour les seuils de stock
- Notifications pour les tâches en retard
- Suivi des ressources insuffisantes

### Validation en 3 Étapes
1. **Stock importé** - Priorité aux importations existantes
2. **Achats locaux** - Commandes fournisseurs confirmées
3. **Stock libre** - Affectation manuelle du stock disponible

### Dashboard et Statistiques
- Tableau de bord des tâches de production
- Statistiques des mouvements de stock
- Historique détaillé par client/fournisseur

## Support et Maintenance

Pour toute question ou problème :
1. Consultez la documentation Swagger intégrée
2. Vérifiez les logs de l'application
3. Contactez l'équipe de développement

## Licence

Ce projet est développé pour la gestion interne de l'entreprise textile.