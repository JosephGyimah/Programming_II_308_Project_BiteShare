using System.Net.Http.Json;
using BiteShare.Shared.DTOs;

namespace BiteShare.Client.Services;

public record MenuItemDto(Guid Id, string Name, string? Description, decimal Price, bool Available);
public record AddMenuItemRequest(string Name, string? Description, decimal Price);
public record ParticipantDto(Guid Id, string DisplayName, bool IsHost, bool IsGuest);

/// <summary>
/// Thin wrapper over the two named HttpClients (identity-authed vs
/// participant-authed). Pages call through here rather than touching
/// HttpClient directly, so auth wiring stays in one place.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _identityApi;
    private readonly HttpClient _sessionApi;

    public ApiClient(IHttpClientFactory factory)
    {
        _identityApi = factory.CreateClient("IdentityApi");
        _sessionApi = factory.CreateClient("SessionApi");
    }

    // --- Auth / account -------------------------------------------------

    public async Task<AuthResponse?> RegisterAsync(string email, string password, string displayName)
    {
        var resp = await _identityApi.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password, displayName));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>() : null;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var resp = await _identityApi.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>() : null;
    }

    public async Task<GuestJoinResponse?> GuestJoinAsync(string joinCode, string displayName)
    {
        var resp = await _identityApi.PostAsJsonAsync("api/auth/guest-join", new GuestJoinRequest(joinCode, displayName));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<GuestJoinResponse>() : null;
    }

    // --- Sessions (identity-authed) -------------------------------------

    public async Task<SessionWithTokenDto?> CreateSessionAsync(string name, DateTime? deadlineUtc)
    {
        var resp = await _identityApi.PostAsJsonAsync("api/sessions", new CreateSessionRequest(name, deadlineUtc));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SessionWithTokenDto>() : null;
    }

    public async Task<SessionWithTokenDto?> JoinSessionByCodeAsync(string joinCode)
    {
        var resp = await _identityApi.PostAsJsonAsync("api/sessions/join", new JoinSessionRequest(joinCode));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SessionWithTokenDto>() : null;
    }

    // --- Session-scoped (participant-authed) -----------------------------

    public async Task<List<ParticipantDto>> GetParticipantsAsync(Guid sessionId) =>
        await _sessionApi.GetFromJsonAsync<List<ParticipantDto>>($"api/sessions/{sessionId}/participants") ?? new();

    public async Task<List<MenuItemDto>> GetMenuItemsAsync(Guid sessionId) =>
        await _sessionApi.GetFromJsonAsync<List<MenuItemDto>>($"api/sessions/{sessionId}/menuitems") ?? new();

    public async Task<MenuItemDto?> AddMenuItemAsync(Guid sessionId, string name, string? description, decimal price)
    {
        var resp = await _sessionApi.PostAsJsonAsync($"api/sessions/{sessionId}/menuitems", new AddMenuItemRequest(name, description, price));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<MenuItemDto>() : null;
    }

    public async Task<List<CartItemDto>> GetCartAsync(Guid sessionId) =>
        await _sessionApi.GetFromJsonAsync<List<CartItemDto>>($"api/sessions/{sessionId}/cart") ?? new();

    public async Task<CartItemDto?> AddCartItemAsync(Guid sessionId, Guid menuItemId, int quantity, string? notes)
    {
        var resp = await _sessionApi.PostAsJsonAsync($"api/sessions/{sessionId}/cart", new AddCartItemRequest(menuItemId, quantity, notes));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<CartItemDto>() : null;
    }

    public async Task<bool> RemoveCartItemAsync(Guid sessionId, Guid cartItemId)
    {
        var resp = await _sessionApi.DeleteAsync($"api/sessions/{sessionId}/cart/{cartItemId}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<OrderSummaryDto?> SubmitOrderAsync(Guid sessionId, string splitMode, decimal tax, decimal tip, decimal deliveryFee, Dictionary<Guid, string> paymentMethodIds)
    {
        var resp = await _sessionApi.PostAsJsonAsync($"api/sessions/{sessionId}/orders/submit", new SubmitOrderRequest(splitMode, tax, tip, deliveryFee, paymentMethodIds));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<OrderSummaryDto>() : null;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid sessionId, Guid orderId, string status)
    {
        var resp = await _sessionApi.PostAsJsonAsync($"api/sessions/{sessionId}/orders/{orderId}/status", status);
        return resp.IsSuccessStatusCode;
    }

    public async Task<byte[]?> GetReceiptPdfAsync(Guid sessionId, Guid orderId)
    {
        var resp = await _sessionApi.GetAsync($"api/sessions/{sessionId}/orders/{orderId}/receipts/pdf");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync() : null;
    }
}
