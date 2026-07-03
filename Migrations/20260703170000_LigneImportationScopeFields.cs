using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class LigneImportationScopeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TypeDestination",
                table: "LignesImportation",
                type: "text",
                nullable: false,
                defaultValue: "StockLibre");

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "LignesImportation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateformeId",
                table: "LignesImportation",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_ClientId",
                table: "LignesImportation",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_PlateformeId",
                table: "LignesImportation",
                column: "PlateformeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LignesImportation_Clients_ClientId",
                table: "LignesImportation",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LignesImportation_Plateformes_PlateformeId",
                table: "LignesImportation",
                column: "PlateformeId",
                principalTable: "Plateformes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LignesImportation_Clients_ClientId",
                table: "LignesImportation");

            migrationBuilder.DropForeignKey(
                name: "FK_LignesImportation_Plateformes_PlateformeId",
                table: "LignesImportation");

            migrationBuilder.DropIndex(
                name: "IX_LignesImportation_ClientId",
                table: "LignesImportation");

            migrationBuilder.DropIndex(
                name: "IX_LignesImportation_PlateformeId",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "TypeDestination",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "PlateformeId",
                table: "LignesImportation");
        }
    }
}
