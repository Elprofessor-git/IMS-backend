-- =============================================================================
-- Migration: Traçabilité de la réception d'origine sur le Stock (Fonctionnalité 18)
-- Date: 2026-08-31
-- Technique: Script SQL manuel (méthode établie — cf. ReceptionPartielle.sql,
--            AddMontantsConvertisTND.sql). dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute 2 colonnes nullable + 2 index + 2 clés étrangères sur
--            "Stocks" (aucune colonne/table existante modifiée, aucune contrainte
--            supprimée). Nullable => le stock existant, le stock scindé et le stock
--            manuel conservent une ligne d'origine NULL.
-- Comportement: onDelete RESTRICT (aligné sur les FKs existantes Stock->Client/
--            Plateforme). Les colonnes existantes ont une valeur NULL => l'application
--            new = FALSE (DataLoss n'est pas concerné : pas de colonne perdue).
-- =============================================================================

-- 1. Colonnes de traçabilité (nullable, aucune valeur par défaut)
ALTER TABLE "Stocks" ADD COLUMN IF NOT EXISTS "LigneAchatId" integer NULL;
ALTER TABLE "Stocks" ADD COLUMN IF NOT EXISTS "LigneImportationId" integer NULL;

-- 2. Index (amélioration d'accès — cohérent avec IX_Stocks_ClientId/PlateformeId)
CREATE INDEX IF NOT EXISTS "IX_Stocks_LigneAchatId" ON "Stocks" ("LigneAchatId");
CREATE INDEX IF NOT EXISTS "IX_Stocks_LigneImportationId" ON "Stocks" ("LigneImportationId");

-- 3. Clés étrangères (RESTRICT, table cible "LignesAchat"/"LignesImportation")
ALTER TABLE "Stocks" ADD CONSTRAINT "FK_Stocks_LignesAchat_LigneAchatId"
    FOREIGN KEY ("LigneAchatId") REFERENCES "LignesAchat" ("Id")
    ON DELETE RESTRICT;

ALTER TABLE "Stocks" ADD CONSTRAINT "FK_Stocks_LignesImportation_LigneImportationId"
    FOREIGN KEY ("LigneImportationId") REFERENCES "LignesImportation" ("Id")
    ON DELETE RESTRICT;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable
--   FROM information_schema.columns
--   WHERE table_name = 'Stocks' AND column_name IN ('LigneAchatId','LigneImportationId');
-- => 2 lignes : LigneAchatId (integer, YES), LigneImportationId (integer, YES)
--
-- SELECT conname, contype, confdeltype
--   FROM pg_constraint
--   WHERE conrelid = '"Stocks"'::regclass
--     AND conname IN ('FK_Stocks_LignesAchat_LigneAchatId','FK_Stocks_LignesImportation_LigneImportationId');
-- => 2 lignes, confdeltype = 'r' (RESTRICT)
-- =============================================================================
