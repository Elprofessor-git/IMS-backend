using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPartageSecurise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeutPartagerLiens",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShareLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    IsStockLibreAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Sections = table.Column<int>(type: "integer", nullable: false),
                    FiltersJson = table.Column<string>(type: "text", nullable: true),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareLinks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareLinkAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShareLinkId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinkAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareLinkAccess_ShareLinks_ShareLinkId",
                        column: x => x.ShareLinkId,
                        principalTable: "ShareLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinkAccess_ShareLinkId_Date",
                table: "ShareLinkAccess",
                columns: new[] { "ShareLinkId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_CreatedByUserId",
                table: "ShareLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_ExpiresAtUtc",
                table: "ShareLinks",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_ScopeType_ScopeId",
                table: "ShareLinks",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_TokenHash",
                table: "ShareLinks",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShareLinkAccess");

            migrationBuilder.DropTable(
                name: "ShareLinks");

            migrationBuilder.DropColumn(
                name: "PeutPartagerLiens",
                table: "Role");
        }
    }
}
