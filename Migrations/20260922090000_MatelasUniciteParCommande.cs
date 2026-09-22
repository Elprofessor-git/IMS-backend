using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <summary>
    /// Unicité du matelas PAR COMMANDE au lieu d'une unicité globale du numéro.
    /// Métier corrigé dans les contrôleurs (MatelasController.Create/Update,
    /// FournitureCommandeController.CreerMatelas) : la garde compare désormais
    /// (CommandeId, NumeroMatelas) en insensibilité casse ET espaces.
    ///
    /// L'index physique traduit la MÊME règle : index unique FONCTIONNEL
    /// PostgreSQL sur ("CommandeId", UPPER(REGEXP_REPLACE("NumeroMatelas", '\s', '', 'g')))
    /// — deux commandes différentes peuvent chacune porter un matelas « M1 »,
    /// mais jamais deux matelas « m1 » / « M 1 » / « M1 » sur la même commande.
    ///
    /// L'index n'est PAS déclaré dans le modèle EF (expression non exprimable en
    /// fluide) : il est géré uniquement par cette migration SQL → aucun drift.
    /// Les lignes historiques (CommandeId NULL, matelas partagés) restent valides :
    /// PostgreSQL traite les NULL comme distincts dans un index unique.
    /// </summary>
    public partial class MatelasUniciteParCommande : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lève l'ancienne contrainte GLOBALE sur le numéro de matelas.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Matelas_NumeroMatelas\";");

            // Nouvelle règle : unique par commande, insensible casse/espaces.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Matelas_CommandeId_NumeroMatelas\" " +
                "ON \"Matelas\" (\"CommandeId\", " +
                "UPPER(REGEXP_REPLACE(\"NumeroMatelas\", '\\s', '', 'g')));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Matelas_CommandeId_NumeroMatelas\";");

            // Restauration de l'ancienne règle globale (annulation seulement).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Matelas_NumeroMatelas\" " +
                "ON \"Matelas\" (\"NumeroMatelas\");");
        }
    }
}