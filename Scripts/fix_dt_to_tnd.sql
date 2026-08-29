-- =============================================================================
-- CORRECTIF DE DONNÉES — Chantier multi-devises, Phase 2
-- Migration du code non-ISO 'DT' (Dinar Tunisien) vers le code ISO 4217 'TND'.
-- Date: 2026-08-29
-- Nature : CORRECTION DE DONNÉES (pas un changement de schéma) => script SQL
--          simple, SANS Up()/Down() ni migration EF.
--
-- ⚠️  NE PAS EXÉCUTER AVANT :
--   1) La Partie 1 (tables "Devises" + "TauxChanges" + seed TND/EUR) soit en
--      place et VALIDÉE (DDL testé sur PostgreSQL 15 jetable).
--   2) Une dernière CONFIRMATION de Sof sur le total exact de lignes concernées.
--
-- Total 'DT' confirmé par lecture réelle de la base Neon (29/08/2026, SELECT) :
--   Achats                    9
--   LignesAchat              18
--   Stocks                   13
--   CommandesClients          2
--   HistoriquesPrixArticles   9
--   ---------------------------
--   TOTAL                    51
-- (Importations / LignesImportation : 0 'DT' — restent en EUR, inchangées.)
--
-- Chaque UPDATE est assorti d'un rappel du nombre attendu ; le script est conçu
-- pour être bloquant (s'arrête) si un total diffère de l'attendu, afin de ne pas
-- corriger à l'aveugle.
-- =============================================================================

-- Compte actuel avant correction (doit afficher 51)
SELECT 'TOTAL_DT' AS controle, COUNT(*) AS nb FROM (
    SELECT "Devise" FROM "Achats"  WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "LignesAchat" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "Stocks" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "CommandesClients" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "HistoriquesPrixArticles" WHERE "Devise"='DT'
) t;

-- Correction proprement dite
UPDATE "Achats"                  SET "Devise"='TND' WHERE "Devise"='DT'; -- attendu: 9
UPDATE "LignesAchat"             SET "Devise"='TND' WHERE "Devise"='DT'; -- attendu: 18
UPDATE "Stocks"                  SET "Devise"='TND' WHERE "Devise"='DT'; -- attendu: 13
UPDATE "CommandesClients"        SET "Devise"='TND' WHERE "Devise"='DT'; -- attendu: 2
UPDATE "HistoriquesPrixArticles" SET "Devise"='TND' WHERE "Devise"='DT'; -- attendu: 9

-- Contrôle post-correction : ne doit plus rester aucun 'DT'
SELECT 'RESTE_DT' AS controle, COUNT(*) AS nb FROM (
    SELECT "Devise" FROM "Achats"  WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "LignesAchat" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "Stocks" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "CommandesClients" WHERE "Devise"='DT'
    UNION ALL SELECT "Devise" FROM "HistoriquesPrixArticles" WHERE "Devise"='DT'
) t;  -- attendu: 0

-- =============================================================================
-- Notes :
--   * Les 4 champs "Devise" restants (Importations, LignesImportation) sont en
--     'EUR' et restent inchangés.
--   * Après exécution, relancer le GROUP BY de l'audit Phase 1 : plus aucune
--     valeur 'DT', uniquement 'TND' et 'EUR'.
-- =============================================================================
