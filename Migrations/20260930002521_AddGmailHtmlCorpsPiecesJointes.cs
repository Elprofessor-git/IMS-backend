using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailHtmlCorpsPiecesJointes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyHtml",
                table: "GmailMessages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GmailAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GmailMessageId = table.Column<int>(type: "integer", nullable: false),
                    GmailAttachmentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsInline = table.Column<bool>(type: "boolean", nullable: false),
                    ContentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GmailAttachments_GmailMessages_GmailMessageId",
                        column: x => x.GmailMessageId,
                        principalTable: "GmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GmailAttachments_GmailMessageId_GmailAttachmentId",
                table: "GmailAttachments",
                columns: new[] { "GmailMessageId", "GmailAttachmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GmailAttachments");

            migrationBuilder.DropColumn(
                name: "BodyHtml",
                table: "GmailMessages");
        }
    }
}
