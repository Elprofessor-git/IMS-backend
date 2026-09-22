/* Sidecar de la migration MatelasUniciteParCommande
   (convention projet : chaque migration manuelle = triplet .cs/.Designer.cs + sidecar .sql
    idempotent, exécutable directement en DB).
   Règle : unicité du matelas PAR COMMANDE, insensible casse/espaces.
   Lève l'ancienne contrainte globale (IX_Matelas_NumeroMatelas) et crée un index
   fonctionnel unique sur ("CommandeId", UPPER(blancs retirés)). */

DROP INDEX IF EXISTS "IX_Matelas_NumeroMatelas";

CREATE UNIQUE INDEX "IX_Matelas_CommandeId_NumeroMatelas"
    ON "Matelas" ("CommandeId", UPPER(REGEXP_REPLACE("NumeroMatelas", '\s', '', 'g')));

INSERT INTO "__EFMigrationsHistory" (MigrationId, ProductVersion)
VALUES ('20260922090000_MatelasUniciteParCommande', '9.0.16')
ON CONFLICT (MigrationId) DO NOTHING;