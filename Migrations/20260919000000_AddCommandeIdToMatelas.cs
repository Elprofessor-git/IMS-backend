using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandeIdToMatelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandeId",
                table: "Matelas",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH lots AS (
                    SELECT DISTINCT ON ("MatelasId")
                           "MatelasId", "CommandeId"
                    FROM "LotCoupes"
                    WHERE "MatelasId" IS NOT NULL AND "CommandeId" IS NOT NULL
                    ORDER BY "MatelasId", "DateCoupe" DESC NULLS LAST
                )
                UPDATE "Matelas" m
                SET "CommandeId" = lots."CommandeId"
                FROM lots
                WHERE lots."MatelasId" = m."Id"
                  AND m."CommandeId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CommandeId",
                table: "Matelas",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matelas_CommandeId",
                table: "Matelas",
                column: "CommandeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matelas_CommandesClients_CommandeId",
                table: "Matelas",
                column: "CommandeId",
                principalTable: "CommandesClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matelas_CommandesClients_CommandeId",
                table: "Matelas");

            migrationBuilder.DropIndex(
                name: "IX_Matelas_CommandeId",
                table: "Matelas");

            migrationBuilder.AlterColumn<int>(
                name: "CommandeId",
                table: "Matelas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.DropColumn(
                name: "CommandeId",
                table: "Matelas");
        }
    }
}
