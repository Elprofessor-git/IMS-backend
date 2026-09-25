using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanDeCoupeEtDimensionsMatelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // L1 coupe — dimensions facultatives du matelas (additif, nullable) :
            //  - Longueur (m) : saisie libre (matelas lecture seule en absence de valeur).
            //  - Laize (cm)   : héritée au défaut de Article.Laize à la création, libre ensuite.
            migrationBuilder.AddColumn<decimal>(
                name: "Longueur",
                table: "Matelas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Laize",
                table: "Matelas",
                type: "numeric",
                nullable: true);

            // L1 coupe — plan de coupe (marker) d'un matelas : une ligne par gabarit/taille.
            // Occurrences × PiecePliage = quantité théorique de la ligne. Verrous applicatifs :
            // plan figé (409) dès qu'une coupe réelle est rattachée au matelas.
            migrationBuilder.CreateTable(
                name: "PlanDeCoupeLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatelasId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDeCoupeLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDeCoupeLignes_Matelas_MatelasId",
                        column: x => x.MatelasId,
                        principalTable: "Matelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Décision 5 : une seule ligne par gabarit dans le plan d'un même matelas.
            // L'index composite couvre aussi les recherches par MatelasId seul.
            migrationBuilder.CreateIndex(
                name: "IX_PlanDeCoupeLignes_MatelasId_Taille",
                table: "PlanDeCoupeLignes",
                columns: new[] { "MatelasId", "Taille" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanDeCoupeLignes");

            migrationBuilder.DropColumn(
                name: "Longueur",
                table: "Matelas");

            migrationBuilder.DropColumn(
                name: "Laize",
                table: "Matelas");
        }
    }
}