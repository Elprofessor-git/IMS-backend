using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class LigneAchatScopeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Rendre Achats.CommandeClientId nullable
            migrationBuilder.AlterColumn<int>(
                name: "CommandeClientId",
                table: "Achats",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // 2. Ajouter les champs de scope sur LignesAchat
            migrationBuilder.AddColumn<string>(
                name: "TypeDestination",
                table: "LignesAchat",
                type: "text",
                nullable: false,
                defaultValue: "StockLibre");

            migrationBuilder.AddColumn<int>(
                name: "CommandeClientId",
                table: "LignesAchat",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "LignesAchat",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateformeId",
                table: "LignesAchat",
                type: "integer",
                nullable: true);

            // 3. Migration de données : rétro-remplir depuis l'en-tête Achat
            //    Les LignesAchat existantes héritent du CommandeClientId de l'en-tête
            //    et reçoivent TypeDestination='Commande' (comportement historique).
            migrationBuilder.Sql(@"
                UPDATE ""LignesAchat"" la
                SET ""CommandeClientId"" = a.""CommandeClientId"",
                    ""TypeDestination""  = 'Commande'
                FROM ""Achats"" a
                WHERE la.""AchatId"" = a.""Id""
                  AND a.""CommandeClientId"" IS NOT NULL;
            ");

            // 4. Index
            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_CommandeClientId",
                table: "LignesAchat",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_ClientId",
                table: "LignesAchat",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_PlateformeId",
                table: "LignesAchat",
                column: "PlateformeId");

            // 5. Clés étrangères
            migrationBuilder.AddForeignKey(
                name: "FK_LignesAchat_CommandesClients_CommandeClientId",
                table: "LignesAchat",
                column: "CommandeClientId",
                principalTable: "CommandesClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LignesAchat_Clients_ClientId",
                table: "LignesAchat",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LignesAchat_Plateformes_PlateformeId",
                table: "LignesAchat",
                column: "PlateformeId",
                principalTable: "Plateformes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LignesAchat_CommandesClients_CommandeClientId",
                table: "LignesAchat");

            migrationBuilder.DropForeignKey(
                name: "FK_LignesAchat_Clients_ClientId",
                table: "LignesAchat");

            migrationBuilder.DropForeignKey(
                name: "FK_LignesAchat_Plateformes_PlateformeId",
                table: "LignesAchat");

            migrationBuilder.DropIndex(
                name: "IX_LignesAchat_CommandeClientId",
                table: "LignesAchat");

            migrationBuilder.DropIndex(
                name: "IX_LignesAchat_ClientId",
                table: "LignesAchat");

            migrationBuilder.DropIndex(
                name: "IX_LignesAchat_PlateformeId",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "TypeDestination",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "CommandeClientId",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "PlateformeId",
                table: "LignesAchat");

            migrationBuilder.AlterColumn<int>(
                name: "CommandeClientId",
                table: "Achats",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
