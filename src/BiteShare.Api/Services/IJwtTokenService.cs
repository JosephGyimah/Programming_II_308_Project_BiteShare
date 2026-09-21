using System.Security.Claims;
using BiteShare.Data;

namespace BiteShare.Api.Services;

public interface IJwtTokenService
{
    /// <summary>Token for a registered/logged-in user. Used against /api/sessions (host actions).</summary>
    (string Token, DateTime ExpiresAtUtc) CreateIdentityToken(ApplicationUser user);

    /// <summary>
    /// Token scoped to one Session as one Participant — used for guests AND for
    /// authenticated users once they've joined/created a session. This is the
    /// token sent to session-scoped endpoints (cart, orders, participants) and
    /// to the OrderHub SignalR connection.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) CreateParticipantToken(Guid participantId, Guid sessionId, string displayName, bool isHost);
}
