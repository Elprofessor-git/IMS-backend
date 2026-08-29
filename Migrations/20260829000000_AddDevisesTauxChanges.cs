using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDevisesTauxChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devises",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Symbole = table.Column<string>(type: "text", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devises", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TauxChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviseCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateEffective = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Taux = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TauxChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TauxChanges_Devises_DeviseCode",
                        column: x => x.DeviseCode,
                        principalTable: "Devises",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TauxChanges_DeviseCode_DateEffective",
                table: "TauxChanges",
                columns: new[] { "DeviseCode", "DateEffective" });

            // Seed : devise de référence TND + EUR (devises actives au démarrage)
            migrationBuilder.InsertData(
                table: "Devises",
                columns: new[] { "Code", "Nom", "Symbole", "EstActif" },
                values: new object[,]
                {
                    { "TND", "Dinar Tunisien", "DT", true },
                    { "EUR", "Euro", "€", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TauxChanges");

            migrationBuilder.DropTable(
                name: "Devises");
        }
    }
}
