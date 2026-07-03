using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentJoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentsJoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AchatId = table.Column<int>(type: "integer", nullable: true),
                    ImportationId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    NomFichier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TailleOctets = table.Column<long>(type: "bigint", nullable: false),
                    Contenu = table.Column<byte[]>(type: "bytea", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AjoutePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsJoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentsJoints_Achats_AchatId",
                        column: x => x.AchatId,
                        principalTable: "Achats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentsJoints_Importations_ImportationId",
                        column: x => x.ImportationId,
                        principalTable: "Importations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsJoints_AchatId",
                table: "DocumentsJoints",
                column: "AchatId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsJoints_ImportationId",
                table: "DocumentsJoints",
                column: "ImportationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentsJoints");
        }
    }
}
