using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardRapportsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ajouter les 2 colonnes PeutVoirDashboard + PeutVoirRapports sur "Role".
            // NOT NULL DEFAULT true — rétrocompatible : tous les rôles existants
            // peuvent voir le tableau de bord et les rapports par défaut.
            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirDashboard",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirRapports",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PeutVoirDashboard", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirRapports", table: "Role");
        }
    }
}
