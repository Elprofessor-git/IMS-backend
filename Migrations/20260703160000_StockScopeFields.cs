using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class StockScopeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateformeId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_ClientId",
                table: "Stocks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_PlateformeId",
                table: "Stocks",
                column: "PlateformeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Clients_ClientId",
                table: "Stocks",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Plateformes_PlateformeId",
                table: "Stocks",
                column: "PlateformeId",
                principalTable: "Plateformes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Clients_ClientId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Plateformes_PlateformeId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_ClientId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_PlateformeId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "PlateformeId",
                table: "Stocks");
        }
    }
}
