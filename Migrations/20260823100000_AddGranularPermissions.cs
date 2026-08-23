using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGranularPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ajouter les 9 nouvelles colonnes (8 PeutVoirX + PeutGererPlateformes)
            // NOT NULL DEFAULT false — rétrocompatible : tous les rôles existants
            // commencent avec false, puis le backfill ci-dessous copie la valeur existante.
            migrationBuilder.AddColumn<bool>(
                name: "PeutGererPlateformes",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirClients",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirCommandes",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirFournisseurs",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirMouvements",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirPlateformes",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirRoles",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirTaches",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirUtilisateurs",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill : copier les valeurs existantes pour garantir zéro régression.
            // PeutVoirX = PeutGererX existant (l'accès en lecture est au moins aussi
            // large que l'accès en écriture precedent).
            // PeutGererPlateformes = PeutGererClients (partage precedent).
            // PeutVoirRoles = EstAdministrateur (seul admin pouvait voir les roles).
            migrationBuilder.Sql(@"
                UPDATE ""Role"" SET
                    ""PeutVoirMouvements"" = ""PeutGererMouvements"",
                    ""PeutVoirCommandes"" = ""PeutGererCommandes"",
                    ""PeutVoirClients"" = ""PeutGererClients"",
                    ""PeutVoirFournisseurs"" = ""PeutGererFournisseurs"",
                    ""PeutVoirPlateformes"" = ""PeutGererClients"",
                    ""PeutGererPlateformes"" = ""PeutGererClients"",
                    ""PeutVoirTaches"" = ""PeutGererTaches"",
                    ""PeutVoirUtilisateurs"" = ""PeutGererUtilisateurs"",
                    ""PeutVoirRoles"" = ""EstAdministrateur""
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PeutGererPlateformes", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirClients", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirCommandes", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirFournisseurs", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirMouvements", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirPlateformes", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirRoles", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirTaches", table: "Role");
            migrationBuilder.DropColumn(name: "PeutVoirUtilisateurs", table: "Role");
        }
    }
}
