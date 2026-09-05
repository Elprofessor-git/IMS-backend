-- =============================================================================
-- Migration: Bon de commande de production lié à la CommandeClient (Partie 2)
-- Date: 2026-09-05
-- Technique: Script SQL manuel (méthode établie — cf. AddStockLigneTraceability.sql).
--            dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute 1 colonne nullable + 1 index + 1 clé étrangère sur
--            "DocumentsJoints" (aucune colonne/table existante modifiée, aucune
--            contrainte supprimée). Nullable => les documents existants liés aux
--            achats/importations conservent une commande NULL.
-- Comportement: onDelete CASCADE (aligné sur BesoinsCommandes->CommandesClients) :
--            la suppression d'une commande entraîne ses documents de production.
-- =============================================================================

-- 1. Colonne "CommandeClientId" (nullable, aucune valeur par défaut)
ALTER TABLE "DocumentsJoints" ADD COLUMN IF NOT EXISTS "CommandeClientId" integer NULL;

-- 2. Index (amélioration d'accès — cohérent avec IX_DocumentsJoints_AchatId)
CREATE INDEX IF NOT EXISTS "IX_DocumentsJoints_CommandeClientId"
    ON "DocumentsJoints" ("CommandeClientId");

-- 3. Clé étrangère (CASCADE, table cible "CommandesClients")
ALTER TABLE "DocumentsJoints" ADD CONSTRAINT "FK_DocumentsJoints_CommandesClients_CommandeClientId"
    FOREIGN KEY ("CommandeClientId") REFERENCES "CommandesClients" ("Id")
    ON DELETE CASCADE;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable
--   FROM information_schema.columns
--   WHERE table_name = 'DocumentsJoints' AND column_name = 'CommandeClientId';
-- => 1 ligne : CommandeClientId (integer, YES)
--
-- SELECT conname, contype, confdeltype
--   FROM pg_constraint
--   WHERE conrelid = '"DocumentsJoints"'::regclass
--     AND conname = 'FK_DocumentsJoints_CommandesClients_CommandeClientId';
-- => 1 ligne, confdeltype = 'c' (CASCADE)
-- =============================================================================