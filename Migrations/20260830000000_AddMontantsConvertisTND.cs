using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddMontantsConvertisTND : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MontantTotalTND",
                table: "Achats",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontantLigneTND",
                table: "LignesAchat",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontantTotalTND",
                table: "Importations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontantLigneTND",
                table: "LignesImportation",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrixUnitaireTND",
                table: "Stocks",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MontantTotalTND",
                table: "Achats");

            migrationBuilder.DropColumn(
                name: "MontantLigneTND",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "MontantTotalTND",
                table: "Importations");

            migrationBuilder.DropColumn(
                name: "MontantLigneTND",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "PrixUnitaireTND",
                table: "Stocks");
        }
    }
}
