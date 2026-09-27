using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleCourrielsGmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeutGererCourriels",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirCourriels",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GmailConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GmailAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    GoogleUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    GrantedScopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HistoryId = table.Column<string>(type: "text", nullable: true),
                    WatchExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmailConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GmailConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GmailConnectionId = table.Column<int>(type: "integer", nullable: false),
                    GmailMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GmailThreadId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    From = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    To = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Cc = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Rfc822MessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    Snippet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsStarred = table.Column<bool>(type: "boolean", nullable: false),
                    HasAttachments = table.Column<bool>(type: "boolean", nullable: false),
                    LabelsJson = table.Column<string>(type: "text", nullable: true),
                    IsSynchronized = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedTaskId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GmailMessages_GmailConnections_GmailConnectionId",
                        column: x => x.GmailConnectionId,
                        principalTable: "GmailConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GmailMessages_TachesProduction_CreatedTaskId",
                        column: x => x.CreatedTaskId,
                        principalTable: "TachesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmailAiAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GmailMessageId = table.Column<int>(type: "integer", nullable: false),
                    IsTask = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    SuggestedTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SuggestedDescription = table.Column<string>(type: "text", nullable: true),
                    SuggestedPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SuggestedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuggestedAssigneeUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AnalysisJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAiAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAiAnalyses_AspNetUsers_SuggestedAssigneeUserId",
                        column: x => x.SuggestedAssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailAiAnalyses_GmailMessages_GmailMessageId",
                        column: x => x.GmailMessageId,
                        principalTable: "GmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailAiReponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GmailMessageId = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GmailDraftId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GmailSentMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAiReponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAiReponses_GmailMessages_GmailMessageId",
                        column: x => x.GmailMessageId,
                        principalTable: "GmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAiAnalyses_GmailMessageId_CreatedAt",
                table: "EmailAiAnalyses",
                columns: new[] { "GmailMessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAiAnalyses_SuggestedAssigneeUserId",
                table: "EmailAiAnalyses",
                column: "SuggestedAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAiReponses_GmailMessageId_GeneratedAt",
                table: "EmailAiReponses",
                columns: new[] { "GmailMessageId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GmailConnections_UserId_GoogleUserId",
                table: "GmailConnections",
                columns: new[] { "UserId", "GoogleUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GmailConnections_UserId_IsActive",
                table: "GmailConnections",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GmailMessages_CreatedTaskId",
                table: "GmailMessages",
                column: "CreatedTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GmailMessages_GmailConnectionId_GmailMessageId",
                table: "GmailMessages",
                columns: new[] { "GmailConnectionId", "GmailMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GmailMessages_GmailConnectionId_ReceivedAt",
                table: "GmailMessages",
                columns: new[] { "GmailConnectionId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAiAnalyses");

            migrationBuilder.DropTable(
                name: "EmailAiReponses");

            migrationBuilder.DropTable(
                name: "GmailMessages");

            migrationBuilder.DropTable(
                name: "GmailConnections");

            migrationBuilder.DropColumn(
                name: "PeutGererCourriels",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirCourriels",
                table: "Role");
        }
    }
}
