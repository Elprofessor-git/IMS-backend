using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LOT 2 / Partie A — les lignes de dates deviennent des entités de premier
            // rang : une ligne de date peut exister SANS cellule (créée, modifiée ou
            // supprimée indépendamment). Jusqu'ici la grille était transposée et les
            // dates n'étaient que des samedis générés côté client ; le modèle voulu est
            // « chaînes = colonnes, dates = lignes ».
            //
            // Backfill préservant les données : chaque date déjà présente dans une
            // cellule (PlanningEntries.DateSamedi) devient une ligne de date, pour que
            // les cellules existantes restent visibles immédiatement après migration.

            migrationBuilder.CreateTable(
                name: "PlanningDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDates", x => x.Id);
                });

            migrationBuilder.Sql(
                "INSERT INTO \"PlanningDates\" (\"Date\") " +
                "SELECT DISTINCT \"DateSamedi\" FROM \"PlanningEntries\" " +
                "WHERE \"DateSamedi\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDates_Date",
                table: "PlanningDates",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanningDates_Date",
                table: "PlanningDates");

            migrationBuilder.DropTable(
                name: "PlanningDates");
        }
    }
}