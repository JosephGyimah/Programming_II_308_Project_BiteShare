using BiteShare.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace BiteShare.Client.Services;

/// <summary>
/// Wraps the SignalR connection to OrderHub. This is the highest technical-risk
/// piece of the project per the execution guide — Blazor WASM connections drop
/// often (mobile networks, tab backgrounding), so:
/// - WithAutomaticReconnect() handles short blips without the caller doing anything.
/// - On Reconnected, we explicitly re-run JoinSession — group membership does not
///   survive a reconnect (SignalR assigns a new ConnectionId), even though the
///   server also tries to auto-join on OnConnectedAsync from the token claims.
/// - On Closed (automatic reconnect gave up), we surface it via ConnectionLost so
///   the UI can show a "reconnecting..." banner instead of silently going stale.
/// </summary>
public class OrderHubService : IAsyncDisposable
{
    private readonly string _baseUrl;
    private HubConnection? _connection;

    public event Action<CartEvent>? CartUpdated;
    public event Action<OrderStatusUpdate>? OrderStatusChanged;
    public event Action? Reconnecting;
    public event Action? Reconnected;
    public event Action? ConnectionLost;

    public OrderHubService(IConfiguration configuration)
    {
        _baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:5001";
    }

    public async Task ConnectAsync(Guid sessionId, string participantToken)
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/order", options =>
            {
                // Browsers can't set an Authorization header on the WebSocket
                // handshake — the token goes as a query param instead (see
                // OrderHub / Program.cs OnMessageReceived on the API side).
                options.AccessTokenProvider = () => Task.FromResult<string?>(participantToken);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _connection.On<CartEvent>("CartUpdated", evt => CartUpdated?.Invoke(evt));
        _connection.On<OrderStatusUpdate>("OrderStatusChanged", evt => OrderStatusChanged?.Invoke(evt));

        _connection.Reconnecting += _ => { Reconnecting?.Invoke(); return Task.CompletedTask; };
        _connection.Reconnected += async _ =>
        {
            await _connection.InvokeAsync("JoinSession", sessionId.ToString());
            Reconnected?.Invoke();
        };
        _connection.Closed += _ => { ConnectionLost?.Invoke(); return Task.CompletedTask; };

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinSession", sessionId.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
