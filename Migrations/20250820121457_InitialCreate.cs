using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend_Gestion_Magasin_API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Categorie = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SousCategorie = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Unite = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Marque = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Caracteristiques = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PrixUnitaireMoyen = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SeuilAlerte = table.Column<int>(type: "integer", nullable: false),
                    SeuilCritique = table.Column<int>(type: "integer", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FournisseurClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NomEntreprise = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CodePostal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pays = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstFournisseur = table.Column<bool>(type: "boolean", nullable: false),
                    EstClient = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FournisseurClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fournisseurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomEntreprise = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PersonneContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CodePostal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pays = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SpecialitesProduits = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConditionsPaiement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DelaiLivraisonJours = table.Column<int>(type: "integer", nullable: false),
                    NotesContrat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fournisseurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plateformes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SiteWeb = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plateformes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PeutGererStock = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererCommandes = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererTaches = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererClients = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererFournisseurs = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererAchats = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererImportations = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererUtilisateurs = table.Column<bool>(type: "boolean", nullable: false),
                    PeutGererMouvements = table.Column<bool>(type: "boolean", nullable: false),
                    PeutValiderStock = table.Column<bool>(type: "boolean", nullable: false),
                    PeutConfirmerAchats = table.Column<bool>(type: "boolean", nullable: false),
                    PeutValiderImportations = table.Column<bool>(type: "boolean", nullable: false),
                    EstAdministrateur = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Taches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstTerminee = table.Column<bool>(type: "boolean", nullable: false),
                    Assignee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priorite = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CodeCouleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmplacementPhysique = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumeroLot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Quantite = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantiteReservee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TypeStock = table.Column<string>(type: "text", nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DateEntree = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatePeremption = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ValidationManuelleRequise = table.Column<bool>(type: "boolean", nullable: false),
                    EstValide = table.Column<bool>(type: "boolean", nullable: false),
                    ValidePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateValidation = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stocks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Importations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceImportation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FournisseurId = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    DateImportation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateReceptionPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateReceptionReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModeExpedition = table.Column<string>(type: "text", nullable: false),
                    MontantTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CheminFacture = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CheminBonLivraison = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CheminCertificatDouane = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NotesImportation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HistoriqueModifications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Importations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Importations_Fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalTable: "Fournisseurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NomEntreprise = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CodePostal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pays = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferencesTissus = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NotesHistorique = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    PlateformeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Plateformes_PlateformeId",
                        column: x => x.PlateformeId,
                        principalTable: "Plateformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Poste = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    Equipe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Departement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DerniereConnexion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommandesClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroCommande = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    TitreCommande = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DescriptionCommande = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCommande = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateLivraisonSouhaitee = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    MontantTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PourcentageRessourcesCouvertes = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    NotesSpeciales = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SpecificationsClient = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandesClients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandesClients_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Achats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroAchat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FournisseurId = table.Column<int>(type: "integer", nullable: false),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    DateAchat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateLivraisonPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateLivraisonReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MontantTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ConditionsPaiement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NotesAchat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CheminPDF = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HistoriqueModifications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    JustificatifAnnulation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achats_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Achats_Fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalTable: "Fournisseurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BesoinsCommandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    TypeBesoin = table.Column<string>(type: "text", nullable: false),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuantiteUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NombrePieces = table.Column<int>(type: "integer", nullable: false),
                    QuantiteTotale = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantiteCouverte = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantiteStockImporte = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantiteAchatsLocaux = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantiteStockLibre = table.Column<decimal>(type: "numeric", nullable: false),
                    EstCompletementCouvert = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BesoinsCommandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BesoinsCommandes_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BesoinsCommandes_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LignesImportation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportationId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: true),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CodeCouleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Nature = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Quantite = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MontantLigne = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstAffecteStock = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesImportation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesImportation_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LignesImportation_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LignesImportation_Importations_ImportationId",
                        column: x => x.ImportationId,
                        principalTable: "Importations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TachesProduction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CommandeClientId = table.Column<int>(type: "integer", nullable: true),
                    EquipeAssignee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponsableAssigne = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    Priorite = table.Column<string>(type: "text", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateDebutPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateDebutReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DureeEstimeeHeures = table.Column<int>(type: "integer", nullable: false),
                    DureeReelleHeures = table.Column<int>(type: "integer", nullable: false),
                    PourcentageAvancement = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    NotesProgression = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProblemesBloques = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TachesProduction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TachesProduction_CommandesClients_CommandeClientId",
                        column: x => x.CommandeClientId,
                        principalTable: "CommandesClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LignesAchat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AchatId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Couleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CodeCouleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Taille = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Quantite = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MontantLigne = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DescriptionSpecifique = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesAchat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesAchat_Achats_AchatId",
                        column: x => x.AchatId,
                        principalTable: "Achats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LignesAchat_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MouvementsStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockId = table.Column<int>(type: "integer", nullable: false),
                    TacheProductionId = table.Column<int>(type: "integer", nullable: true),
                    TypeMouvement = table.Column<string>(type: "text", nullable: false),
                    OrigineMouvement = table.Column<string>(type: "text", nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantiteAvant = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantiteApres = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EmplacementSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmplacementDestination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumeroLot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Motif = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DateMouvement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectuePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValidePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateValidation = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MouvementsStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MouvementsStock_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MouvementsStock_TachesProduction_TacheProductionId",
                        column: x => x.TacheProductionId,
                        principalTable: "TachesProduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achats_CommandeClientId",
                table: "Achats",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Achats_FournisseurId",
                table: "Achats",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_Achats_NumeroAchat",
                table: "Achats",
                column: "NumeroAchat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RoleId",
                table: "AspNetUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BesoinsCommandes_ArticleId",
                table: "BesoinsCommandes",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_BesoinsCommandes_CommandeClientId",
                table: "BesoinsCommandes",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PlateformeId",
                table: "Clients",
                column: "PlateformeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandesClients_ClientId",
                table: "CommandesClients",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandesClients_NumeroCommande",
                table: "CommandesClients",
                column: "NumeroCommande",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Importations_FournisseurId",
                table: "Importations",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_Importations_ReferenceImportation",
                table: "Importations",
                column: "ReferenceImportation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_AchatId",
                table: "LignesAchat",
                column: "AchatId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAchat_ArticleId",
                table: "LignesAchat",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_ArticleId",
                table: "LignesImportation",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_CommandeClientId",
                table: "LignesImportation",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesImportation_ImportationId",
                table: "LignesImportation",
                column: "ImportationId");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_DateMouvement",
                table: "MouvementsStock",
                column: "DateMouvement");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_StockId",
                table: "MouvementsStock",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_TacheProductionId",
                table: "MouvementsStock",
                column: "TacheProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_ArticleId_TypeStock",
                table: "Stocks",
                columns: new[] { "ArticleId", "TypeStock" });

            migrationBuilder.CreateIndex(
                name: "IX_TachesProduction_CommandeClientId",
                table: "TachesProduction",
                column: "CommandeClientId");

            migrationBuilder.CreateIndex(
                name: "IX_TachesProduction_Statut_DateFinPrevue",
                table: "TachesProduction",
                columns: new[] { "Statut", "DateFinPrevue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BesoinsCommandes");

            migrationBuilder.DropTable(
                name: "FournisseurClients");

            migrationBuilder.DropTable(
                name: "LignesAchat");

            migrationBuilder.DropTable(
                name: "LignesImportation");

            migrationBuilder.DropTable(
                name: "MouvementsStock");

            migrationBuilder.DropTable(
                name: "Taches");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Achats");

            migrationBuilder.DropTable(
                name: "Importations");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "TachesProduction");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "Fournisseurs");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "CommandesClients");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Plateformes");
        }
    }
}
