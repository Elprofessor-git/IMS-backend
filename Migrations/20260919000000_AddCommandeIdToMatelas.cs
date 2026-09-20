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
            // Colonne à rattachement optionnel — PAS de NOT NULL, PAS de DEFAULT 0 :
            // les matelas historiques partagés entre plusieurs commandes restent
            // valides avec CommandeId = null (principe additif).
            migrationBuilder.AddColumn<int>(
                name: "CommandeId",
                table: "Matelas",
                type: "integer",
                nullable: true);

            // Backfill BEST-EFFORT, uniquement pour les matelas dont toutes les coupes
            // relèvent d'UNE SEULE et même commande (COUNT(DISTINCT) = 1).
            // Les matelas dont les coupes portent sur plusieurs commandes différentes
            // (ambiguïté réelle), ou sans aucune coupe, restent à NULL.
            migrationBuilder.Sql(
                """
                UPDATE "Matelas" m
                SET "CommandeId" = x."CommandeId"
                FROM (
                    SELECT lc."MatelasId" AS "MatelasId",
                           MIN(lc."CommandeId") AS "CommandeId"
                    FROM "LotCoupes" lc
                    WHERE lc."MatelasId" IS NOT NULL
                      AND lc."CommandeId" IS NOT NULL
                    GROUP BY lc."MatelasId"
                    HAVING COUNT(DISTINCT lc."CommandeId") = 1
                ) x
                WHERE x."MatelasId" = m."Id";
                """);

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
                onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.DropColumn(
                name: "CommandeId",
                table: "Matelas");
        }
    }
}