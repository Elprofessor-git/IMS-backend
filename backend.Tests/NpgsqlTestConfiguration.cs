using System.Runtime.CompilerServices;

namespace Backend.Tests;

/// <summary>
/// Reproduit la configuration Npgsql de l'application (Program.cs :
/// <c>AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)</c>).
///
/// Pourquoi : le harnais de tests ouvre une connexion Npgsql pour créer la base
/// éphémère <c>ims_test_*</c> AVANT de démarrer l'hôte ASP.NET, donc avant que
/// Program.cs ne soit exécuté. Le commutateur étant lu une seule fois au premier
/// usage de Npgsql, il était déjà trop tard et les <see cref="DateTime"/> de
/// Kind=Utc reçus en JSON (planning, tâches) étaient refusés — un échec qui n'existe
/// pas dans l'application réelle, où le commutateur est positionné à temps.
///
/// [ModuleInitializer] garantit l'exécution de ce code au chargement de l'assembly
/// de tests, donc avant tout usage de Npgsql.
/// </summary>
internal static class NpgsqlTestConfiguration
{
    [ModuleInitializer]
    internal static void ActiverLegacyTimestampBehavior() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}
