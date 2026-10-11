using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Curseur de synchronisation de la Discussion (E1) ─────────────────
            // Une SEQUENCE GLOBALE, partagée par toutes les lignes de MessagesDiscussion.
            // La revision n'est pas un compteur par conversation : c'est un curseur
            // monotone unique permettant à GET /sync de répondre « tout ce dont la
            // revision dépasse mon curseur ». Créée AVANT la table, car la colonne
            // "Revision" s'appuie dessus comme valeur par défaut.
            migrationBuilder.Sql(
                "CREATE SEQUENCE IF NOT EXISTS message_discussion_revision_seq AS bigint START WITH 1 INCREMENT BY 1;");

            migrationBuilder.AddColumn<bool>(
                name: "PeutGererDiscussion",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PeutUtiliserDiscussion",
                table: "Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MessageDiscussionId",
                table: "Notifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstCompteInterne",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Direct"),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstPrive = table.Column<bool>(type: "boolean", nullable: false),
                    CleDirecte = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContexteType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContexteId = table.Column<int>(type: "integer", nullable: true),
                    CreeParUserId = table.Column<string>(type: "text", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchiveLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMembres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    EstAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    RejointLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DernierMessageLuId = table.Column<long>(type: "bigint", nullable: false),
                    Muet = table.Column<bool>(type: "boolean", nullable: false),
                    AQuitteLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMembres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMembres_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMembres_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessagesDiscussion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    AuteurUserId = table.Column<string>(type: "text", nullable: false),
                    Contenu = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EnvoyeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupprimeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReponseAId = table.Column<long>(type: "bigint", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('message_discussion_revision_seq')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesDiscussion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagesDiscussion_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MessageDiscussionId",
                table: "Notifications",
                column: "MessageDiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembres_ConversationId_UserId",
                table: "ConversationMembres",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembres_UserId",
                table: "ConversationMembres",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CleDirecte",
                table: "Conversations",
                column: "CleDirecte",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContexteType_ContexteId",
                table: "Conversations",
                columns: new[] { "ContexteType", "ContexteId" },
                unique: true,
                filter: "\"ContexteType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesDiscussion_AuteurUserId",
                table: "MessagesDiscussion",
                column: "AuteurUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesDiscussion_ConversationId_Id",
                table: "MessagesDiscussion",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesDiscussion_Revision",
                table: "MessagesDiscussion",
                column: "Revision");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_MessagesDiscussion_MessageDiscussionId",
                table: "Notifications",
                column: "MessageDiscussionId",
                principalTable: "MessagesDiscussion",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Renouvellement de la revision à CHAQUE écriture ──────────────────
            // Un trigger BEFORE INSERT OR UPDATE écrase la revision avec la prochaine
            // valeur de la séquence. Il couvre les trois cas voulus par la spec :
            //   • INSERT  : nouveau message ;
            //   • UPDATE  : modification d'un message ;
            //   • UPDATE  : suppression LOGIQUE (SupprimeLe renseigné, contenu vidé).
            // Le contrôleur ne gère donc jamais la revision lui-même : impossible de
            // l'oublier lors de l'ajout d'une nouvelle voie d'écriture.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION discussion_message_revision() RETURNS trigger AS $$
BEGIN
    NEW.""Revision"" := nextval('message_discussion_revision_seq');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;");

            migrationBuilder.Sql(@"
CREATE TRIGGER trg_discussion_message_revision
BEFORE INSERT OR UPDATE ON ""MessagesDiscussion""
FOR EACH ROW EXECUTE FUNCTION discussion_message_revision();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_MessagesDiscussion_MessageDiscussionId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "ConversationMembres");

            migrationBuilder.DropTable(
                name: "MessagesDiscussion");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_MessageDiscussionId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PeutGererDiscussion",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "PeutUtiliserDiscussion",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "MessageDiscussionId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EstCompteInterne",
                table: "AspNetUsers");

            // La table ayant été supprimée ci-dessus, le trigger l'est aussi. Il ne
            // reste qu'à retirer la fonction et la séquence globales.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS discussion_message_revision();");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS message_discussion_revision_seq;");
        }
    }
}
