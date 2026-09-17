using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdresFabrication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrdreFabricationId",
                table: "LotCoupes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrdresFabrication",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    NumeroOF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresFabrication", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresFabrication_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdresFabrication_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdresFabricationEtiquettes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreFabricationId = table.Column<int>(type: "integer", nullable: false),
                    FournitureCommandeLigneId = table.Column<int>(type: "integer", nullable: true),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QuantiteEtiquettes = table.Column<int>(type: "integer", nullable: false),
                    DateImpression = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresFabricationEtiquettes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationEtiquettes_FournitureCommandesLignes_Fourn~",
                        column: x => x.FournitureCommandeLigneId,
                        principalTable: "FournitureCommandesLignes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationEtiquettes_OrdresFabrication_OrdreFabricat~",
                        column: x => x.OrdreFabricationId,
                        principalTable: "OrdresFabrication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdresFabricationTailles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreFabricationId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantite = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresFabricationTailles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationTailles_OrdresFabrication_OrdreFabrication~",
                        column: x => x.OrdreFabricationId,
                        principalTable: "OrdresFabrication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_CommandeId_OrdreFabricationId",
                table: "LotCoupes",
                columns: new[] { "CommandeId", "OrdreFabricationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_OrdreFabricationId",
                table: "LotCoupes",
                column: "OrdreFabricationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabrication_ChaineProductionId",
                table: "OrdresFabrication",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabrication_CommandeId_NumeroOF",
                table: "OrdresFabrication",
                columns: new[] { "CommandeId", "NumeroOF" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationEtiquettes_FournitureCommandeLigneId",
                table: "OrdresFabricationEtiquettes",
                column: "FournitureCommandeLigneId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationEtiquettes_OrdreFabricationId",
                table: "OrdresFabricationEtiquettes",
                column: "OrdreFabricationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationTailles_OrdreFabricationId",
                table: "OrdresFabricationTailles",
                column: "OrdreFabricationId");

            migrationBuilder.AddForeignKey(
                name: "FK_LotCoupes_OrdresFabrication_OrdreFabricationId",
                table: "LotCoupes",
                column: "OrdreFabricationId",
                principalTable: "OrdresFabrication",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotCoupes_OrdresFabrication_OrdreFabricationId",
                table: "LotCoupes");

            migrationBuilder.DropTable(
                name: "OrdresFabricationEtiquettes");

            migrationBuilder.DropTable(
                name: "OrdresFabricationTailles");

            migrationBuilder.DropTable(
                name: "OrdresFabrication");

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_CommandeId_OrdreFabricationId",
                table: "LotCoupes");

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_OrdreFabricationId",
                table: "LotCoupes");

            migrationBuilder.DropColumn(
                name: "OrdreFabricationId",
                table: "LotCoupes");
        }
    }
}
