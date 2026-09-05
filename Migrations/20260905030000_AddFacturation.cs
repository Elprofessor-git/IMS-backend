using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrixFacon",
                table: "CommandesClients",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PeutGererFactures",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirFactures",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Factures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroFacture = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateFacture = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModePaiement = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Rib = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Iban = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModeLivraison = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NombreColis = table.Column<int>(type: "integer", nullable: true),
                    PoidsNetKg = table.Column<decimal>(type: "numeric", nullable: true),
                    PoidsBrutKg = table.Column<decimal>(type: "numeric", nullable: true),
                    VolumeM3 = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factures_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactureCommandesLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FactureId = table.Column<int>(type: "integer", nullable: false),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    Modele = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantite = table.Column<int>(type: "integer", nullable: false),
                    PrixUnitaireFacon = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MontantLigne = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactureCommandesLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactureCommandesLignes_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactureCommandesLignes_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactureCommandesLignes_CommandeId",
                table: "FactureCommandesLignes",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_FactureCommandesLignes_FactureId",
                table: "FactureCommandesLignes",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_ClientId",
                table: "Factures",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_NumeroFacture",
                table: "Factures",
                column: "NumeroFacture",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactureCommandesLignes");

            migrationBuilder.DropTable(
                name: "Factures");

            migrationBuilder.DropColumn(
                name: "PeutVoirFactures",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutGererFactures",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PrixFacon",
                table: "CommandesClients");
        }
    }
}