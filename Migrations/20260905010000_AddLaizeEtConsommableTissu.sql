-- =============================================================================
-- Migration: Référentiel tissu — Laize sur Article + consommable tissu sur BOM (Partie 3)
-- Date: 2026-09-05
-- Technique: Script SQL manuel (méthode établie — cf. AddStockLigneTraceability.sql).
--            dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute 1 colonne nullable sur "Articles" et 1 colonne booléenne
--            NOT NULL avec défaut FALSE sur "BomLignes". Aucune colonne/table
--            existante modifiée, aucune contrainte supprimée.
-- Comportement: Laize NULL pour les non-tissus (boutons, fils…) ; EstConsommableTissu
--            vaut FALSE pour toutes les lignes BOM existantes (aucune migration
--            de données nécessaire — la valeur est saisie par l'utilisateur ensuite).
-- =============================================================================

-- 1. Largeur du rouleau de tissu en mètres (nullable, aucun défaut)
ALTER TABLE "Articles" ADD COLUMN IF NOT EXISTS "Laize" numeric NULL;

-- 2. Flag « consommable tissu » sur les lignes BOM (FALSE par défaut pour l'existant)
ALTER TABLE "BomLignes" ADD COLUMN IF NOT EXISTS "EstConsommableTissu" boolean NOT NULL DEFAULT FALSE;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--   WHERE table_name IN ('Articles','BomLignes') AND column_name IN ('Laize','EstConsommableTissu');
-- => 2 lignes : Laize (numeric, YES, null) et EstConsommableTissu (boolean, NO, false)
-- =============================================================================