using BiteShare.Api.Services;
using BiteShare.Data;
using BiteShare.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly BiteShareDbContext _db;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService tokenService, BiteShareDbContext db)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Email, password, and display name are all required.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        var (token, expires) = _tokenService.CreateIdentityToken(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        var (token, expires) = _tokenService.CreateIdentityToken(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.DisplayName));
    }

    /// <summary>
    /// "Join without account" flow — no registration required. Issues a
    /// participant-scoped token, not a full identity token.
    /// </summary>
    [HttpPost("guest-join")]
    public async Task<ActionResult<GuestJoinResponse>> GuestJoin(GuestJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Display name is required.");

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.JoinCode == request.JoinCode.ToUpperInvariant());
        if (session is null)
            return NotFound("No session found for that join code.");
        if (session.Status != Shared.Models.SessionStatus.Open)
            return BadRequest("This session is no longer accepting participants.");

        var guestToken = Guid.NewGuid().ToString("N");
        var participant = new Shared.Models.Participant
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            DisplayName = request.DisplayName,
            GuestToken = guestToken,
            IsHost = false
        };

        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        var (jwt, _) = _tokenService.CreateParticipantToken(participant.Id, session.Id, participant.DisplayName, isHost: false);
        return Ok(new GuestJoinResponse(jwt, participant.Id, session.Id));
    }
}
