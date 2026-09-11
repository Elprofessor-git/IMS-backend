using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionEtSuiviFourniture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matelas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroMatelas = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateMatelas = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PiecePliage = table.Column<int>(type: "integer", nullable: false),
                    CoupeEstimee = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matelas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChainesProduction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TypeChaine = table.Column<string>(type: "text", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainesProduction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FournitureCommandesLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Portee = table.Column<string>(type: "text", nullable: false),
                    DesignationSpecifique = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QuantiteFourniture = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Unite = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FournitureCommandesLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FournitureCommandesLignes_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FournitureCommandesLignes_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvoisFourniture",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeFournitureLigneId = table.Column<int>(type: "integer", nullable: false),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    QuantiteEnvoyee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ForcerDepassement = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvoisFourniture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvoisFourniture_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnvoisFourniture_FournitureCommandesLignes_CommandeFournitureLigneId",
                        column: x => x.CommandeFournitureLigneId,
                        principalTable: "FournitureCommandesLignes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionsFourniture",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeFournitureLigneId = table.Column<int>(type: "integer", nullable: false),
                    QuantiteRecue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DateReception = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionsFourniture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptionsFourniture_FournitureCommandesLignes_CommandeFournitureLigneId",
                        column: x => x.CommandeFournitureLigneId,
                        principalTable: "FournitureCommandesLignes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matelas_NumeroMatelas",
                table: "Matelas",
                column: "NumeroMatelas",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChainesProduction_Nom",
                table: "ChainesProduction",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FournitureCommandesLignes_ArticleId",
                table: "FournitureCommandesLignes",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_FournitureCommandesLignes_CommandeId_ArticleId_Taille",
                table: "FournitureCommandesLignes",
                columns: new[] { "CommandeId", "ArticleId", "Taille" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvoisFourniture_ChaineProductionId",
                table: "EnvoisFourniture",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvoisFourniture_CommandeFournitureLigneId_ChaineProductionId",
                table: "EnvoisFourniture",
                columns: new[] { "CommandeFournitureLigneId", "ChaineProductionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionsFourniture_CommandeFournitureLigneId",
                table: "ReceptionsFourniture",
                column: "CommandeFournitureLigneId");

            migrationBuilder.AddColumn<int>(
                name: "ArticleParentId",
                table: "Articles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Taille",
                table: "Articles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ParentEnfant",
                table: "Articles",
                columns: new[] { "ArticleParentId", "Taille" });

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Articles_ArticleParentId",
                table: "Articles",
                column: "ArticleParentId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<int>(
                name: "MatelasId",
                table: "LotCoupes",
                type: "integer",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_CommandeId",
                table: "LotCoupes");

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_MatelasId",
                table: "LotCoupes",
                column: "MatelasId");

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_CommandeId_MatelasId",
                table: "LotCoupes",
                columns: new[] { "CommandeId", "MatelasId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LotCoupes_Matelas_MatelasId",
                table: "LotCoupes",
                column: "MatelasId",
                principalTable: "Matelas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddColumn<int>(
                name: "ChaineProductionId",
                table: "LotExports",
                type: "integer",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_LotExports_CommandeId",
                table: "LotExports");

            migrationBuilder.CreateIndex(
                name: "IX_LotExports_ChaineProductionId",
                table: "LotExports",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_LotExports_CommandeId_ChaineProductionId",
                table: "LotExports",
                columns: new[] { "CommandeId", "ChaineProductionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LotExports_ChainesProduction_ChaineProductionId",
                table: "LotExports",
                column: "ChaineProductionId",
                principalTable: "ChainesProduction",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotExports_ChainesProduction_ChaineProductionId",
                table: "LotExports");

            migrationBuilder.DropForeignKey(
                name: "FK_LotCoupes_Matelas_MatelasId",
                table: "LotCoupes");

            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Articles_ArticleParentId",
                table: "Articles");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceptionsFourniture_FournitureCommandesLignes_CommandeFournitureLigneId",
                table: "ReceptionsFourniture");

            migrationBuilder.DropForeignKey(
                name: "FK_EnvoisFourniture_FournitureCommandesLignes_CommandeFournitureLigneId",
                table: "EnvoisFourniture");

            migrationBuilder.DropForeignKey(
                name: "FK_EnvoisFourniture_ChainesProduction_ChaineProductionId",
                table: "EnvoisFourniture");

            migrationBuilder.DropForeignKey(
                name: "FK_FournitureCommandesLignes_CommandesClients_CommandeId",
                table: "FournitureCommandesLignes");

            migrationBuilder.DropForeignKey(
                name: "FK_FournitureCommandesLignes_Articles_ArticleId",
                table: "FournitureCommandesLignes");

            migrationBuilder.DropIndex(
                name: "IX_LotExports_CommandeId_ChaineProductionId",
                table: "LotExports");

            migrationBuilder.DropIndex(
                name: "IX_LotExports_ChaineProductionId",
                table: "LotExports");

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_CommandeId_MatelasId",
                table: "LotCoupes");

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_MatelasId",
                table: "LotCoupes");

            migrationBuilder.DropIndex(
                name: "IX_Articles_ParentEnfant",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_ReceptionsFourniture_CommandeFournitureLigneId",
                table: "ReceptionsFourniture");

            migrationBuilder.DropIndex(
                name: "IX_EnvoisFourniture_CommandeFournitureLigneId_ChaineProductionId",
                table: "EnvoisFourniture");

            migrationBuilder.DropIndex(
                name: "IX_EnvoisFourniture_ChaineProductionId",
                table: "EnvoisFourniture");

            migrationBuilder.DropIndex(
                name: "IX_FournitureCommandesLignes_CommandeId_ArticleId_Taille",
                table: "FournitureCommandesLignes");

            migrationBuilder.DropIndex(
                name: "IX_FournitureCommandesLignes_ArticleId",
                table: "FournitureCommandesLignes");

            migrationBuilder.DropIndex(
                name: "IX_ChainesProduction_Nom",
                table: "ChainesProduction");

            migrationBuilder.DropIndex(
                name: "IX_Matelas_NumeroMatelas",
                table: "Matelas");

            migrationBuilder.DropColumn(
                name: "ChaineProductionId",
                table: "LotExports");

            migrationBuilder.DropColumn(
                name: "MatelasId",
                table: "LotCoupes");

            migrationBuilder.DropColumn(
                name: "Taille",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ArticleParentId",
                table: "Articles");

            migrationBuilder.CreateIndex(
                name: "IX_LotExports_CommandeId",
                table: "LotExports",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_CommandeId",
                table: "LotCoupes",
                column: "CommandeId");

            migrationBuilder.DropTable(
                name: "ReceptionsFourniture");

            migrationBuilder.DropTable(
                name: "EnvoisFourniture");

            migrationBuilder.DropTable(
                name: "FournitureCommandesLignes");

            migrationBuilder.DropTable(
                name: "ChainesProduction");

            migrationBuilder.DropTable(
                name: "Matelas");
        }
    }
}