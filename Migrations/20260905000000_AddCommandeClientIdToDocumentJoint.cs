using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandeClientIdToDocumentJoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandeClientId",
                table: "DocumentsJoints",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsJoints_CommandeClientId",
                table: "DocumentsJoints",
                column: "CommandeClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentsJoints_CommandesClients_CommandeClientId",
                table: "DocumentsJoints",
                column: "CommandeClientId",
                principalTable: "CommandesClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentsJoints_CommandesClients_CommandeClientId",
                table: "DocumentsJoints");

            migrationBuilder.DropIndex(
                name: "IX_DocumentsJoints_CommandeClientId",
                table: "DocumentsJoints");

            migrationBuilder.DropColumn(
                name: "CommandeClientId",
                table: "DocumentsJoints");
        }
    }
}