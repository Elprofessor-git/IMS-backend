-- =============================================================================
-- Migration: Type de paiement sur Achat (Fonctionnalité 21)
-- Date: 2026-08-30
-- Technique: Script SQL manuel (méthode établie — cf. AddStockLigneTraceability.sql).
--            dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute 1 colonne nullable sur "Achats" (aucune colonne/table
--            existante modifiée). Nullable => les achats existants n'ont pas de mode
--            de règlement renseigné (aucune valeur par défaut arbitraire imposée).
--            Le champ est stocké en string (enum TypePaiement via HasConversion<string>).
-- =============================================================================

-- 1. Colonne "type de paiement" (nullable, stockée en text)
ALTER TABLE "Achats" ADD COLUMN IF NOT EXISTS "TypePaiement" text NULL;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable
--   FROM information_schema.columns
--   WHERE table_name = 'Achats' AND column_name = 'TypePaiement';
-- => 1 ligne : TypePaiement (text, YES)
-- =============================================================================
