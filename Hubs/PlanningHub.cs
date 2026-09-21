using Microsoft.AspNetCore.SignalR;

namespace Backend_Gestion_Magasin_API.Hubs
{
    /// <summary>
    /// Hub SignalR « Planning » : notified chaque client (grille temps réel) via
    /// <c>PlanningChanged</c> et chaque utilisateur ciblé via <c>NotificationReceived</c>.
    /// Point de montage : <c>app.MapHub&lt;PlanningHub&gt;("/hubs/planning")</c>.
    /// </summary>
    public class PlanningHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "PlanningClients");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "PlanningClients");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Diffuse un changement de grille à tous les clients connectés.</summary>
        public async Task BroadcastPlanningChanged(string message)
            => await Clients.Group("PlanningClients").SendAsync("PlanningChanged", message, DateTime.UtcNow);
    }
}
