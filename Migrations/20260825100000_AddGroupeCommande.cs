using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupeCommande : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupesCommandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupesCommandes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupeCommandeCommandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupeCommandeId = table.Column<int>(type: "integer", nullable: false),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupeCommandeCommandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupeCommandeCommandes_GroupesCommandes_GroupeCommandeId",
                        column: x => x.GroupeCommandeId,
                        principalTable: "GroupesCommandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupeCommandeCommandes_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<int>(
                name: "GroupeCommandeId",
                table: "LignesAchat",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupeCommandeId",
                table: "LignesImportation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupeCommandeId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupeCommandeCommandes_GroupeCommandeId_CommandeClientId",
                table: "GroupeCommandeCommandes",
                columns: new[] { "GroupeCommandeId", "CommandeClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupeCommandeCommandes_CommandeClientId",
                table: "GroupeCommandeCommandes",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_GroupeCommandeId",
                table: "LignesAchat",
                column: "GroupeCommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_GroupeCommandeId",
                table: "LignesImportation",
                column: "GroupeCommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_GroupeCommandeId",
                table: "Stocks",
                column: "GroupeCommandeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LignesAchat_GroupesCommandes_GroupeCommandeId",
                table: "LignesAchat",
                column: "GroupeCommandeId",
                principalTable: "GroupesCommandes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LignesImportation_GroupesCommandes_GroupeCommandeId",
                table: "LignesImportation",
                column: "GroupeCommandeId",
                principalTable: "GroupesCommandes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_GroupesCommandes_GroupeCommandeId",
                table: "Stocks",
                column: "GroupeCommandeId",
                principalTable: "GroupesCommandes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LignesAchat_GroupesCommandes_GroupeCommandeId",
                table: "LignesAchat");

            migrationBuilder.DropForeignKey(
                name: "FK_LignesImportation_GroupesCommandes_GroupeCommandeId",
                table: "LignesImportation");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_GroupesCommandes_GroupeCommandeId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupeCommandeCommandes_GroupesCommandes_GroupeCommandeId",
                table: "GroupeCommandeCommandes");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupeCommandeCommandes_CommandesClients_CommandeClientId",
                table: "GroupeCommandeCommandes");

            migrationBuilder.DropIndex(
                name: "IX_LignesAchat_GroupeCommandeId",
                table: "LignesAchat");

            migrationBuilder.DropIndex(
                name: "IX_LignesImportation_GroupeCommandeId",
                table: "LignesImportation");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_GroupeCommandeId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_GroupeCommandeCommandes_GroupeCommandeId_CommandeClientId",
                table: "GroupeCommandeCommandes");

            migrationBuilder.DropIndex(
                name: "IX_GroupeCommandeCommandes_CommandeClientId",
                table: "GroupeCommandeCommandes");

            migrationBuilder.DropColumn(
                name: "GroupeCommandeId",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "GroupeCommandeId",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "GroupeCommandeId",
                table: "Stocks");

            migrationBuilder.DropTable(
                name: "GroupeCommandeCommandes");

            migrationBuilder.DropTable(
                name: "GroupesCommandes");
        }
    }
}
