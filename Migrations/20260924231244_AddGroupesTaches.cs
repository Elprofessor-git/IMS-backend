using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupesTaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupeTacheId",
                table: "TachesProduction",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupesTaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupesTaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupesTachesLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupeTacheId = table.Column<int>(type: "integer", nullable: false),
                    Titre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EquipeAssignee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponsableAssigne = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priorite = table.Column<int>(type: "integer", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    DureeEstimeeHeures = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupesTachesLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupesTachesLignes_GroupesTaches_GroupeTacheId",
                        column: x => x.GroupeTacheId,
                        principalTable: "GroupesTaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TachesProduction_GroupeTacheId",
                table: "TachesProduction",
                column: "GroupeTacheId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupesTachesLignes_GroupeTacheId_Ordre",
                table: "GroupesTachesLignes",
                columns: new[] { "GroupeTacheId", "Ordre" });

            migrationBuilder.AddForeignKey(
                name: "FK_TachesProduction_GroupesTaches_GroupeTacheId",
                table: "TachesProduction",
                column: "GroupeTacheId",
                principalTable: "GroupesTaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TachesProduction_GroupesTaches_GroupeTacheId",
                table: "TachesProduction");

            migrationBuilder.DropTable(
                name: "GroupesTachesLignes");

            migrationBuilder.DropTable(
                name: "GroupesTaches");

            migrationBuilder.DropIndex(
                name: "IX_TachesProduction_GroupeTacheId",
                table: "TachesProduction");

            migrationBuilder.DropColumn(
                name: "GroupeTacheId",
                table: "TachesProduction");
        }
    }
}
