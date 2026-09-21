using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class NotificationUtilisateurIdText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Correction du type de Notification.UtilisateurId : int → text.
            //
            // La migration 20260921070443_AddPlanningEtNotifications a créé la colonne en
            // « integer » alors que le modèle la déclare en string (ApplicationUser.Id est
            // un string GUID Identity). Résultat : toute écriture planning échouait en 500
            // (Npgsql 42804 « column UtilisateurId is of type integer but expression is of
            // type text ») et la lecture /me plantait en 500 (ToString non traduisible par EF).
            //
            // Idempotent : « USING "UtilisateurId"::text » fonctionne que la colonne soit
            // déjà « text » (environnements où le sidecar .sql a été appliqué) ou « integer »
            // (environnements où la migration EF a construit la table). La migration
            // 20260921070443 est déjà appliquée en production : elle n'est PAS modifiée.
            migrationBuilder.Sql(
                "ALTER TABLE \"Notifications\" ALTER COLUMN \"UtilisateurId\" TYPE text USING \"UtilisateurId\"::text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non restauré : le cast inverse échouerait sur les GUID non numériques déjà
            // persistés. La colonne reste text (désiré par le modèle).
        }
    }
}