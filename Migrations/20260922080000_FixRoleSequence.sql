/* Sidecar de la migration FixRoleSequence
   (convention projet : chaque migration EF = triplet .cs/.Designer.cs + sidecar .sql
    idempotent, appliqué directement en DB docker imsjetable-db-1 GestionTextileDB).
    Cohérent avec Migrations/20260922080000_FixRoleSequence.cs */

-- Resynchronise la séquence IDENTITY de « Role » sur son maximum réel :
-- SeedData insèra le rôle administrateur avec Id explicite (1) sans avancer la
-- séquence -> toute création de rôle via l'API recollisionnait sur 1 (23505).
-- Idempotent : remesuré à chaque exécution.
SELECT setval('"Role_Id_seq"', (SELECT GREATEST(MAX("Id"), 1) FROM "Role"));

INSERT INTO "__EFMigrationsHistory" (MigrationId, ProductVersion)
VALUES ('20260922080000_FixRoleSequence', '9.0.16')
ON CONFLICT (MigrationId) DO NOTHING;