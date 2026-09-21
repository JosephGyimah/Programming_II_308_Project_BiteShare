using System.Security.Claims;

namespace BiteShare.Api.Services;

/// <summary>Convenience wrapper around the claims on a participant-scoped JWT.</summary>
public record CurrentParticipant(Guid ParticipantId, Guid SessionId, string DisplayName, bool IsHost)
{
    public static CurrentParticipant? FromUser(ClaimsPrincipal user)
    {
        var participantIdClaim = user.FindFirst(BiteShareClaimTypes.ParticipantId)?.Value;
        var sessionIdClaim = user.FindFirst(BiteShareClaimTypes.SessionId)?.Value;

        if (participantIdClaim is null || sessionIdClaim is null)
            return null;
        if (!Guid.TryParse(participantIdClaim, out var participantId))
            return null;
        if (!Guid.TryParse(sessionIdClaim, out var sessionId))
            return null;

        var displayName = user.FindFirst(BiteShareClaimTypes.DisplayName)?.Value ?? string.Empty;
        var isHost = user.FindFirst(BiteShareClaimTypes.IsHost)?.Value == "true";

        return new CurrentParticipant(participantId, sessionId, displayName, isHost);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public static string? GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirst(BiteShareClaimTypes.DisplayName)?.Value;
}
