-- =============================================================================
-- Migration: Montants convertis en TND à l'écriture (chantier multi-devises, Phase 4)
-- Date: 2026-08-30
-- Technique: Script SQL manuel (méthode établie — cf. ReceptionPartielle.sql,
--            AddDevisesTauxChanges.sql). dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute uniquement 5 colonnes (aucune colonne/table modifiée,
--            aucune contrainte supprimée). Défaut 0 => les lignes existantes restent
--            à 0, elles seront refroidies par le backfill (Partie 4).
-- Charge: conversion effectuée côté applicatif via TauxChangeService (static).
-- =============================================================================

-- 1. Achat : total converti en TND
ALTER TABLE "Achats" ADD COLUMN IF NOT EXISTS "MontantTotalTND" numeric(18,4) NOT NULL DEFAULT 0;

-- 2. LigneAchat : montant de ligne converti en TND
ALTER TABLE "LignesAchat" ADD COLUMN IF NOT EXISTS "MontantLigneTND" numeric(18,4) NOT NULL DEFAULT 0;

-- 3. Importation : total converti en TND
ALTER TABLE "Importations" ADD COLUMN IF NOT EXISTS "MontantTotalTND" numeric(18,4) NOT NULL DEFAULT 0;

-- 4. LigneImportation : montant de ligne converti en TND
ALTER TABLE "LignesImportation" ADD COLUMN IF NOT EXISTS "MontantLigneTND" numeric(18,4) NOT NULL DEFAULT 0;

-- 5. Stock : prix unitaire converti en TND (valorisation du stock)
ALTER TABLE "Stocks" ADD COLUMN IF NOT EXISTS "PrixUnitaireTND" numeric(18,4) NOT NULL DEFAULT 0;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--   WHERE table_name IN ('Achats','LignesAchat','Importations','LignesImportation','Stocks')
--     AND column_name LIKE '%TND';
-- => 5 lignes : MontantTotalTND (x2), MontantLigneTND (x2), PrixUnitaireTND (x1)
-- =============================================================================
