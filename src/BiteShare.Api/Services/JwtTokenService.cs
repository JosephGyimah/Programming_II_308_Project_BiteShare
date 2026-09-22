using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BiteShare.Data;
using Microsoft.IdentityModel.Tokens;

namespace BiteShare.Api.Services;

public static class BiteShareClaimTypes
{
    public const string TokenType = "token_type";
    public const string ParticipantId = "participant_id";
    public const string SessionId = "session_id";
    public const string DisplayName = "display_name";
    public const string IsHost = "is_host";
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateIdentityToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(BiteShareClaimTypes.DisplayName, user.DisplayName),
            new(BiteShareClaimTypes.TokenType, "identity"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return CreateToken(claims);
    }

    public (string Token, DateTime ExpiresAtUtc) CreateParticipantToken(Guid participantId, Guid sessionId, string displayName, bool isHost)
    {
        var claims = new List<Claim>
        {
            new(BiteShareClaimTypes.ParticipantId, participantId.ToString()),
            new(BiteShareClaimTypes.SessionId, sessionId.ToString()),
            new(BiteShareClaimTypes.DisplayName, displayName),
            new(BiteShareClaimTypes.IsHost, isHost ? "true" : "false"),
            new(BiteShareClaimTypes.TokenType, "participant"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return CreateToken(claims);
    }

    private (string Token, DateTime ExpiresAtUtc) CreateToken(List<Claim> claims)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var signingKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured. Set it via user-secrets or environment variables — never commit it.");
        var expiryMinutes = jwtSection.GetValue<int?>("ExpiryMinutes") ?? 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
