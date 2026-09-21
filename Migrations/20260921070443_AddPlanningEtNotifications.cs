using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningEtNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeutGererPlanning",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirPlanning",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlanningEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChaineProductionId = table.Column<int>(type: "integer", nullable: true),
                    DateSamedi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NumeroCommande = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantite = table.Column<int>(type: "integer", nullable: true),
                    EstLivree = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningEntries_ChainesProduction_ChaineProductionId",
                        column: x => x.ChaineProductionId,
                        principalTable: "ChainesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UtilisateurId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DateNotification = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstLivree = table.Column<bool>(type: "boolean", nullable: false),
                    PlanningEntryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_PlanningEntries_PlanningEntryId",
                        column: x => x.PlanningEntryId,
                        principalTable: "PlanningEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PlanningEntryId",
                table: "Notifications",
                column: "PlanningEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningEntries_ChaineProductionId_DateSamedi_NumeroCommande",
                table: "PlanningEntries",
                columns: new[] { "ChaineProductionId", "DateSamedi", "NumeroCommande" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PlanningEntries");

            migrationBuilder.DropColumn(
                name: "PeutGererPlanning",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirPlanning",
                table: "Role");
        }
    }
}
