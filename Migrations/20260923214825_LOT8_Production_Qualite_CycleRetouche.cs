using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class LOT8_Production_Qualite_CycleRetouche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeutGererProduction",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutGererQualite",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirProduction",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirQualite",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EstSousTraitant",
                table: "ChainesProduction",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "DefautCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefautCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrdresFabricationEtapes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreFabricationId = table.Column<int>(type: "integer", nullable: false),
                    TypeEtape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    PlanningEntryId = table.Column<int>(type: "integer", nullable: true),
                    DateDebutPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateDebutReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TempsTheoriqueHeures = table.Column<decimal>(type: "numeric", nullable: false),
                    TempsReelHeures = table.Column<decimal>(type: "numeric", nullable: false),
                    ResponsableAssigne = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresFabricationEtapes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationEtapes_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationEtapes_OrdresFabrication_OrdreFabricationId",
                        column: x => x.OrdreFabricationId,
                        principalTable: "OrdresFabrication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrdresFabricationEtapes_PlanningEntries_PlanningEntryId",
                        column: x => x.PlanningEntryId,
                        principalTable: "PlanningEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ControleQualiteDefautLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControleQualiteId = table.Column<int>(type: "integer", nullable: false),
                    DefautCodeId = table.Column<int>(type: "integer", nullable: false),
                    Quantite = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControleQualiteDefautLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControleQualiteDefautLignes_DefautCodes_DefautCodeId",
                        column: x => x.DefautCodeId,
                        principalTable: "DefautCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlesQualite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdreFabricationId = table.Column<int>(type: "integer", nullable: true),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    TypeControle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ControleParentId = table.Column<int>(type: "integer", nullable: true),
                    EnvoiRetoucheId = table.Column<int>(type: "integer", nullable: true),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantiteControlee = table.Column<int>(type: "integer", nullable: false),
                    QuantiteAcceptee = table.Column<int>(type: "integer", nullable: false),
                    QuantiteRetouche = table.Column<int>(type: "integer", nullable: false),
                    QuantiteRebut = table.Column<int>(type: "integer", nullable: false),
                    DateControle = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlesQualite", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlesQualite_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ControlesQualite_ControlesQualite_ControleParentId",
                        column: x => x.ControleParentId,
                        principalTable: "ControlesQualite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlesQualite_OrdresFabrication_OrdreFabricationId",
                        column: x => x.OrdreFabricationId,
                        principalTable: "OrdresFabrication",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EnvoisRetouche",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControleQualiteId = table.Column<int>(type: "integer", nullable: false),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: false),
                    QuantiteRenvoyee = table.Column<int>(type: "integer", nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvoisRetouche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvoisRetouche_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnvoisRetouche_ControlesQualite_ControleQualiteId",
                        column: x => x.ControleQualiteId,
                        principalTable: "ControlesQualite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControleQualiteDefautLignes_ControleQualiteId",
                table: "ControleQualiteDefautLignes",
                column: "ControleQualiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ControleQualiteDefautLignes_DefautCodeId",
                table: "ControleQualiteDefautLignes",
                column: "DefautCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlesQualite_ChaineProductionId",
                table: "ControlesQualite",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlesQualite_ControleParentId",
                table: "ControlesQualite",
                column: "ControleParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlesQualite_EnvoiRetoucheId",
                table: "ControlesQualite",
                column: "EnvoiRetoucheId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlesQualite_OrdreFabricationId_ChaineProductionId_Tail~",
                table: "ControlesQualite",
                columns: new[] { "OrdreFabricationId", "ChaineProductionId", "Taille" });

            migrationBuilder.CreateIndex(
                name: "IX_DefautCodes_Code",
                table: "DefautCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvoisRetouche_ChaineProductionId",
                table: "EnvoisRetouche",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvoisRetouche_ControleQualiteId",
                table: "EnvoisRetouche",
                column: "ControleQualiteId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationEtapes_ChaineProductionId",
                table: "OrdresFabricationEtapes",
                column: "ChaineProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationEtapes_OrdreFabricationId",
                table: "OrdresFabricationEtapes",
                column: "OrdreFabricationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresFabricationEtapes_PlanningEntryId",
                table: "OrdresFabricationEtapes",
                column: "PlanningEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ControleQualiteDefautLignes_ControlesQualite_ControleQualit~",
                table: "ControleQualiteDefautLignes",
                column: "ControleQualiteId",
                principalTable: "ControlesQualite",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ControlesQualite_EnvoisRetouche_EnvoiRetoucheId",
                table: "ControlesQualite",
                column: "EnvoiRetoucheId",
                principalTable: "EnvoisRetouche",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Données (deployé de façon idempotente, cf. sidecar .sql) ──

            // Rôles administrateurs : les 4 permissions des modules Production/Qualité.
            migrationBuilder.Sql(
                "UPDATE \"Role\" SET " +
                "\"PeutVoirProduction\" = true, \"PeutGererProduction\" = true, " +
                "\"PeutVoirQualite\" = true, \"PeutGererQualite\" = true " +
                "WHERE \"EstAdministrateur\" = true;");

            // Référentiel initial des codes défaut (module Qualité).
            migrationBuilder.Sql(
                @"INSERT INTO ""DefautCodes"" (""Code"", ""Libelle"", ""EstActif"") VALUES
                    ('TIRAGE', 'Tirage / couleur non conforme', true),
                    ('COUTURE', 'Couture défectueuse (points sautés, fil, ourlet)', true),
                    ('DIMENSION', 'Dimension hors tolérance', true),
                    ('TRACE', 'Trace / tache irréversible', true),
                    ('TISSU', 'Tissu défectueux (trame, trou, défaut matière)', true),
                    ('MONTAGE', 'Montage / garnissage défectueux', true),
                    ('ETIQUETTE', 'Étiquette manquante ou erronée', true),
                    ('AUTRE', 'Autre défaut (préciser en note)', true)
                ON CONFLICT (""Code"") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnvoisRetouche_ControlesQualite_ControleQualiteId",
                table: "EnvoisRetouche");

            migrationBuilder.DropTable(
                name: "ControleQualiteDefautLignes");

            migrationBuilder.DropTable(
                name: "OrdresFabricationEtapes");

            migrationBuilder.DropTable(
                name: "DefautCodes");

            migrationBuilder.DropTable(
                name: "ControlesQualite");

            migrationBuilder.DropTable(
                name: "EnvoisRetouche");

            migrationBuilder.DropColumn(
                name: "PeutGererProduction",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutGererQualite",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirProduction",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirQualite",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "EstSousTraitant",
                table: "ChainesProduction");
        }
    }
}
