using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsTachesEtEmails : Migration
    {
        /// <summary>
        /// LOT 17 — notifications Tâches et Courriels, SANS table supplémentaire :
        ///   • <c>Type</c> (texte, défaut « Planning ») : origine de la notification, lisible
        ///     en base et alignée sur les autres énumérations stockées en string du projet.
        ///     Les notifications existantes sont conservées et deviennent de type Planning.
        ///   • <c>TacheProductionId</c> / <c>GmailMessageId</c> (FK nullables, SetNull) :
        ///     cibles du lien profond au clic. SetNull => une notification survit à la
        ///     suppression de sa cible, conformément au comportement planning existant.
        ///   • index (UtilisateurId, EstLivree) : requête de la cloche (liste + compteur).
        /// Aucune donnée existante n'est supprimée ni réécrite.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GmailMessageId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TacheProductionId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Planning");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_GmailMessageId",
                table: "Notifications",
                column: "GmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TacheProductionId",
                table: "Notifications",
                column: "TacheProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UtilisateurId_EstLivree",
                table: "Notifications",
                columns: new[] { "UtilisateurId", "EstLivree" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GmailMessages_GmailMessageId",
                table: "Notifications",
                column: "GmailMessageId",
                principalTable: "GmailMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_TachesProduction_TacheProductionId",
                table: "Notifications",
                column: "TacheProductionId",
                principalTable: "TachesProduction",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GmailMessages_GmailMessageId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_TachesProduction_TacheProductionId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_GmailMessageId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TacheProductionId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UtilisateurId_EstLivree",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "GmailMessageId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TacheProductionId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");
        }
    }
}
