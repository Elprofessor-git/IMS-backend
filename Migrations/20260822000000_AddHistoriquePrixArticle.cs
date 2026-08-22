using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoriquePrixArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoriquesPrixArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DateEffective = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    LigneAchatId = table.Column<int>(type: "integer", nullable: true),
                    LigneImportationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriquesPrixArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriquesPrixArticles_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoriquesPrixArticles_LignesAchat_LigneAchatId",
                        column: x => x.LigneAchatId,
                        principalTable: "LignesAchat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoriquesPrixArticles_LignesImportation_LigneImportationId",
                        column: x => x.LigneImportationId,
                        principalTable: "LignesImportation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoriquesPrixArticles_ArticleId_DateEffective",
                table: "HistoriquesPrixArticles",
                columns: new[] { "ArticleId", "DateEffective" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoriquesPrixArticles_LigneAchatId",
                table: "HistoriquesPrixArticles",
                column: "LigneAchatId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriquesPrixArticles_LigneImportationId",
                table: "HistoriquesPrixArticles",
                column: "LigneImportationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoriquesPrixArticles");
        }
    }
}
