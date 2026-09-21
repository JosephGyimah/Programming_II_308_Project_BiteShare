namespace BiteShare.Shared.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, DateTime ExpiresAtUtc, string UserId, string DisplayName);
public record GuestJoinRequest(string JoinCode, string DisplayName);
public record GuestJoinResponse(string GuestToken, Guid ParticipantId, Guid SessionId);
