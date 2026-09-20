using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoupePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Module « Coupe » : permissions dédiées (distinctes de « commandes »).
            // Par défaut FALSE pour tous les rôles existants : aucune élévation de
            // privilège automatique. Seul l'administrateur est backfillé à TRUE
            // ci-dessous (rôle Id=1), cohérent avec les autres modules.
            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirCoupe",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutGererCoupe",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // L'administrateur (rôle 1) reste gestionnaire du module Coupe.
            migrationBuilder.Sql(
                """
                UPDATE "Role"
                SET "PeutVoirCoupe" = true,
                    "PeutGererCoupe" = true
                WHERE "Id" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeutVoirCoupe",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutGererCoupe",
                table: "Role");
        }
    }
}