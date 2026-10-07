using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdreCoupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdresCoupe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: false),
                    Modele = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    MargeSecurite = table.Column<decimal>(type: "numeric", nullable: true),
                    ReferenceOF = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresCoupe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresCoupe_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdresCoupe_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdreCoupeMatieres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreCoupeId = table.Column<int>(type: "integer", nullable: false),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Laize = table.Column<decimal>(type: "numeric", nullable: true),
                    ConsoClient = table.Column<decimal>(type: "numeric", nullable: false),
                    ConsoReelle = table.Column<decimal>(type: "numeric", nullable: true),
                    RetraitPourcentage = table.Column<decimal>(type: "numeric", nullable: false),
                    MetresRecus = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdreCoupeMatieres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdreCoupeMatieres_OrdresCoupe_OrdreCoupeId",
                        column: x => x.OrdreCoupeId,
                        principalTable: "OrdresCoupe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdreCoupePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreCoupeId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Plis = table.Column<int>(type: "integer", nullable: false),
                    MatelasId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdreCoupePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdreCoupePlans_Matelas_MatelasId",
                        column: x => x.MatelasId,
                        principalTable: "Matelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdreCoupePlans_OrdresCoupe_OrdreCoupeId",
                        column: x => x.OrdreCoupeId,
                        principalTable: "OrdresCoupe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdreCoupeTailles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreCoupeId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantiteDemandee = table.Column<int>(type: "integer", nullable: true),
                    QuantiteAvecMarge = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdreCoupeTailles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdreCoupeTailles_OrdresCoupe_OrdreCoupeId",
                        column: x => x.OrdreCoupeId,
                        principalTable: "OrdresCoupe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdreCoupeOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreCoupePlanId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdreCoupeOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdreCoupeOccurrences_OrdreCoupePlans_OrdreCoupePlanId",
                        column: x => x.OrdreCoupePlanId,
                        principalTable: "OrdreCoupePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupeMatieres_OrdreCoupeId",
                table: "OrdreCoupeMatieres",
                column: "OrdreCoupeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupeOccurrences_OrdreCoupePlanId_Taille",
                table: "OrdreCoupeOccurrences",
                columns: new[] { "OrdreCoupePlanId", "Taille" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupePlans_MatelasId",
                table: "OrdreCoupePlans",
                column: "MatelasId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupePlans_OrdreCoupeId_Index",
                table: "OrdreCoupePlans",
                columns: new[] { "OrdreCoupeId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupeTailles_OrdreCoupeId_Index",
                table: "OrdreCoupeTailles",
                columns: new[] { "OrdreCoupeId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdreCoupeTailles_OrdreCoupeId_Libelle",
                table: "OrdreCoupeTailles",
                columns: new[] { "OrdreCoupeId", "Libelle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdresCoupe_ChaineProductionId",
                table: "OrdresCoupe",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresCoupe_CommandeClientId_Modele_Couleur",
                table: "OrdresCoupe",
                columns: new[] { "CommandeClientId", "Modele", "Couleur" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdreCoupeMatieres");

            migrationBuilder.DropTable(
                name: "OrdreCoupeOccurrences");

            migrationBuilder.DropTable(
                name: "OrdreCoupeTailles");

            migrationBuilder.DropTable(
                name: "OrdreCoupePlans");

            migrationBuilder.DropTable(
                name: "OrdresCoupe");
        }
    }
}
