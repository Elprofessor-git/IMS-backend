-- =============================================================================
-- Migration: FixDecimalPrecisions (précisions décimales des quantités)
-- Date: 2026-09-16
-- Technique: Triplet manuel (.cs/.Designer.cs/.sql), DDL validé contre le dump
--            autorité dotnet-ef (`dotnet ef migrations script`) et rejoué sur
--            PostgreSQL 15 jetable. Aligne 5 colonnes quantités sur numeric(18,4)
--            conformément à la vérification DDL du schéma (QuantiteAchatsLocaux,
--            QuantiteStockImporte et QuantiteStockLibre étaient en numeric sans
--            précision ; LigneAchat.QuantiteRecue et LigneImportation.QuantiteRecue
--            étaient déjà en numeric(18,4) en base -> ALTER idempotent/no-op).
-- Rétrocompatible: changement de type uniquement, aucune valeur perdue
--            (numeric(18,4) >= numeric sans précision pour les données stockées),
--            aucune colonne/table ajoutée ou supprimée.
-- =============================================================================

-- 1. BesoinsCommandes -> quantités alignées sur numeric(18,4)
ALTER TABLE "BesoinsCommandes" ALTER COLUMN "QuantiteAchatsLocaux" TYPE numeric(18,4);
ALTER TABLE "BesoinsCommandes" ALTER COLUMN "QuantiteStockImporte" TYPE numeric(18,4);
ALTER TABLE "BesoinsCommandes" ALTER COLUMN "QuantiteStockLibre" TYPE numeric(18,4);

-- 2. QuantitesRecue des lignes d'achat et d'importation (déjà numeric(18,4) en
--    base -> ALTER idempotent, garanti par la migration)
ALTER TABLE "LignesAchat" ALTER COLUMN "QuantiteRecue" TYPE numeric(18,4);
ALTER TABLE "LignesImportation" ALTER COLUMN "QuantiteRecue" TYPE numeric(18,4);