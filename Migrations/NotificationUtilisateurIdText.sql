/* Sidecar de la migration NotificationUtilisateurIdText
   (convention projet : chaque migration EF = triplet .cs/.Designer.cs + sidecar .sql
    idempotent, appliqué directement en DB docker imsjetable-db-1 GestionTextileDB).
    Cohérent avec Migrations/20260921090000_NotificationUtilisateurIdText.cs.

    Idempotent : le USING "UtilisateurId"::text fonctionne que la colonne soit
    déjà « text » (sidecar de AddPlanningEtNotifications) ou « integer » (migration EF). */

ALTER TABLE "Notifications" ALTER COLUMN "UtilisateurId" TYPE text USING "UtilisateurId"::text;
INSERT INTO "__EFMigrationsHistory" (MigrationId, ProductVersion)
VALUES ('20260921090000_NotificationUtilisateurIdText', '9.0.4')
ON CONFLICT (MigrationId) DO NOTHING;