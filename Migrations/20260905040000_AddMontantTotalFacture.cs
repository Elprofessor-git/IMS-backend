using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddMontantTotalFacture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MontantTotal",
                table: "Factures",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Backfill des factures existantes (somme des lignes) : idempotent.
            migrationBuilder.Sql(
                "UPDATE \"Factures\" f SET \"MontantTotal\" = COALESCE(" +
                "(SELECT SUM(l.\"MontantLigne\") FROM \"FactureCommandesLignes\" l WHERE l.\"FactureId\" = f.\"Id\"), 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MontantTotal",
                table: "Factures");
        }
    }
}