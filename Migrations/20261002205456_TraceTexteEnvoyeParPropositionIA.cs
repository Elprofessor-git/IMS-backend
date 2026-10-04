using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class TraceTexteEnvoyeParPropositionIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SentBody",
                table: "EmailAiReponses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentSubject",
                table: "EmailAiReponses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentBody",
                table: "EmailAiReponses");

            migrationBuilder.DropColumn(
                name: "SentSubject",
                table: "EmailAiReponses");
        }
    }
}
