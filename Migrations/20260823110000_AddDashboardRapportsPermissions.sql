-- =============================================================================
-- Migration: Permissions PeutVoirDashboard + PeutVoirRapports sur le rôle
-- Date: 2026-08-23
-- Technique: Script SQL manuel (méthode établie — cf. ReceptionPartielle.sql,
--            AddMontantsConvertisTND.sql). dotnet-ef indisponible => DDL écrit à la main.
-- Rétrocompatible: ajoute uniquement 2 colonnes booléennes sur "Role"
--            (aucune colonne/table modifiée, aucune contrainte supprimée).
-- Défaut true => tous les rôles existants (dont l'admin par défaut) peuvent
--            voir le tableau de bord et les rapports immédiatement, sans backfill.
-- Correctif: migration fantôme — PeutVoirDashboard/PeutVoirRapports existaient
--            dans Models/Role.cs sans migration en dépôt => drift modèle/migrations
--            provoquant "column r.PeutVoirDashboard does not exist" à la connexion
--            sur base fraîche. MigrationId = 20260823110000 (celui enregistré sur
--            Neon dans __EFMigrationsHistory) => MigrateAsync la saute sur Neon
--            et l'applique sur base fraîche.
-- =============================================================================

-- 1. PeutVoirDashboard : accès au tableau de bord
ALTER TABLE "Role" ADD COLUMN IF NOT EXISTS "PeutVoirDashboard" boolean NOT NULL DEFAULT TRUE;

-- 2. PeutVoirRapports : accès aux rapports
ALTER TABLE "Role" ADD COLUMN IF NOT EXISTS "PeutVoirRapports" boolean NOT NULL DEFAULT TRUE;

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--   WHERE table_name = 'Role' AND column_name IN ('PeutVoirDashboard','PeutVoirRapports');
-- => 2 lignes : PeutVoirDashboard (boolean, NO, true), PeutVoirRapports (boolean, NO, true)
-- =============================================================================
