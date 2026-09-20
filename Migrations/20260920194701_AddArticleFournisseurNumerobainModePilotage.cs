using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleFournisseurNumerobainModePilotage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroBain",
                table: "LignesImportation",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroBain",
                table: "LignesAchat",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModePilotage",
                table: "CommandesClients",
                type: "text",
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.CreateTable(
                name: "ArticleFournisseurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    FournisseurId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceFournisseur = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PrixHabituel = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DelaiApprovisionnementJours = table.Column<int>(type: "integer", nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleFournisseurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleFournisseurs_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticleFournisseurs_Fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalTable: "Fournisseurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleFournisseurs_ArticleId_FournisseurId",
                table: "ArticleFournisseurs",
                columns: new[] { "ArticleId", "FournisseurId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleFournisseurs_FournisseurId",
                table: "ArticleFournisseurs",
                column: "FournisseurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleFournisseurs");

            migrationBuilder.DropColumn(
                name: "NumeroBain",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "NumeroBain",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "ModePilotage",
                table: "CommandesClients");
        }
    }
}
