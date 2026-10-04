using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddBesoinCoupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BesoinsCoupe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantiteCommandee = table.Column<int>(type: "integer", nullable: false),
                    MetrageAnnonce = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmpreinteCalcul = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BesoinsCoupe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BesoinsCoupe_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BesoinsCoupe_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BesoinsCoupe_ArticleId",
                table: "BesoinsCoupe",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_BesoinsCoupe_CommandeClientId_ArticleId_Couleur",
                table: "BesoinsCoupe",
                columns: new[] { "CommandeClientId", "ArticleId", "Couleur" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BesoinsCoupe");
        }
    }
}
