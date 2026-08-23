using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMarque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommandesClients_Marques_MarqueId",
                table: "CommandesClients");

            migrationBuilder.DropIndex(
                name: "IX_CommandesClients_MarqueId",
                table: "CommandesClients");

            migrationBuilder.DropColumn(
                name: "MarqueId",
                table: "CommandesClients");

            migrationBuilder.DropTable(
                name: "Marques");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Marques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstActive = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlateformeId = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.AddColumn<int>(
                name: "MarqueId",
                table: "CommandesClients",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marques_PlateformeId",
                table: "Marques",
                column: "PlateformeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandesClients_MarqueId",
                table: "CommandesClients",
                column: "MarqueId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommandesClients_Marques_MarqueId",
                table: "CommandesClients",
                column: "MarqueId",
                principalTable: "Marques",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
