using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class ImportationOriginePlateformeEnTete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // L'origine d'une importation (Fournisseur direct ou Plateforme qui regroupe
            // plusieurs fournisseurs et envoie tout en un seul envoi) est un attribut de
            // l'IMPORTATION (en-tête), jamais de la ligne. Le champ TypeOrigine par ligne
            // (Fournisseur/ClientCMT) n'a donc plus de raison d'être et est supprimé.
            migrationBuilder.DropColumn(
                name: "TypeOrigine",
                table: "LignesImportation");

            // Source alternative à FournisseurId sur l'en-tête : PlateformeId (exclusifs,
            // contrôlé applicativement dans ImportationController).
            migrationBuilder.AddColumn<int>(
                name: "PlateformeId",
                table: "Importations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Importations_PlateformeId",
                table: "Importations",
                column: "PlateformeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Importations_Plateformes_PlateformeId",
                table: "Importations",
                column: "PlateformeId",
                principalTable: "Plateformes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Importations_Plateformes_PlateformeId",
                table: "Importations");

            migrationBuilder.DropIndex(
                name: "IX_Importations_PlateformeId",
                table: "Importations");

            migrationBuilder.DropColumn(
                name: "PlateformeId",
                table: "Importations");

            migrationBuilder.AddColumn<string>(
                name: "TypeOrigine",
                table: "LignesImportation",
                type: "text",
                nullable: false,
                defaultValue: "Fournisseur");
        }
    }
}
