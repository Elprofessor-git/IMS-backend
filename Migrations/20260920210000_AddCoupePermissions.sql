-- =============================================================================
-- Migration: AddCoupePermissions (module « Coupe » — permissions dédiées)
-- Date: 2026-09-20
-- Technique: Triplet manuel (.cs/.Designer.cs/.sql). Le Designer.cs et le
--            snapshot ont été régénérés pour refléter le modèle cible complet
--            (EF Core 9, `dotnet ef migrations add` indisponible ici : seul le
--            runtime .NET 10.0.4 est installé, sans ASP.NET Core 9). Les DDL
--            sont validées contre un snapshot réel de production (Neon) rejoué
--            sur PostgreSQL 15 jetable.
-- Rétrocompatibilité: colonnes ajoutées avec DEFAULT false — pas d'élévation
--            de privilège pour les rôles existants. Seul l'administrateur
--            (Id=1) est backfillé à true, cohérent avec les autres modules.
-- =============================================================================

START TRANSACTION;

ALTER TABLE "Role" ADD "PeutVoirCoupe" boolean NOT NULL DEFAULT false;
ALTER TABLE "Role" ADD "PeutGererCoupe" boolean NOT NULL DEFAULT false;

UPDATE "Role"
SET "PeutVoirCoupe" = true,
    "PeutGererCoupe" = true
WHERE "Id" = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260920210000_AddCoupePermissions', '9.0.4');

COMMIT;