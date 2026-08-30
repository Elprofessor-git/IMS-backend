using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLigneTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LigneAchatId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LigneImportationId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_LigneAchatId",
                table: "Stocks",
                column: "LigneAchatId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_LigneImportationId",
                table: "Stocks",
                column: "LigneImportationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_LignesAchat_LigneAchatId",
                table: "Stocks",
                column: "LigneAchatId",
                principalTable: "LignesAchat",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_LignesImportation_LigneImportationId",
                table: "Stocks",
                column: "LigneImportationId",
                principalTable: "LignesImportation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_LignesAchat_LigneAchatId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_LignesImportation_LigneImportationId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_LigneAchatId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_LigneImportationId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "LigneAchatId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "LigneImportationId",
                table: "Stocks");
        }
    }
}
