-- =============================================================================
-- Migration: Marge de sécurité par défaut sur les commandes clients (Bug 29)
-- Date: 2026-09-11
-- Technique: Triplet manuel (.cs/.Designer.cs/.sql), DDL validé contre le dump
--            autorité dotnet-ef (`dotnet ef migrations script`) et rejoué sur
--            PostgreSQL 15 jetable. Colonne vide par défaut à la valeur 0 =>
--            rétrocompatibilité : les commandes qui n'ont jamais utilisé
--            Calculer conservent une marge de 0 et ValiderRessources se
--            comporte exactement comme avant.
-- Rétrocompatible: ajoute 1 colonne NOT NULL avec DEFAULT 0 (aucune colonne/
--            table existante modifiée, aucune contrainte supprimée).
-- =============================================================================

-- 1. Colonne de marge de sécurité (défaut 0, précision alignée sur
--    ResultatCalcul.MargeAppliquee => numeric(5,2))
ALTER TABLE "CommandesClients" ADD COLUMN IF NOT EXISTS "MargeSecuriteDefaut" numeric(5,2) NOT NULL DEFAULT 0.0;