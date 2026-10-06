// Sample — GridHub.cs
// SignalR hub that broadcasts real-time events to all connected clients.

using Microsoft.AspNetCore.SignalR;

namespace GridLog.SignalR.Hubs
{
    public class GridHub : Hub
    {
        // Broadcast: called by API controllers after any data change.
        // All connected clients (WPFGridLog-1, GridLog.Notifier, GridLog.Web)
        // receive this and refresh their pending panels.
        public async Task NotifyPendingChanged()
            => await Clients.All.SendAsync("PendingChanged");

        // Broadcast: tells WPFGridLog-1 desktop app to navigate to a page.
        // Used by Notifier when operator clicks a chip — opens the matching
        // Tripping or Shutdown page on the main app.
        public async Task NavigateTo(string page)
            => await Clients.All.SendAsync("NavigateTo", page);

        // Broadcast: WPFGridLog-1 announces which page it is currently showing.
        // Notifier uses this to mute the bell when the operator is already
        // looking at the Tripping Events page.
        public async Task BroadcastActivePage(string page)
            => await Clients.All.SendAsync("ActivePageChanged", page);
    }
}
