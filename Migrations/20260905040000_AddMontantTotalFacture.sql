-- =============================================================================
-- Migration: MontantTotal figé sur Factures (Point 2 — prompt correctif 2026-09-06)
-- Date: 2026-09-06
-- Technique: Script SQL manuel (méthode établie — cf. AddStockLigneTraceability.sql).
--            dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajout d'une colonne NOT NULL avec défaut 0 + backfill destructif
--            sur les factures existantes depuis la somme des lignes (fait UNE SEULE
--            fois ici ; ensuite la valeur est figée en base à l'écriture).
--            Le backfill ne reproduit PAS les éventuelles modifications ultérieures
--            (le GET lit désormais la valeur stockée — jamais recalculée).
-- Convention alignée sur LigneAchat/LigneImportation : montants calculés à
-- l'écriture, stockés, jamais recalculés à la lecture.
-- =============================================================================

-- 1. Ajout de la colonne
ALTER TABLE "Factures" ADD COLUMN IF NOT EXISTS "MontantTotal" numeric(18,4) NOT NULL DEFAULT 0;

-- 2. Backfill : les factures déjà en base (brouillons ou émises) reçoivent le total
--    correspondant à leur état actuel des lignes. Idempotent si rejoué.
UPDATE "Factures" f
   SET "MontantTotal" = COALESCE((
         SELECT SUM(l."MontantLigne")
           FROM "FactureCommandesLignes" l
          WHERE l."FactureId" = f."Id"
       ), 0);

-- =============================================================================
-- Vérification attendue après exécution :
-- \d "Factures" => colonne "MontantTotal" numeric(18,4) NOT NULL DEFAULT 0
-- SELECT "Id","NumeroFacture","MontantTotal" FROM "Factures"; => totaux = somme des lignes
-- =============================================================================