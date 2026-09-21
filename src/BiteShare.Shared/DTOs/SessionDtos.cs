namespace BiteShare.Shared.DTOs;

public record CreateSessionRequest(string Name, DateTime? OrderDeadlineUtc);
public record SessionSummaryDto(Guid Id, string Name, string JoinCode, string Status, int ParticipantCount);
public record JoinSessionRequest(string JoinCode);

/// <summary>Returned whenever a session is created or joined — bundles the session
/// summary with the participant-scoped token the client should use for
/// cart/order/participant calls and the SignalR hub connection.</summary>
public record SessionWithTokenDto(SessionSummaryDto Session, string ParticipantToken, Guid ParticipantId);
