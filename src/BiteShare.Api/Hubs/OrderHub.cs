using BiteShare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BiteShare.Api.Hubs;

/// <summary>
/// Broadcasts cart add/remove/update events and order status pipeline updates
/// (confirmed -> preparing -> out for delivery -> delivered) to everyone in a session.
/// One SignalR group per Session.Id. Controllers push events in via IHubContext;
/// clients call JoinSession once connected.
///
/// Reconnection handling for Blazor WASM clients is the highest-risk part of this
/// project (see execution guide, Stream A risk note). The client is responsible for
/// re-calling JoinSession after every reconnect — see OrderHubService on the client,
/// which wires HubConnection.Reconnected to do exactly that. Server-side, group
/// membership is *not* preserved across a dropped connection (SignalR gives each
/// reconnect a new ConnectionId), which is why the client must re-join rather than
/// the server trying to restore it.
/// </summary>
[Authorize]
public class OrderHub : Hub
{
    public static string SessionGroup(Guid sessionId) => $"session:{sessionId}";

    public override async Task OnConnectedAsync()
    {
        // Auto-join the caller's own session group based on their participant token,
        // so the client doesn't have to race a manual JoinSession call after connecting.
        var caller = CurrentParticipant.FromUser(Context.User!);
        if (caller is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(caller.SessionId));

        await base.OnConnectedAsync();
    }

    /// <summary>Kept for explicit re-join after a client-detected reconnect.</summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(Guid.Parse(sessionId)));
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(Guid.Parse(sessionId)));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Group membership is dropped automatically by SignalR on disconnect — nothing
        // to clean up manually. Don't mutate Participant/session state here: a dropped
        // WebSocket is expected and frequent on mobile networks, and isn't the same as
        // the participant leaving the session (that's an explicit Remove/leave action).
        await base.OnDisconnectedAsync(exception);
    }
}
