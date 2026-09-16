using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddParcMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeutGererMachines",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirMachines",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodeMachine = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Marque = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Modele = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumeroSerie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TypeMachine = table.Column<string>(type: "text", nullable: false),
                    DateAcquisition = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Emplacement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterventionsMachines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachineId = table.Column<int>(type: "integer", nullable: false),
                    TypeIntervention = table.Column<string>(type: "text", nullable: false),
                    DateIntervention = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PannneConstatee = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PiecesRemplacees = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CoutIntervention = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DureeImmobilisationHeures = table.Column<int>(type: "integer", nullable: true),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProchaineDateMaintenance = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterventionsMachines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterventionsMachines_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionsMachines_MachineId",
                table: "InterventionsMachines",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_CodeMachine",
                table: "Machines",
                column: "CodeMachine",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterventionsMachines");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropColumn(
                name: "PeutGererMachines",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirMachines",
                table: "Role");
        }
    }
}
