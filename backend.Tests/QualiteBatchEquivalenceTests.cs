using System.Data.Common;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Backend.Tests
{
    /// <summary>
    /// LOT « Nettoyage » — fin du N+1 de <see cref="QualiteService.CalculerSoldeAsync"/>.
    ///
    /// Deux propriétés vérifiées :
    ///   1. ÉQUIVALENCE : le batch <c>CalculerSoldesAsync</c> rend EXACTEMENT les mêmes
    ///      soldes que N appels à <c>CalculerSoldeAsync</c>, y compris pour un triplet
    ///      sans aucune donnée.
    ///   2. COÛT CONSTANT : le nombre de requêtes SQL du batch ne grandit PAS avec le
    ///      nombre de triplets (c'est tout l'intérêt : supprimer le N+1).
    /// </summary>
    public class QualiteBatchEquivalenceTests : IClassFixture<AuthApiFactory>
    {
        private readonly AuthApiFactory _factory;

        public QualiteBatchEquivalenceTests(AuthApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Batch_egale_appels_individuels_et_cout_constant()
        {
            string connexion = null!;
            var triplets = new List<QualiteService.Triplet>();

            await _factory.WithDbAsync(async ctx =>
            {
                connexion = ctx.Database.GetConnectionString()!;

                var plateforme = new Plateforme { Nom = "P-batch", EstActif = true };
                ctx.Plateformes.Add(plateforme);
                await ctx.SaveChangesAsync();

                var client = new Client
                {
                    Nom = "Client-batch",
                    Email = $"batch.{Guid.NewGuid():N}@ims.test",
                    PlateformeId = plateforme.Id,
                    EstActif = true
                };
                ctx.Clients.Add(client);
                await ctx.SaveChangesAsync();

                var commande = new CommandeClient
                {
                    NumeroCommande = "CMD-batch-" + Guid.NewGuid().ToString("N")[..4],
                    ClientId = client.Id,
                    Statut = StatutCommande.EnProduction
                };
                ctx.CommandesClients.Add(commande);
                await ctx.SaveChangesAsync();

                var of = new OrdreFabrication
                {
                    CommandeId = commande.Id,
                    NumeroOF = "OF-batch-" + Guid.NewGuid().ToString("N")[..4]
                };
                ctx.OrdresFabrication.Add(of);

                var chaine = new ChaineProduction
                {
                    Nom = "Chaîne batch",
                    TypeChaine = TypeChaineProduction.Confection,
                    EstSousTraitant = true,
                    EstActif = true
                };
                ctx.ChainesProduction.Add(chaine);
                await ctx.SaveChangesAsync();

                var tA = new QualiteService.Triplet(commande.Id, chaine.Id, "M");
                var tB = new QualiteService.Triplet(commande.Id, chaine.Id, "L");
                var tVide = new QualiteService.Triplet(commande.Id, chaine.Id, "S");
                triplets.AddRange(new[] { tA, tB, tVide });

                // Exports : Q(tA)=100, Q(tB)=50, Q(tVide)=0
                ctx.LotExports.AddRange(
                    new LotExport { CommandeId = commande.Id, ChaineProductionId = chaine.Id, Taille = "M", QuantiteExportee = 100 },
                    new LotExport { CommandeId = commande.Id, ChaineProductionId = chaine.Id, Taille = "L", QuantiteExportee = 50 });
                await ctx.SaveChangesAsync();

                // Tour 1 de tA : 60 contrôlées, 50 acceptées, 10 retouche → R1=40.
                var c1 = new ControleQualite
                {
                    OrdreFabricationId = of.Id,
                    ChaineProductionId = chaine.Id,
                    Taille = "M",
                    TypeControle = TypeControle.Interne,
                    QuantiteControlee = 60,
                    QuantiteAcceptee = 50,
                    QuantiteRetouche = 10,
                    QuantiteRebut = 0
                };
                // Tour 1 de tB : soldé d'emblée.
                var c3 = new ControleQualite
                {
                    OrdreFabricationId = of.Id,
                    ChaineProductionId = chaine.Id,
                    Taille = "L",
                    TypeControle = TypeControle.Interne,
                    QuantiteControlee = 50,
                    QuantiteAcceptee = 50,
                    QuantiteRetouche = 0,
                    QuantiteRebut = 0
                };
                ctx.ControlesQualite.AddRange(c1, c3);
                await ctx.SaveChangesAsync();

                // Envoi de 6 pièces de c1, re-contrôle de 4 → ER=2, R(c1)=10-6=4.
                var envoi = new EnvoiRetouche
                {
                    ControleQualiteId = c1.Id,
                    ChaineProductionId = chaine.Id,
                    QuantiteRenvoyee = 6
                };
                ctx.EnvoisRetouche.Add(envoi);
                await ctx.SaveChangesAsync();

                ctx.ControlesQualite.Add(new ControleQualite
                {
                    OrdreFabricationId = of.Id,
                    ChaineProductionId = chaine.Id,
                    Taille = "M",
                    TypeControle = TypeControle.RetourSousTraitant,
                    EnvoiRetoucheId = envoi.Id,
                    QuantiteControlee = 4,
                    QuantiteAcceptee = 4,
                    QuantiteRetouche = 0,
                    QuantiteRebut = 0
                });
                await ctx.SaveChangesAsync();
            });

            // Contexte instrumenté sur la MÊME base, pour compter les requêtes.
            var compteur = new CompteurRequetes();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connexion, o => o.EnableRetryOnFailure())
                .AddInterceptors(compteur)
                .Options;

            await using var ctx = new ApplicationDbContext(options);
            var service = new QualiteService(ctx);

            // 1. ÉQUIVALENCE batch == N appels individuels.
            var batch = await service.CalculerSoldesAsync(triplets);
            foreach (var t in triplets)
            {
                Assert.True(batch.ContainsKey(t), $"le batch doit rendre le triplet {t}");
                var individuel = await service.CalculerSoldeAsync(t);
                Assert.Equal(individuel, batch[t]);
            }

            // 2. COÛT CONSTANT : 1 triplet vs 3 triplets → même nombre de requêtes.
            compteur.Reinitialiser();
            await service.CalculerSoldesAsync(new[] { triplets[0] });
            var requetesUnTriplet = compteur.Nombre;

            compteur.Reinitialiser();
            await service.CalculerSoldesAsync(triplets);
            var requetesTroisTriplets = compteur.Nombre;

            Assert.True(requetesUnTriplet > 0, "le compteur doit observer au moins une requête");
            Assert.Equal(requetesUnTriplet, requetesTroisTriplets);
        }

        private sealed class CompteurRequetes : DbCommandInterceptor
        {
            public int Nombre;

            public void Reinitialiser() => Nombre = 0;

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            {
                Interlocked.Increment(ref Nombre);
                return base.ReaderExecuting(command, eventData, result);
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData,
                InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref Nombre);
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override InterceptionResult<object> ScalarExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
            {
                Interlocked.Increment(ref Nombre);
                return base.ScalarExecuting(command, eventData, result);
            }

            public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
                DbCommand command, CommandEventData eventData,
                InterceptionResult<object> result, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref Nombre);
                return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
