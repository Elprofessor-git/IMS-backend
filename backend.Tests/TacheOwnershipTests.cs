using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

/// <summary>
/// Tests d'ownership et d'IDOR/BOLA du module Tâches (LOT 16).
///
/// Chaque test utilise un VRAI jeton JWT et la VRAIE base PostgreSQL migrée : les statuts
/// HTTP observés sont ceux du pipeline d'authentification et d'autorisation de production.
/// </summary>
public class TacheOwnershipTests : IClassFixture<TacheApiFactory>
{
    private readonly TacheApiFactory _factory;

    public TacheOwnershipTests(TacheApiFactory factory) => _factory = factory;

    // ── Mise en place ───────────────────────────────────────────────────

    /// <summary>Utilisateur ordinaire : voit et gère le module, mais rien d'autre que ses tâches.</summary>
    private Task<TestUser> CreateOperateurAsync(string id, string nom, string prenom) =>
        _factory.CreateUserAsync(id, nom, prenom, role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = false;
            role.PeutAssignerTaches = false;
        });

    private async Task<int> CreerTacheDirectAsync(string userId, string titre)
    {
        return await _factory.WithDbAsync(async db =>
        {
            var t = new TacheProduction
            {
                Titre = titre,
                Statut = StatutTache.NonCommence,
                Priorite = PrioriteTache.Normale,
                DateCreation = DateTime.Now,
                CreatedByUserId = userId,
                AssignedToUserId = userId,
                CreePar = titre
            };
            db.TachesProduction.Add(t);
            await db.SaveChangesAsync();
            return t.Id;
        });
    }

    // ══════════════ Test 1 — le serveur décide du propriétaire ══════════════

    [Fact]
    public async Task Test01_Creation_rattache_la_tache_a_auteur_ET_au_responsable_courants()
    {
        var a = await CreateOperateurAsync("t1-alice", "A", "Alice");

        var reponse = await a.Client.PostAsJsonAsync("/api/TacheProduction", new
        {
            titre = "Tâche créée par Alice",
            description = "contenu"
        });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Titre == "Tâche créée par Alice"));

        Assert.Equal(a.Id, tache.CreatedByUserId);
        Assert.Equal(a.Id, tache.AssignedToUserId);
    }

    [Fact]
    public async Task Test01bis_Le_client_ne_peut_pas_injecter_le_createur()
    {
        var a = await CreateOperateurAsync("t1b-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t1b-bob", "B", "Bob");

        // Le client tente de s'attribuer la propriété de Bob : mass-assignment.
        // CreatedByUserId et CreePar ne font pas partie du DTO de création : le corps
        // est ignoré silencieusement, la tâche est créée mais reste celle d'Alice.
        var reponse = await a.Client.PostAsJsonAsync("/api/TacheProduction", new
        {
            titre = "Tentative d'injection",
            createdByUserId = b.Id,
            creePar = "Falsifié"
        });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Titre == "Tentative d'injection"));

        // Le serveur a ignoré les valeurs client : le propriétaire est bien Alice.
        Assert.Equal(a.Id, tache.CreatedByUserId);
        Assert.Equal(a.Id, tache.AssignedToUserId);
        Assert.NotEqual("Falsifié", tache.CreePar);
    }

    [Fact]
    public async Task Test01ter_A_la_creation_le_responsable_vient_du_serveur_ou_du_droit_dassignation()
    {
        // Sans droit d'assignation, désigner un tiers à la création est refusé (403) :
        // on ne crée pas une tâche que le client croit attribuée à quelqu'un d'autre.
        var alice = await CreateOperateurAsync("t1c-alice", "A", "Alice");
        var bob = await CreateOperateurAsync("t1c-bob", "B", "Bob");

        var refuse = await alice.Client.PostAsJsonAsync("/api/TacheProduction", new
        {
            titre = "Création avec tiers",
            assignedToUserId = bob.Id
        });
        Assert.Equal(HttpStatusCode.Forbidden, refuse.StatusCode);

        // Avec le droit, la désignation est acceptée et le créateur reste inchangé.
        var aliceHabilitee = await _factory.CreateUserAsync("t1c-dora", "D", "Dora", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });
        var carol = await CreateOperateurAsync("t1c-carol", "C", "Carol");

        var accepte = await aliceHabilitee.Client.PostAsJsonAsync("/api/TacheProduction", new
        {
            titre = "Création pour un tiers",
            assignedToUserId = carol.Id
        });
        Assert.Equal(HttpStatusCode.Created, accepte.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Titre == "Création pour un tiers"));

        Assert.Equal(aliceHabilitee.Id, tache.CreatedByUserId);
        Assert.Equal(carol.Id, tache.AssignedToUserId);
    }

    // ══════════════ Test 2 — « Mes tâches » ne renvoie que les siennes ══════════════

    [Fact]
    public async Task Test02_Scope_mine_ne_renvoie_que_les_taches_de_utilisateur()
    {
        var a = await CreateOperateurAsync("t2-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t2-bob", "B", "Bob");

        await CreerTacheDirectAsync(a.Id, "Tâche A1");
        await CreerTacheDirectAsync(a.Id, "Tâche A2");
        await CreerTacheDirectAsync(b.Id, "Tâche B1");
        await CreerTacheDirectAsync(b.Id, "Tâche B2");

        var aVoit = await LireTitresAsync(a.Client, "mine");
        var bVoit = await LireTitresAsync(b.Client, "mine");

        Assert.Contains("Tâche A1", aVoit);
        Assert.Contains("Tâche A2", aVoit);
        Assert.DoesNotContain("Tâche B1", aVoit);
        Assert.DoesNotContain("Tâche B2", aVoit);

        Assert.Contains("Tâche B1", bVoit);
        Assert.Contains("Tâche B2", bVoit);
        Assert.DoesNotContain("Tâche A1", bVoit);
        Assert.DoesNotContain("Tâche A2", bVoit);
    }

    [Fact]
    public async Task Test02bis_Le_scope_par_defaut_est_mine()
    {
        var a = await CreateOperateurAsync("t2b-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t2b-bob", "B", "Bob");

        await CreerTacheDirectAsync(a.Id, "Default A");
        await CreerTacheDirectAsync(b.Id, "Default B");

        var sansParametre = await LireTitresAsync(a.Client, scope: null);

        Assert.Contains("Default A", sansParametre);
        Assert.DoesNotContain("Default B", sansParametre);
    }

    // ══════════════ Test 3 — lecture d'une tâche d'autrui : 404 ══════════════

    [Fact]
    public async Task Test03_A_ne_peut_pas_lire_la_tache_de_B()
    {
        var a = await CreateOperateurAsync("t3-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t3-bob", "B", "Bob");

        var idB = await CreerTacheDirectAsync(b.Id, "Privée de Bob");

        var reponse = await a.Client.GetAsync($"/api/TacheProduction/{idB}");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);

        // Aucune fuite dans le corps de la réponse.
        var corps = await reponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Privée de Bob", corps);
    }

    // ══════════════ Test 4 — modification d'une tâche d'autrui : refusée ══════════════

    [Fact]
    public async Task Test04_A_ne_peut_pas_modifier_la_tache_de_B_et_la_BD_est_inchangee()
    {
        var a = await CreateOperateurAsync("t4-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t4-bob", "B", "Bob");

        var idB = await CreerTacheDirectAsync(b.Id, "Intouchable");

        var reponse = await a.Client.PutAsJsonAsync($"/api/TacheProduction/{idB}", new
        {
            titre = "TITRE PIRATÉ"
        });

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);

        var apres = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idB));

        Assert.Equal("Intouchable", apres.Titre);
    }

    // ══════════════ Test 5 — suppression d'une tâche d'autrui : refusée ══════════════

    [Fact]
    public async Task Test05_A_ne_peut_pas_supprimer_la_tache_de_B_elle_survit()
    {
        var a = await CreateOperateurAsync("t5-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t5-bob", "B", "Bob");

        var idB = await CreerTacheDirectAsync(b.Id, "Survivante");

        var reponse = await a.Client.DeleteAsync($"/api/TacheProduction/{idB}");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);

        var existe = await _factory.WithDbAsync(db => db.TachesProduction.AnyAsync(t => t.Id == idB));
        Assert.True(existe, "La tâche de Bob ne doit pas avoir été supprimée.");
    }

    // ══════════════ Test 6 — assignation autorisée ══════════════

    [Fact]
    public async Task Test06_A_assigne_sa_propre_tache_a_B_quand_il_a_le_droit()
    {
        var a = await _factory.CreateUserAsync("t6-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });
        var b = await CreateOperateurAsync("t6-bob", "B", "Bob");

        var idA = await CreerTacheDirectAsync(a.Id, "Tâche de Alice à déléguer");

        var reponse = await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = b.Id });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idA));

        // Le créateur reste le créateur : seul le responsable change.
        Assert.Equal(a.Id, tache.CreatedByUserId);
        Assert.Equal(b.Id, tache.AssignedToUserId);
    }

    [Fact]
    public async Task Test06ter_Assigner_une_vide_desassigne_sans_transferer_la_tache_a_l_appelant()
    {
        var a = await _factory.CreateUserAsync("t6c-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });
        var b = await CreateOperateurAsync("t6c-bob", "B", "Bob");

        var idA = await CreerTacheDirectAsync(a.Id, "Tâche à désassigner");
        await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = b.Id });

        // Une valeur vide sur /Assigner est une DÉSASSIGNATION : elle ne doit surtout
        // pas remettre la tâche sur le compte de l'appelant.
        var reponse = await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = (string?)null });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idA));

        Assert.Null(tache.AssignedToUserId);
        Assert.Null(tache.ResponsableAssigne);
        // Le propriétaire n'a pas bougé.
        Assert.Equal(a.Id, tache.CreatedByUserId);
    }

    [Fact]
    public async Task Test06bis_Sans_droit_dassignation_l_assignation_a_un_tiers_est_refusee()
    {
        var a = await CreateOperateurAsync("t6b-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t6b-bob", "B", "Bob");

        var idA = await CreerTacheDirectAsync(a.Id, "Tâche de Alice");

        var reponse = await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = b.Id });

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idA));

        Assert.Equal(a.Id, tache.AssignedToUserId);
    }

    // ══════════════ Test 7 — assignation à un utilisateur inexistant ══════════════

    [Fact]
    public async Task Test07_Assigner_a_un_utilisateur_inexistant_renvoie_400()
    {
        var a = await _factory.CreateUserAsync("t7-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });

        var idA = await CreerTacheDirectAsync(a.Id, "Tâche de Alice");

        var reponse = await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = "utilisateur-fantome" });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    [Fact]
    public async Task Test07bis_Assigner_a_un_utilisateur_inactif_renvoie_400()
    {
        var a = await _factory.CreateUserAsync("t7b-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });

        await _factory.CreateUserAsync("t7b-inactif", "I", "Inactif", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
        });
        await _factory.WithDbAsync(db =>
            db.Users.Where(u => u.Id == "t7b-inactif").ExecuteUpdateAsync(s => s.SetProperty(u => u.EstActif, false)));

        var idA = await CreerTacheDirectAsync(a.Id, "Tâche de Alice");

        var reponse = await a.Client.PostAsJsonAsync($"/api/TacheProduction/{idA}/Assigner",
            new { assignedToUserId = "t7b-inactif" });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    // ══════════════ Test 8 — absence de permission de module ══════════════

    [Fact]
    public async Task Test08_Utilisateur_sans_PeutVoirTaches_obtient_403()
    {
        var a = await _factory.CreateUserAsync("t8-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = false;
            role.PeutGererTaches = false;
        });

        var reponse = await a.Client.GetAsync("/api/TacheProduction");

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
    }

    // ══════════════ Test 9 — scope=all sans droit global ══════════════

    [Fact]
    public async Task Test09_Scope_all_sans_droit_global_renvoie_403()
    {
        var a = await CreateOperateurAsync("t9-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t9-bob", "B", "Bob");
        await CreerTacheDirectAsync(b.Id, "Tâche de Bob");

        var reponse = await a.Client.GetAsync("/api/TacheProduction?scope=all");

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
    }

    [Fact]
    public async Task Test09bis_Scope_all_avec_droit_global_renvoie_toutes_les_taches()
    {
        var a = await _factory.CreateUserAsync("t9b-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = true;
        });
        var b = await CreateOperateurAsync("t9b-bob", "B", "Bob");

        await CreerTacheDirectAsync(a.Id, "Tâche A visible");
        await CreerTacheDirectAsync(b.Id, "Tâche B visible");

        var titres = await LireTitresAsync(a.Client, "all");

        Assert.Contains("Tâche A visible", titres);
        Assert.Contains("Tâche B visible", titres);
    }

    [Fact]
    public async Task Test09ter_Avec_droit_global_A_peut_agir_sur_la_tache_de_B()
    {
        var a = await _factory.CreateUserAsync("t9c-admin", "Ad", "Admin", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = true;
        });
        var b = await CreateOperateurAsync("t9c-bob", "B", "Bob");

        var idB = await CreerTacheDirectAsync(b.Id, "Tâche de Bob");

        var reponse = await a.Client.PutAsJsonAsync($"/api/TacheProduction/{idB}", new
        {
            titre = "Reprise par l'administrateur"
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var apres = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idB));

        Assert.Equal("Reprise par l'administrateur", apres.Titre);
        Assert.Equal(b.Id, apres.CreatedByUserId); // la propriété d'origine est préservée
    }

    // ══════════════ Actions mutantes : toutes doivent respecter l'ownership ══════════════

    [Theory]
    [InlineData("PUT", "statut", "{\"statut\":\"Termine\"}")]
    [InlineData("PUT", "equipe", "{\"equipeId\":\"Atelier B\"}")]
    [InlineData("POST", "Commencer", "\"n'importe qui\"")]
    [InlineData("POST", "MettreAJourAvancement", "100")]
    [InlineData("POST", "Bloquer", "\"blocage pirate\"")]
    [InlineData("POST", "Debloquer", "null")]
    [InlineData("POST", "Terminer", "\"fini\"")]
    [InlineData("POST", "Assigner", "{\"assignedToUserId\":\"t11-alice\"}")]
    [InlineData("POST", "ModifierPriorite", "{\"priorite\":\"Urgente\"}")]
    [InlineData("POST", "ModifierEcheance", "{\"dateFinPrevue\":\"2030-01-01T00:00:00Z\"}")]
    public async Task Test10_Aucune_action_mutante_n_echappe_a_l_ownership(string methode, string route, string corps)
    {
        var a = await _factory.CreateUserAsync("t11-alice", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true;
        });
        var b = await CreateOperateurAsync("t11-bob", "B", "Bob");

        var idB = await CreerTacheDirectAsync(b.Id, "Cible de toutes les actions");

        using var requete = new HttpRequestMessage(new HttpMethod(methode), $"/api/TacheProduction/{idB}/{route}")
        {
            Content = JsonContent.Create(JsonSerializer.Deserialize<object>(corps))
        };
        var reponse = await a.Client.SendAsync(requete);

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);

        // Aucune action ne doit avoir modifié la tâche.
        var apres = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.Id == idB));

        Assert.Equal(StatutTache.NonCommence, apres.Statut);
        Assert.Equal(PrioriteTache.Normale, apres.Priorite);
        Assert.Null(apres.DateDebutReelle);
        Assert.Null(apres.DateFinReelle);
        Assert.Null(apres.EquipeAssignee);
        Assert.Equal(b.Id, apres.AssignedToUserId);
        Assert.Equal(0m, apres.PourcentageAvancement);
        Assert.Null(apres.ProblemesBloques);
    }

    [Fact]
    public async Task Test10bis_Les_routes_de_filtre_ne_contournent_pas_l_ownership()
    {
        var a = await CreateOperateurAsync("t12-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t12-bob", "B", "Bob");

        // Tâche de Bob, dans la même équipe que celle d'Alice : le filtre Équipe ne doit rien divulguer.
        var idB = await _factory.WithDbAsync(async db =>
        {
            var t = new TacheProduction
            {
                Titre = "Tâche de Bob même équipe",
                EquipeAssignee = "Atelier A",
                Statut = StatutTache.NonCommence,
                DateCreation = DateTime.Now,
                CreatedByUserId = b.Id,
                AssignedToUserId = b.Id
            };
            db.TachesProduction.Add(t);
            await db.SaveChangesAsync();
            return t.Id;
        });

        var parEquipe = await LireTitresAsync(a.Client, null, $"/api/TacheProduction/Equipe/Atelier%20A");
        Assert.DoesNotContain("Tâche de Bob même équipe", parEquipe);

        var parStatut = await LireTitresAsync(a.Client, null, "/api/TacheProduction/Statut/0");
        Assert.DoesNotContain("Tâche de Bob même équipe", parStatut);

        // Le dashboard ne doit pas non plus agréger les tâches d'autrui.
        var dashboard = await a.Client.GetFromJsonAsync<JsonElement>("/api/TacheProduction/Dashboard");
        Assert.Equal(0, dashboard.GetProperty("totalTaches").GetInt32());
        Assert.False(dashboard.GetProperty("peutVoirToutesLesTaches").GetBoolean());
    }

    // ══════════════ Test 12 — endpoint legacy ══════════════

    [Fact]
    public async Task Test12_L_endpoint_legacy_api_Tache_n_expose_plus_aucune_donnee()
    {
        var a = await CreateOperateurAsync("t13-alice", "A", "Alice");

        // Une tâche existe bien dans la table legacy : c'est l'API qui a disparu.
        await _factory.WithDbAsync(async db =>
        {
            db.Taches.Add(new Tache { Titre = "Tâche legacy", Assignee = "quelqu'un" });
            await db.SaveChangesAsync();
        });

        foreach (var (methode, url) in new[]
                 {
                     (HttpMethod.Get, "/api/Tache"),
                     (HttpMethod.Get, "/api/Tache/1"),
                     (HttpMethod.Post, "/api/Tache"),
                     (HttpMethod.Put, "/api/Tache/1"),
                     (HttpMethod.Delete, "/api/Tache/1"),
                 })
        {
            using var requete = new HttpRequestMessage(methode, url);
            if (methode == HttpMethod.Post)
                requete.Content = JsonContent.Create(new { titre = "injection" });
            else if (methode == HttpMethod.Put)
                requete.Content = JsonContent.Create(new { id = 1, titre = "injection" });

            var reponse = await a.Client.SendAsync(requete);

            // Le contrôleur a été supprimé : la route n'est plus routée (404).
            Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        }

        // La table legacy est préservée (aucune migration destructive).
        var total = await _factory.WithDbAsync(db => db.Taches.CountAsync());
        Assert.Equal(1, total);
    }

    // ══════════════ Tâches historiques orphelines ══════════════

    [Fact]
    public async Task Test13_Une_tache_historique_sans_proprietaire_n_est_visible_que_des_privileges_globaux()
    {
        var a = await CreateOperateurAsync("t14-alice", "A", "Alice");
        var b = await CreateOperateurAsync("t14-bob", "B", "Bob");

        var idOrpheline = await _factory.WithDbAsync(async db =>
        {
            var t = new TacheProduction
            {
                Titre = "Tâche historique sans propriétaire",
                Statut = StatutTache.NonCommence,
                DateCreation = DateTime.Now,
                CreatedByUserId = null,
                AssignedToUserId = null
            };
            db.TachesProduction.Add(t);
            await db.SaveChangesAsync();
            return t.Id;
        });

        Assert.DoesNotContain("Tâche historique sans propriétaire", await LireTitresAsync(a.Client, "mine"));
        Assert.DoesNotContain("Tâche historique sans propriétaire", await LireTitresAsync(b.Client, "mine"));

        // Un rôle global, lui, la voit.
        var admin = await _factory.CreateUserAsync("t14-admin", "Ad", "Admin", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = true;
        });
        Assert.Contains("Tâche historique sans propriétaire", await LireTitresAsync(admin.Client, "mine"));
    }

    // ── Utilitaire ──────────────────────────────────────────────────────

    private static async Task<List<string>> LireTitresAsync(HttpClient client, string? scope, string? url = null)
    {
        if (url == null)
            url = scope == null
                ? "/api/TacheProduction"
                : $"/api/TacheProduction?scope={scope}";

        var reponse = await client.GetAsync(url);
        reponse.EnsureSuccessStatusCode();

        var taches = await reponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        return taches!.Select(t => t.GetProperty("titre").GetString()!).ToList();
    }

    // ══════════════ LOT 18 §2 — Contournement PeutAssignerTaches via GroupeTache ══════════════
    // Scénario : Alice (sans PeutAssignerTaches) applique un groupe contenant une ligne
    // avec le nom de Carol. Le serveur doit REFUSER (403) l'assignation à Carol,
    // au lieu de la laisser passer silencieusement via ResolveLigneResponsableAsync.

    [Fact]
    public async Task LOT18_S2_AppliquerGroupe_sans_PeutAssignerTaches_refuse_assignation_tiers()
    {
        // Arrange : Alice (opérateur standard, PAS de droit d'assignation)
        var alice = await CreateOperateurAsync("lot18-alice", "A", "Alice");

        // Carol (cible de l'assignation interdite)
        var carol = await CreateOperateurAsync("lot18-carol", "C", "Carol");

        // Commande pour l'application
        var clientId = await _factory.CreateClientAsync("lot18-");
        var commandeId = await _factory.WithDbAsync(async db =>
        {
            var cmd = new CommandeClient
            {
                NumeroCommande = "CMD-LOT18-" + Guid.NewGuid().ToString("N")[..8],
                TitreCommande = "Commande test LOT18",
                Statut = StatutCommande.EnProduction,
                DateCommande = DateTime.Now,
                ClientId = clientId
            };
            db.CommandesClients.Add(cmd);
            await db.SaveChangesAsync();
            return cmd.Id;
        });

        // Groupe avec une ligne portant le nom de Carol
        var groupeId = await _factory.WithDbAsync(async db =>
        {
            var groupe = new GroupeTache
            {
                Nom = "Groupe LOT18 " + Guid.NewGuid().ToString("N")[..6],
                EstActif = true,
                DateCreation = DateTime.Now
            };
            db.GroupesTaches.Add(groupe);
            await db.SaveChangesAsync();

            var ligne = new GroupeTacheLigne
            {
                GroupeTacheId = groupe.Id,
                Titre = "Tâche pour Carol",
                Description = "Doit être assignée à Carol",
                Priorite = PrioriteTache.Normale,
                DureeEstimeeHeures = 2,
                Ordre = 1,
                // Libellé résolu par ResolveLigneResponsableAsync. On utilise le
                // UserName (« lot18-carol ») et NON le nom « Carol » : les deux
                // tests de ce fichier créent un opérateur dont le Nom vaut « C »
                // sur la MÊME base (IClassFixture), et un libellé non qualifié
                // serait ambigu — la résolution retiendrait le premier trouvé par
                // OrderBy(Nom), c'est-à-dire potentiellement l'autre Carol.
                ResponsableAssigne = "lot18-carol" // UserName de Carol
            };
            db.GroupesTachesLignes.Add(ligne);
            await db.SaveChangesAsync();
            return groupe.Id;
        });

        // Act : Alice applique le groupe à la commande
        var reponse = await alice.Client.PostAsJsonAsync($"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new
        {
            CommandeId = commandeId
        });

        // Assert : 403 Forbidden — l'assignation à Carol est interdite sans PeutAssignerTaches
        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);

        // Vérifier qu'AUCUNE tâche n'a été créée (transaction annulée)
        var tachesCreees = await _factory.WithDbAsync(db =>
            db.TachesProduction.CountAsync(t => t.GroupeTacheId == groupeId));
        Assert.Equal(0, tachesCreees);
    }

    [Fact]
    public async Task LOT18_S2_AppliquerGroupe_AVEC_PeutAssignerTaches_accepte_assignation_tiers()
    {
        // Arrange : Alice AVEC le droit d'assignation
        var alice = await _factory.CreateUserAsync("lot18-alice-ok", "A", "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutAssignerTaches = true; // DROIT ACCORDÉ
        });

        var carol = await CreateOperateurAsync("lot18-carol-ok", "C", "Carol");

        var clientId = await _factory.CreateClientAsync("lot18-ok-");
        var commandeId = await _factory.WithDbAsync(async db =>
        {
            var cmd = new CommandeClient
            {
                NumeroCommande = "CMD-LOT18-OK-" + Guid.NewGuid().ToString("N")[..8],
                TitreCommande = "Commande test LOT18 OK",
                Statut = StatutCommande.EnProduction,
                DateCommande = DateTime.Now,
                ClientId = clientId
            };
            db.CommandesClients.Add(cmd);
            await db.SaveChangesAsync();
            return cmd.Id;
        });

        var groupeId = await _factory.WithDbAsync(async db =>
        {
            var groupe = new GroupeTache
            {
                Nom = "Groupe LOT18 OK " + Guid.NewGuid().ToString("N")[..6],
                EstActif = true,
                DateCreation = DateTime.Now
            };
            db.GroupesTaches.Add(groupe);
            await db.SaveChangesAsync();

            var ligne = new GroupeTacheLigne
            {
                GroupeTacheId = groupe.Id,
                Titre = "Tâche pour Carol (autorisée)",
                Priorite = PrioriteTache.Normale,
                DureeEstimeeHeures = 2,
                Ordre = 1,
                // Même raison que le test précédent : UserName unique, jamais un
                // libellé de nom ambigu entre les deux Carol de la base partagée.
                ResponsableAssigne = "lot18-carol-ok"
            };
            db.GroupesTachesLignes.Add(ligne);
            await db.SaveChangesAsync();
            return groupe.Id;
        });

        // Act
        var reponse = await alice.Client.PostAsJsonAsync($"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new
        {
            CommandeId = commandeId
        });

        // Assert : 200 OK — le droit permet l'assignation à Carol
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        // Vérifier que la tâche est bien assignée à Carol
        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.GroupeTacheId == groupeId));

        Assert.Equal(carol.Id, tache.AssignedToUserId);
    }

    // ══════════════ LOT 19 — Résolution fiable du responsable ══════════════
    // Le libellé de responsable d'une ligne de groupe est saisi librement (UI :
    // groupes-tab.tsx). L'ancien ResolveLigneResponsableAsync renvoyait
    // ApplicationUser? : « aucun responsable désigné » et « aucun utilisateur
    // trouvé » se confondaient, un homonyme était choisi au hasard, et la casse
    // comme les accents tombaient. Le résultat est désormais discriminé et
    // l'appelant REFUSE (400) sur 0 correspondance comme sur homonymie, sans
    // jamais créer la moindre tâche.

    // ── Outils ───────────────────────────────────────────────────────────

    /// <summary>Applicatrice disposant du droit d'assignation (sinon le 403
    /// masquerait le 400 que ces tests doivent observer).</summary>
    private Task<TestUser> CreateApplicatriceAsync(string id) =>
        _factory.CreateUserAsync(id, id, "Alice", role =>
        {
            role.PeutVoirTaches = true;
            role.PeutGererTaches = true;
            role.PeutVoirToutesTaches = false;
            role.PeutAssignerTaches = true;
        });

    private async Task<int> CreerCommandeAsync(string suffixe)
    {
        return await _factory.WithDbAsync(async db =>
        {
            var cmd = new CommandeClient
            {
                NumeroCommande = "CMD-LOT19-" + suffixe + "-" + Guid.NewGuid().ToString("N")[..8],
                TitreCommande = "Commande LOT19 " + suffixe,
                Statut = StatutCommande.EnProduction,
                DateCommande = DateTime.Now,
                ClientId = await _factory.CreateClientAsync("lot19-" + suffixe)
            };
            db.CommandesClients.Add(cmd);
            await db.SaveChangesAsync();
            return cmd.Id;
        });
    }

    /// <summary>Groupe actif de N lignes. Chaque ligne porte un responsable libre
    /// (null = aucun responsable désigné, cas nominal qui doit rester accepté).</summary>
    private async Task<int> CreerGroupeAsync(
        string suffixe, params (string Titre, int Ordre, string? Responsable)[] lignes)
    {
        return await _factory.WithDbAsync(async db =>
        {
            var groupe = new GroupeTache
            {
                Nom = "Groupe LOT19 " + suffixe + " " + Guid.NewGuid().ToString("N")[..6],
                EstActif = true,
                DateCreation = DateTime.Now
            };
            db.GroupesTaches.Add(groupe);
            await db.SaveChangesAsync();

            foreach (var (titre, ordre, responsable) in lignes)
            {
                db.GroupesTachesLignes.Add(new GroupeTacheLigne
                {
                    GroupeTacheId = groupe.Id,
                    Titre = titre,
                    Priorite = PrioriteTache.Normale,
                    DureeEstimeeHeures = 1,
                    Ordre = ordre,
                    ResponsableAssigne = responsable
                });
            }

            await db.SaveChangesAsync();
            return groupe.Id;
        });
    }

    private Task<int> CompterTachesAsync(int groupeId, int commandeId) =>
        _factory.WithDbAsync(db => db.TachesProduction.CountAsync(
            t => t.GroupeTacheId == groupeId || t.CommandeClientId == commandeId));

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LOT19_AucunMatch_refuse_le_groupe_et_cree_aucune_tache()
    {
        var alice = await CreateApplicatriceAsync("lot19-alice-0match");
        var commandeId = await CreerCommandeAsync("0match");
        var groupeId = await CreerGroupeAsync("0match", ("Découpe", 1, "Zebulon Introuvable"));

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new { CommandeId = commandeId });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

        // Le message cite le libellé introuvable ET la ligne fautive.
        var corps = await reponse.Content.ReadAsStringAsync();
        Assert.Contains("Zebulon Introuvable", corps);
        Assert.Contains("Découpe", corps);

        // Aucun repli silencieux sur le créateur : rien n'est écrit.
        Assert.Equal(0, await CompterTachesAsync(groupeId, commandeId));
    }

    [Fact]
    public async Task LOT19_Homonymes_refuse_le_groupe_et_liste_les_candidats()
    {
        var alice = await CreateApplicatriceAsync("lot19-alice-ambigu");
        // Deux homonymes exacts : aucun des 4 formats ne peut les départager.
        await CreateOperateurAsync("lot19-dupont-a", "Dupont", "Jean");
        await CreateOperateurAsync("lot19-dupont-b", "Dupont", "Jean");

        var commandeId = await CreerCommandeAsync("ambigu");
        var groupeId = await CreerGroupeAsync("ambigu", ("Pliage", 1, "Jean Dupont"));

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new { CommandeId = commandeId });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

        // Chaque candidat est cité par son nom lisible ET son UserName, sans
        // exposer d'identifiant interne supplémentaire.
        var corps = await reponse.Content.ReadAsStringAsync();
        Assert.Contains("Jean Dupont (lot19-dupont-a)", corps);
        Assert.Contains("Jean Dupont (lot19-dupont-b)", corps);
        Assert.Contains("Pliage", corps);

        // Aucun homonyme n'est retenu au hasard.
        Assert.Equal(0, await CompterTachesAsync(groupeId, commandeId));
    }

    [Fact]
    public async Task LOT19_Casse_differente_resout_le_responsable()
    {
        var alice = await CreateApplicatriceAsync("lot19-alice-casse");
        var carol = await CreateOperateurAsync("lot19-carol-casse", "Carol", "Caroline");

        var commandeId = await CreerCommandeAsync("casse");
        // « carol » vs « Carol » : la comparaison était sensible à la casse.
        var groupeId = await CreerGroupeAsync("casse", ("Marquage", 1, "carol"));

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new { CommandeId = commandeId });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        var tache = await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking().SingleAsync(t => t.GroupeTacheId == groupeId));
        Assert.Equal(carol.Id, tache.AssignedToUserId);
    }

    [Fact]
    public async Task LOT19_Accent_different_resout_le_responsable_dans_les_deux_sens()
    {
        var alice = await CreateApplicatriceAsync("lot19-alice-accent");
        // Stocké AVEC accent dans la base.
        var avecAccent = await CreateOperateurAsync("lot19-francois", "François", "Jean");
        // Stocké SANS accent dans la base.
        var sansAccent = await CreateOperateurAsync("lot19-gontran", "Gontran", "Achille");

        // Sens 1 : libellé sans accent, utilisateur avec accent.
        var commande1 = await CreerCommandeAsync("accent1");
        var groupe1 = await CreerGroupeAsync("accent1", ("Découpe", 1, "Francois"));

        var reponse1 = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupe1}/Appliquer", new { CommandeId = commande1 });

        Assert.Equal(HttpStatusCode.OK, reponse1.StatusCode);
        Assert.Equal(avecAccent.Id, await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .Where(t => t.GroupeTacheId == groupe1)
                .Select(t => t.AssignedToUserId)
                .SingleAsync()));

        // Sens 2 : libellé avec accent, utilisateur sans accent.
        var commande2 = await CreerCommandeAsync("accent2");
        var groupe2 = await CreerGroupeAsync("accent2", ("Pliage", 1, "Gontran"));

        var reponse2 = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupe2}/Appliquer", new { CommandeId = commande2 });

        Assert.Equal(HttpStatusCode.OK, reponse2.StatusCode);
        Assert.Equal(sansAccent.Id, await _factory.WithDbAsync(db =>
            db.TachesProduction.AsNoTracking()
                .Where(t => t.GroupeTacheId == groupe2)
                .Select(t => t.AssignedToUserId)
                .SingleAsync()));
    }

    [Fact]
    public async Task LOT19_Groupe_3_lignes_dont_la_2e_ambigue_ne_cree_AUCUNE_tache()
    {
        var alice = await CreateApplicatriceAsync("lot19-alice-3lignes");
        var cible = await CreateOperateurAsync("lot19-cible-3lignes", "Cible", "Celine");
        await CreateOperateurAsync("lot19-durand-a", "Durand", "Paul");
        await CreateOperateurAsync("lot19-durand-b", "Durand", "Paul");

        var commandeId = await CreerCommandeAsync("3lignes");
        var groupeId = await CreerGroupeAsync("3lignes",
            ("Étape 1 résolue", 1, "lot19-cible-3lignes"),
            ("Étape 2 ambiguë", 2, "Paul Durand"),
            ("Étape 3 résolue", 3, "lot19-cible-3lignes"));

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new { CommandeId = commandeId });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        var corps = await reponse.Content.ReadAsStringAsync();
        Assert.Contains("Étape 2 ambiguë", corps);

        // Le refus porte sur le GROUPE ENTIER : les lignes 1 et 3, pourtant
        // parfaitement résolues, ne doivent laisser AUCUNE trace. On contrôle
        // par commande ET par titres, pour ne pas manquer une tâche orpheline.
        Assert.Equal(0, await CompterTachesAsync(groupeId, commandeId));
        Assert.Equal(0, await _factory.WithDbAsync(db => db.TachesProduction.CountAsync(
            t => t.Titre == "Étape 1 résolue" || t.Titre == "Étape 3 résolue")));

        // Garde-fou : la cible des lignes 1 et 3 n'a rien reçu non plus.
        Assert.Equal(0, await _factory.WithDbAsync(db =>
            db.TachesProduction.CountAsync(t => t.AssignedToUserId == cible.Id)));
    }

    [Fact]
    public async Task LOT19_Aucun_responsable_designe_reste_le_cas_nominal()
    {
        // Non-régression : une ligne SANS responsable n'est pas un échec, la tâche
        // revient au créateur. C'est ce qui distingue « non désigné » de
        // « introuvable » dans le résultat discriminé.
        var alice = await CreateApplicatriceAsync("lot19-alice-non-designe");
        var commandeId = await CreerCommandeAsync("non-designe");
        var groupeId = await CreerGroupeAsync("non-designe",
            ("Étape sans responsable", 1, null),
            ("Étape responsable vide", 2, "   "));

        var reponse = await alice.Client.PostAsJsonAsync(
            $"/api/TacheProduction/Groupes/{groupeId}/Appliquer", new { CommandeId = commandeId });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(2, await CompterTachesAsync(groupeId, commandeId));

        // Les deux tâches reviennent à l'applicatrice (comparaison limitée à CETTE
        // commande : les autres tests de la classe ont leurs propres tâches).
        Assert.Equal(0, await _factory.WithDbAsync(db => db.TachesProduction.CountAsync(
            t => t.CommandeClientId == commandeId && t.AssignedToUserId != alice.Id)));
    }
}
