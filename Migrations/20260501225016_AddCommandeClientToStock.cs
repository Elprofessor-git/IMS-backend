using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandeClientToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandeClientId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CommandeClientId",
                table: "Stocks",
                column: "CommandeClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_CommandesClients_CommandeClientId",
                table: "Stocks",
                column: "CommandeClientId",
                principalTable: "CommandesClients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_CommandesClients_CommandeClientId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CommandeClientId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CommandeClientId",
                table: "Stocks");
        }
    }
}
