using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class ReceptionPartielle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuantiteRecue",
                table: "LignesAchat",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StatutLigne",
                table: "LignesAchat",
                type: "text",
                nullable: false,
                defaultValue: "EnAttente");

            migrationBuilder.AddColumn<decimal>(
                name: "QuantiteRecue",
                table: "LignesImportation",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StatutLigne",
                table: "LignesImportation",
                type: "text",
                nullable: false,
                defaultValue: "EnAttente");

            // Backfill: lignes d'achats Livrés → QuantiteRecue = Quantite, StatutLigne = Complete
            migrationBuilder.Sql(@"
                UPDATE ""LignesAchat"" la
                SET ""QuantiteRecue"" = la.""Quantite"",
                    ""StatutLigne"" = 'Complete'
                FROM ""Achats"" a
                WHERE la.""AchatId"" = a.""Id""
                  AND a.""Statut"" = 'Livre'
                  AND la.""StatutLigne"" = 'EnAttente'");

            // Backfill: lignes d'importations Recues → QuantiteRecue = Quantite, StatutLigne = Complete
            migrationBuilder.Sql(@"
                UPDATE ""LignesImportation"" li
                SET ""QuantiteRecue"" = li.""Quantite"",
                    ""StatutLigne"" = 'Complete'
                FROM ""Importations"" i
                WHERE li.""ImportationId"" = i.""Id""
                  AND i.""Statut"" = 'Recue'
                  AND li.""StatutLigne"" = 'EnAttente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantiteRecue",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "StatutLigne",
                table: "LignesAchat");

            migrationBuilder.DropColumn(
                name: "QuantiteRecue",
                table: "LignesImportation");

            migrationBuilder.DropColumn(
                name: "StatutLigne",
                table: "LignesImportation");
        }
    }
}
