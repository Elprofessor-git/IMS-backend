using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentImportationAndBOM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MarqueId",
                table: "CommandesClients",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BomLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    QuantiteParPiece = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unite = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BomLignes_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BomLignes_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfigTailles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantite = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigTailles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigTailles_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentsImportation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportationId = table.Column<int>(type: "integer", nullable: false),
                    NomFichier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CheminFichier = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TypeFichier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TailleOctets = table.Column<long>(type: "bigint", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AjoutePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsImportation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentsImportation_Importations_ImportationId",
                        column: x => x.ImportationId,
                        principalTable: "Importations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Marques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlateformeId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstActive = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marques_Plateformes_PlateformeId",
                        column: x => x.PlateformeId,
                        principalTable: "Plateformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultatsCalcul",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    BesoinBrut = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MargeAppliquee = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    BesoinFinal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QteAchat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QteImport = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QteStockReserve = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QteDisponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Manque = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstSuffisant = table.Column<bool>(type: "boolean", nullable: false),
                    DateCalcul = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultatsCalcul", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultatsCalcul_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultatsCalcul_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandesClients_MarqueId",
                table: "CommandesClients",
                column: "MarqueId");

            migrationBuilder.CreateIndex(
                name: "IX_BomLignes_ArticleId",
                table: "BomLignes",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_BomLignes_CommandeId",
                table: "BomLignes",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigTailles_CommandeId",
                table: "ConfigTailles",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsImportation_ImportationId",
                table: "DocumentsImportation",
                column: "ImportationId");

            migrationBuilder.CreateIndex(
                name: "IX_Marques_PlateformeId",
                table: "Marques",
                column: "PlateformeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultatsCalcul_ArticleId",
                table: "ResultatsCalcul",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultatsCalcul_CommandeId",
                table: "ResultatsCalcul",
                column: "CommandeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommandesClients_Marques_MarqueId",
                table: "CommandesClients",
                column: "MarqueId",
                principalTable: "Marques",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommandesClients_Marques_MarqueId",
                table: "CommandesClients");

            migrationBuilder.DropTable(
                name: "BomLignes");

            migrationBuilder.DropTable(
                name: "ConfigTailles");

            migrationBuilder.DropTable(
                name: "DocumentsImportation");

            migrationBuilder.DropTable(
                name: "Marques");

            migrationBuilder.DropTable(
                name: "ResultatsCalcul");

            migrationBuilder.DropIndex(
                name: "IX_CommandesClients_MarqueId",
                table: "CommandesClients");

            migrationBuilder.DropColumn(
                name: "MarqueId",
                table: "CommandesClients");
        }
    }
}
