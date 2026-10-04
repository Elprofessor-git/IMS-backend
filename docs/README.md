# docs

Documents de projet versionnés dans le dépôt backend.

| Fichier | Rôle |
|---|---|
| `final-test.md` | Journal de validation lot par lot : périmètre, vérifications, défauts trouvés, décisions, points ouverts |
| `document_maitre_IMS.md` | État d'avancement consolidé du projet |
| `pont-chantier-ims.md` | Contexte de chantier, décisions en attente, consignes de la nouvelle conversation |
| `tests-backend.md` | Exécution de la suite de tests backend (voir ci-dessous) |

Ces fichiers ont été ajoutés ici parce que la racine du workspace n'est pas un dépôt
git : tant qu'ils n'y restaient pas, aucune de leurs mises à jour n'était versionnée.
Ils ne contiennent aucun secret : seules des **noms** de variables d'environnement
(`JWT_SECRET`, `GROQ_API_KEY`, `Google:ClientSecret`, …) sont cités, jamais leur valeur.

---

# Exécuter les tests backend

## Contournement d'environnement (à connaître avant tout)

Deux contraintes de cette machine :

1. **Aucun runtime .NET 9 sur l'hôte** — `dotnet` présent est le SDK 10.0.112, qui ne
   peut pas exécuter un projet `net9.0`.
2. **Le restore NuGet échoue dans le conteneur** (`ims-nuget` vide, pas de réseau NuGet).

Les tests sont donc lancés par **`dotnet vstest` sur les binaires déjà compilés**, dans
l'image SDK 9, avec PostgreSQL joignable sur le réseau `ims-test-net`.

## Prérequis

PostgreSQL de test doit tourner. Le conteneur attendu :

```bash
docker ps --filter name=ims-test-pg     # Up
docker inspect ims-test-pg --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}={{$v.IPAddress}}{{end}}'
# → ims-test-net=172.25.0.2
```

L'adresse n'est pas fixe : la lire à chaque fois, puis la reporter dans `IMS_TEST_PG`.

## Commande exacte

Le projet **doit être monté au même chemin absolu** à l'intérieur du conteneur.
`WebApplicationFactory` résout la racine de contenu depuis le chemin de compilation
inscrit dans l'assembly ; monté ailleurs, l'hôte cherche un répertoire inexistant et
230 tests sur 284 échouent sur `DirectoryNotFoundException`.

```bash
cd "/mnt/disque-local/Projects/IMS mise à jour/Projet final/backend"
P="/mnt/disque-local/Projects/IMS mise à jour/Projet final/backend"

docker run --rm \
  --network ims-test-net \
  -v "$P:$P" \
  -w "$P" \
  -e IMS_TEST_PG="Host=<IP_LU_PLUS_TOT>;Port=5432;Database=postgres;Username=postgres;Password=<MOT_DE_PASSE_PG_TEST>" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet vstest "$P/backend.Tests/bin/Debug/net9.0/backend.Tests.dll"
```

Les chemins sont entre guillemets : l'arborescence du projet contient un espace
(`IMS mise à jour`).

`IMS_TEST_PG` est lu par `backend.Tests/TacheApiFactory.cs`, avec
`localhost:5432` en valeur par défaut — c'est ce qui rend les tests d'intégration
dépendants d'un PostgreSQL réel.

## Filtrer

```bash
# un seul test
dotnet vstest "$P/backend.Tests/bin/Debug/net9.0/backend.Tests.dll" --Tests:NomDuTest

# une classe
dotnet vstest "$P/backend.Tests/bin/Debug/net9.0/backend.Tests.dll" --TestCaseFilter:"FullyQualifiedName~NomDeLaClasse"
```

## Après modification du code

Recompiler d'abord — `vstest` n'exécute que ce qui est déjà dans `bin/` :

```bash
dotnet build backend.Tests/backend.Tests.csproj
```

puis relancer la commande ci-dessus.

## Résultat attendu au 4 octobre 2026

284/284 (126 Gmail, 28 composeur, 6 Stock/Partage, le reste réparti) en ~1 min.
Aucun test n'atteint la vraie API Google ni un modèle IA : le hôte de test est à
doubles et l'API est doublée.
