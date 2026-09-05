using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoupesEtExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotCoupes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantiteCoupee = table.Column<int>(type: "integer", nullable: false),
                    DateCoupe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ForcerDepassement = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotCoupes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotCoupes_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LotExports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeId = table.Column<int>(type: "integer", nullable: false),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantiteExportee = table.Column<int>(type: "integer", nullable: false),
                    DateExport = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ForcerDepassement = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotExports_CommandesClients_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_CommandeId",
                table: "LotCoupes",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_LotExports_CommandeId",
                table: "LotExports",
                column: "CommandeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotCoupes");

            migrationBuilder.DropTable(
                name: "LotExports");
        }
    }
}