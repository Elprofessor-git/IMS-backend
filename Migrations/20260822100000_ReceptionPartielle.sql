-- =============================================================================
-- Migration: Réception partielle avec clôture forcée (Fonctionnalité 13)
-- Date: 2026-08-22
-- Technique: Script SQL manuel (méthode Fonctionnalité 8)
-- Rétrocompatible: les lignes existantes sont rétro-mappées
-- =============================================================================

-- 1. LignesAchat : ajouter QuantiteRecue et StatutLigne
ALTER TABLE "LignesAchat"
    ADD COLUMN IF NOT EXISTS "QuantiteRecue" decimal(18,4) NOT NULL DEFAULT 0;

ALTER TABLE "LignesAchat"
    ADD COLUMN IF NOT EXISTS "StatutLigne" text NOT NULL DEFAULT 'EnAttente';

-- 2. LignesImportation : ajouter QuantiteRecue et StatutLigne
ALTER TABLE "LignesImportation"
    ADD COLUMN IF NOT EXISTS "QuantiteRecue" decimal(18,4) NOT NULL DEFAULT 0;

ALTER TABLE "LignesImportation"
    ADD COLUMN IF NOT EXISTS "StatutLigne" text NOT NULL DEFAULT 'EnAttente';

-- 3. Backfill rétrocompatible : marquer les lignes déjà reçues
-- Achat : si l'en-tête est Livre, la ligne était entièrement reçue
UPDATE "LignesAchat" la
SET "QuantiteRecue" = la."Quantite",
    "StatutLigne" = 'Complete'
FROM "Achats" a
WHERE la."AchatId" = a."Id"
  AND a."Statut" = 'Livre'
  AND la."StatutLigne" = 'EnAttente';

-- Importation : si l'en-tête est Recue, la ligne était entièrement reçue
UPDATE "LignesImportation" li
SET "QuantiteRecue" = li."Quantite",
    "StatutLigne" = 'Complete'
FROM "Importations" i
WHERE li."ImportationId" = i."Id"
  AND i."Statut" = 'Recue'
  AND li."StatutLigne" = 'EnAttente';

-- =============================================================================
-- Vérification attendue après exécution :
-- SELECT COUNT(*) FROM "LignesAchat" WHERE "StatutLigne" = 'Complete';  -- devrait = nb lignes d'achats Livres
-- SELECT COUNT(*) FROM "LignesImportation" WHERE "StatutLigne" = 'Complete';  -- devrait = nb lignes d'importations Recues
-- SELECT COUNT(*) FROM "LignesAchat" WHERE "QuantiteRecue" > 0 AND "StatutLigne" = 'EnAttente';  -- devrait = 0
-- =============================================================================
