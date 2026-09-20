-- =============================================================================
-- Migration: AddCommandeIdToMatelas (module PC — rattachement optionnel d'un
--            matelas à sa commande cliente)
-- Date: 2026-09-19 (réécrite 2026-09-20 : nullable, sans DEFAULT 0)
-- Technique: Triplet manuel (.cs/.Designer.cs/.sql). Le Designer.cs et le
--            snapshot ont été régénérés pour refléter le modèle cible complet
--            (EF Core 9, `dotnet ef migrations add` indisponible ici : seul le
--            runtime .NET 10.0.4 est installé, sans ASP.NET Core 9). Les DDL
--            sont validées contre un snapshot réel de production (Neon) rejoué
--            sur PostgreSQL 15 jetable.
-- Rétrocompatibilité: colonne ajoutée NULLABLE — PAS de NOT NULL, PAS de
--            DEFAULT 0. Backfill BEST-EFFORT : affecte la commande uniquement
--            aux matelas dont les coupes relèvent d'UNE SEULE commande
--            (COUNT(DISTINCT "CommandeId") = 1). Les matelas ambigus (coupes
--            sur plusieurs commandes) et ceux sans coupe restent à NULL, ce qui
--            les laisse valides (principe additif). FK en SET NULL : la
--            suppression d'une commande ne supprime pas ses matelas.
-- =============================================================================

START TRANSACTION;

-- 1. Colonne à rattachement optionnel (nullable — pas de DEFAULT)
ALTER TABLE "Matelas" ADD "CommandeId" integer NULL;

-- 2. Backfill best-effort : uniquement les matelas dont les coupes sont sur
--    une seule et même commande (les ambigus et les sans-coupe restent NULL)
UPDATE "Matelas" m
SET "CommandeId" = x."CommandeId"
FROM (
    SELECT lc."MatelasId" AS "MatelasId",
           MIN(lc."CommandeId") AS "CommandeId"
    FROM "LotCoupes" lc
    WHERE lc."MatelasId" IS NOT NULL
      AND lc."CommandeId" IS NOT NULL
    GROUP BY lc."MatelasId"
    HAVING COUNT(DISTINCT lc."CommandeId") = 1
) x
WHERE x."MatelasId" = m."Id";

-- 3. Index + contrainte de clé étrangère vers les commandes clients (SET NULL)
CREATE INDEX "IX_Matelas_CommandeId" ON "Matelas" ("CommandeId");

ALTER TABLE "Matelas" ADD CONSTRAINT "FK_Matelas_CommandesClients_CommandeId"
    FOREIGN KEY ("CommandeId") REFERENCES "CommandesClients" ("Id")
    ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260919000000_AddCommandeIdToMatelas', '9.0.4');

COMMIT;