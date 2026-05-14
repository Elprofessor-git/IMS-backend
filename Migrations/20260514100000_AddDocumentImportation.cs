using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentImportation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentsImportation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportationId = table.Column<int>(type: "integer", nullable: false),
                    NomFichier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CheminFichier = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TypeFichier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TailleOctets = table.Column<long>(type: "bigint", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AjoutePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsImportation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentsImportation_Importations_ImportationId",
                        column: x => x.ImportationId,
                        principalTable: "Importations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsImportation_ImportationId",
                table: "DocumentsImportation",
                column: "ImportationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentsImportation");
        }
    }
}
