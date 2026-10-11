using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdreCoupeIdSurMatelasEtLotCoupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrdreCoupeId",
                table: "Matelas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrdreCoupeId",
                table: "LotCoupes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matelas_OrdreCoupeId",
                table: "Matelas",
                column: "OrdreCoupeId");

            migrationBuilder.CreateIndex(
                name: "IX_LotCoupes_OrdreCoupeId",
                table: "LotCoupes",
                column: "OrdreCoupeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LotCoupes_OrdresCoupe_OrdreCoupeId",
                table: "LotCoupes",
                column: "OrdreCoupeId",
                principalTable: "OrdresCoupe",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Matelas_OrdresCoupe_OrdreCoupeId",
                table: "Matelas",
                column: "OrdreCoupeId",
                principalTable: "OrdresCoupe",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotCoupes_OrdresCoupe_OrdreCoupeId",
                table: "LotCoupes");

            migrationBuilder.DropForeignKey(
                name: "FK_Matelas_OrdresCoupe_OrdreCoupeId",
                table: "Matelas");

            migrationBuilder.DropIndex(
                name: "IX_Matelas_OrdreCoupeId",
                table: "Matelas");

            migrationBuilder.DropIndex(
                name: "IX_LotCoupes_OrdreCoupeId",
                table: "LotCoupes");

            migrationBuilder.DropColumn(
                name: "OrdreCoupeId",
                table: "Matelas");

            migrationBuilder.DropColumn(
                name: "OrdreCoupeId",
                table: "LotCoupes");
        }
    }
}
