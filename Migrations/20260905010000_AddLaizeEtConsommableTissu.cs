using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddLaizeEtConsommableTissu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Laize",
                table: "Articles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstConsommableTissu",
                table: "BomLignes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Laize",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EstConsommableTissu",
                table: "BomLignes");
        }
    }
}