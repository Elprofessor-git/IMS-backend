-- =============================================================================
-- Migration: Nature libre sur DocumentJoint (Fonctionnalité 20)
-- Date: 2026-08-30
-- Technique: Script SQL manuel (méthode établie — cf. AddStockLigneTraceability.sql).
--            dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute 1 colonne nullable sur "DocumentsJoints" (aucune colonne/
--            table existante modifiée). Nullable => les documents existants (y compris
--            ceux de type "Autre" ajoutés avant cette fonctionnalité) conservent une
--            nature NULL. La nature n'est utilisée que pour Type = 'Autre'.
-- =============================================================================

-- 1. Colonne "nature libre" (nullable, aucune valeur par défaut)
ALTER TABLE "DocumentsJoints" ADD COLUMN IF NOT EXISTS "Nature" character varying(200) NULL;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable
--   FROM information_schema.columns
--   WHERE table_name = 'DocumentsJoints' AND column_name = 'Nature';
-- => 1 ligne : Nature (character varying, YES)
-- =============================================================================
