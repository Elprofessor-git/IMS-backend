-- =============================================================================
-- Migration: AddCommandeIdToMatelas (module PC — rattachement d'un matelas à sa
--            commande cliente)
-- Date: 2026-09-19
-- Technique: Triplet manuel (.cs/.Designer.cs/.sql). Le Designer.cs et le
--            snapshot ont été régénérés pour refléter le modèle cible complet
--            (EF Core 9, `dotnet ef migrations add` indisponible ici : seul le
--            runtime .NET 10.0.4 est installé, sans ASP.NET Core 9). Les DDL
--            sont validées contre le dump autorité dotnet-ef (même que couleur
--            LotCoupes) et rejouées sur PostgreSQL 15 jetable.
-- Rétrocompatibilité: colonne ajoutée en nullable + backfill depuis LotCoupes,
--            PUIS passage NOT NULL. Les matelas orphelins (aucune coupe liée)
--            restent à l'écart — le rattachement administratif reste géré côté
--            API (MatelasController.SetCommande). Aucune table existante
--            altérée ni supprimée.
-- =============================================================================

START TRANSACTION;

-- 1. Colonne à rattachement optionnel (permis pour le backfill)
ALTER TABLE "Matelas" ADD "CommandeId" integer NULL;

-- 2. Backfill : affecte à chaque matelas la commande de sa coupe la plus
--    récente (les matelas sans coupe restent NULL)
WITH lots AS (
    SELECT DISTINCT ON ("MatelasId")
           "MatelasId", "CommandeId"
    FROM "LotCoupes"
    WHERE "MatelasId" IS NOT NULL AND "CommandeId" IS NOT NULL
    ORDER BY "MatelasId", "DateCoupe" DESC NULLS LAST
)
UPDATE "Matelas" m
SET "CommandeId" = lots."CommandeId"
FROM lots
WHERE lots."MatelasId" = m."Id"
  AND m."CommandeId" IS NULL;

-- 3. Passage NOT NULL une fois le backfill réalisé
ALTER TABLE "Matelas" ALTER COLUMN "CommandeId" SET NOT NULL;

-- 4. Index + contrainte de clé étrangère vers les commandes clients
CREATE INDEX "IX_Matelas_CommandeId" ON "Matelas" ("CommandeId");

ALTER TABLE "Matelas" ADD CONSTRAINT "FK_Matelas_CommandesClients_CommandeId"
    FOREIGN KEY ("CommandeId") REFERENCES "CommandesClients" ("Id")
    ON DELETE CASCADE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260919000000_AddCommandeIdToMatelas', '9.0.4');

COMMIT;
