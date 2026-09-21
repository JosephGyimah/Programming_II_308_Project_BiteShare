using Microsoft.JSInterop;

namespace BiteShare.Client.Services;

/// <summary>
/// Holds the two token types the client juggles:
/// - Identity token: from register/login. Used for account-level calls
///   (create a session, list "my sessions").
/// - Participant token: scoped to one session, from guest-join or session
///   create/join. Used for cart/orders/participants/menu calls and the
///   SignalR hub connection.
/// Persisted to localStorage so a page refresh doesn't drop the session.
/// </summary>
public class AuthTokenStore
{
    private readonly IJSRuntime _js;

    public string? IdentityToken { get; private set; }
    public string? IdentityDisplayName { get; private set; }
    public string? ParticipantToken { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public bool IsHost { get; private set; }

    public event Action? OnChange;

    public AuthTokenStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task LoadFromStorageAsync()
    {
        IdentityToken = await GetAsync("biteshare_identity_token");
        IdentityDisplayName = await GetAsync("biteshare_identity_name");
        ParticipantToken = await GetAsync("biteshare_participant_token");
        var sessionIdRaw = await GetAsync("biteshare_session_id");
        SessionId = Guid.TryParse(sessionIdRaw, out var id) ? id : null;
        var participantIdRaw = await GetAsync("biteshare_participant_id");
        ParticipantId = Guid.TryParse(participantIdRaw, out var pid) ? pid : null;
        IsHost = await GetAsync("biteshare_is_host") == "true";
        OnChange?.Invoke();
    }

    public async Task SetIdentityAsync(string token, string displayName)
    {
        IdentityToken = token;
        IdentityDisplayName = displayName;
        await SetAsync("biteshare_identity_token", token);
        await SetAsync("biteshare_identity_name", displayName);
        OnChange?.Invoke();
    }

    public async Task SetParticipantAsync(string token, Guid sessionId, Guid participantId, bool isHost)
    {
        ParticipantToken = token;
        SessionId = sessionId;
        ParticipantId = participantId;
        IsHost = isHost;
        await SetAsync("biteshare_participant_token", token);
        await SetAsync("biteshare_session_id", sessionId.ToString());
        await SetAsync("biteshare_participant_id", participantId.ToString());
        await SetAsync("biteshare_is_host", isHost ? "true" : "false");
        OnChange?.Invoke();
    }

    public async Task ClearParticipantAsync()
    {
        ParticipantToken = null;
        SessionId = null;
        ParticipantId = null;
        IsHost = false;
        await RemoveAsync("biteshare_participant_token");
        await RemoveAsync("biteshare_session_id");
        await RemoveAsync("biteshare_participant_id");
        await RemoveAsync("biteshare_is_host");
        OnChange?.Invoke();
    }

    private async Task<string?> GetAsync(string key)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch { return null; }
    }

    private async Task SetAsync(string key, string value) =>
        await _js.InvokeVoidAsync("localStorage.setItem", key, value);

    private async Task RemoveAsync(string key) =>
        await _js.InvokeVoidAsync("localStorage.removeItem", key);
}
