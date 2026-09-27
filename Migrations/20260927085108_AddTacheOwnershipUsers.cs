using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTacheOwnershipUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserId",
                table: "TachesProduction",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "TachesProduction",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PeutAssignerTaches",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutVoirToutesTaches",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TachesProduction_AssignedToUserId",
                table: "TachesProduction",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TachesProduction_CreatedByUserId",
                table: "TachesProduction",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TachesProduction_AspNetUsers_AssignedToUserId",
                table: "TachesProduction",
                column: "AssignedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TachesProduction_AspNetUsers_CreatedByUserId",
                table: "TachesProduction",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Stratégie de migration des données historiques ──────────────────────
            // Les tâches antérieures n'ont aucun propriétaire. On tente une
            // correspondance SAUVE : le libellé legacy doit correspondre EXACTEMENT
            // (et de façon UNIQUE) à un utilisateur IMS actif. En cas d'ambiguïté
            // (plusieurs utilisateurs homonymes) ou d'absence de correspondance, la FK
            // reste NULL : aucune tâche n'est attribuée arbitrairement.
            //
            // Conséquence d'un reliquat NULL : ces tâches ne sont visibles que des rôles
            // disposant de PeutVoirToutesTaches. C'est le choix conservateur — préfère
            // une tâche temporairement invisible à une tâche attribuée au mauvais compte.
            migrationBuilder.Sql(@"
UPDATE ""TachesProduction"" tp
SET ""CreatedByUserId"" = u.""Id""
FROM ""AspNetUsers"" u
WHERE tp.""CreatedByUserId"" IS NULL
  AND tp.""CreePar"" IS NOT NULL
  AND btrim(tp.""CreePar"") <> ''
  AND u.""EstActif""
  AND (u.""Nom"" = btrim(tp.""CreePar"")
       OR (u.""Prenom"" IS NOT NULL AND btrim(u.""Prenom"" || ' ' || u.""Nom"") = btrim(tp.""CreePar"")))
  AND (
        SELECT count(*)
        FROM ""AspNetUsers"" u2
        WHERE u2.""EstActif""
          AND (u2.""Nom"" = btrim(tp.""CreePar"")
               OR (u2.""Prenom"" IS NOT NULL AND btrim(u2.""Prenom"" || ' ' || u2.""Nom"") = btrim(tp.""CreePar"")))
      ) = 1;");

            migrationBuilder.Sql(@"
UPDATE ""TachesProduction"" tp
SET ""AssignedToUserId"" = u.""Id""
FROM ""AspNetUsers"" u
WHERE tp.""AssignedToUserId"" IS NULL
  AND tp.""ResponsableAssigne"" IS NOT NULL
  AND btrim(tp.""ResponsableAssigne"") <> ''
  AND u.""EstActif""
  AND (u.""Nom"" = btrim(tp.""ResponsableAssigne"")
       OR (u.""Prenom"" IS NOT NULL AND btrim(u.""Prenom"" || ' ' || u.""Nom"") = btrim(tp.""ResponsableAssigne"")))
  AND (
        SELECT count(*)
        FROM ""AspNetUsers"" u2
        WHERE u2.""EstActif""
          AND (u2.""Nom"" = btrim(tp.""ResponsableAssigne"")
               OR (u2.""Prenom"" IS NOT NULL AND btrim(u2.""Prenom"" || ' ' || u2.""Nom"") = btrim(tp.""ResponsableAssigne"")))
      ) = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TachesProduction_AspNetUsers_AssignedToUserId",
                table: "TachesProduction");

            migrationBuilder.DropForeignKey(
                name: "FK_TachesProduction_AspNetUsers_CreatedByUserId",
                table: "TachesProduction");

            migrationBuilder.DropIndex(
                name: "IX_TachesProduction_AssignedToUserId",
                table: "TachesProduction");

            migrationBuilder.DropIndex(
                name: "IX_TachesProduction_CreatedByUserId",
                table: "TachesProduction");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "TachesProduction");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "TachesProduction");

            migrationBuilder.DropColumn(
                name: "PeutAssignerTaches",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutVoirToutesTaches",
                table: "Role");
        }
    }
}
