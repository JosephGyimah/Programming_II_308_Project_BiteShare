using BiteShare.Api.Services;
using BiteShare.Data;
using BiteShare.Shared.DTOs;
using BiteShare.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly BiteShareDbContext _db;
    private readonly IJoinCodeGenerator _joinCodeGenerator;
    private readonly IJwtTokenService _tokenService;

    public SessionsController(BiteShareDbContext db, IJoinCodeGenerator joinCodeGenerator, IJwtTokenService tokenService)
    {
        _db = db;
        _joinCodeGenerator = joinCodeGenerator;
        _tokenService = tokenService;
    }

    /// <summary>Host creates a session. Requires a real account (identity token).</summary>
    [HttpPost]
    [Authorize(Policy = "IdentityOnly")]
    public async Task<ActionResult<SessionWithTokenDto>> Create(CreateSessionRequest request)
    {
        var userId = User.GetUserId();
        var displayName = User.GetDisplayName() ?? "Host";
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Session name is required.");

        string joinCode;
        do
        {
            joinCode = _joinCodeGenerator.Generate();
        } while (await _db.Sessions.AnyAsync(s => s.JoinCode == joinCode));

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            HostUserId = userId,
            JoinCode = joinCode,
            Status = SessionStatus.Open,
            OrderDeadlineUtc = request.OrderDeadlineUtc
        };

        var hostParticipant = new Participant
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            DisplayName = displayName,
            UserId = userId,
            IsHost = true
        };

        _db.Sessions.Add(session);
        _db.Participants.Add(hostParticipant);
        await _db.SaveChangesAsync();

        var (participantToken, _) = _tokenService.CreateParticipantToken(hostParticipant.Id, session.Id, displayName, isHost: true);

        return Ok(new SessionWithTokenDto(
            new SessionSummaryDto(session.Id, session.Name, session.JoinCode, session.Status.ToString(), 1),
            participantToken,
            hostParticipant.Id));
    }

    /// <summary>Authenticated user joins an existing session by code (host already has a participant row from Create).</summary>
    [HttpPost("join")]
    [Authorize(Policy = "IdentityOnly")]
    public async Task<ActionResult<SessionWithTokenDto>> JoinByCode(JoinSessionRequest request)
    {
        var userId = User.GetUserId();
        var displayName = User.GetDisplayName() ?? "Participant";
        if (userId is null) return Unauthorized();

        var session = await _db.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.JoinCode == request.JoinCode.ToUpperInvariant());
        if (session is null) return NotFound("No session found for that join code.");
        if (session.Status != SessionStatus.Open) return BadRequest("This session is no longer accepting participants.");

        var existing = session.Participants.FirstOrDefault(p => p.UserId == userId);
        Participant participant;
        if (existing is not null)
        {
            participant = existing;
        }
        else
        {
            participant = new Participant
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                DisplayName = displayName,
                UserId = userId,
                IsHost = false
            };
            _db.Participants.Add(participant);
            await _db.SaveChangesAsync();
        }

        var (participantToken, _) = _tokenService.CreateParticipantToken(participant.Id, session.Id, participant.DisplayName, participant.IsHost);

        return Ok(new SessionWithTokenDto(
            new SessionSummaryDto(session.Id, session.Name, session.JoinCode, session.Status.ToString(), session.Participants.Count + (existing is null ? 1 : 0)),
            participantToken,
            participant.Id));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<SessionSummaryDto>> Get(Guid id)
    {
        var session = await _db.Sessions.Include(s => s.Participants).FirstOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();

        return Ok(new SessionSummaryDto(session.Id, session.Name, session.JoinCode, session.Status.ToString(), session.Participants.Count));
    }

    [HttpGet]
    [Authorize(Policy = "IdentityOnly")]
    public async Task<ActionResult<IEnumerable<SessionSummaryDto>>> GetMine()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var sessions = await _db.Sessions
            .Where(s => s.HostUserId == userId || s.Participants.Any(p => p.UserId == userId))
            .Include(s => s.Participants)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sessions.Select(s => new SessionSummaryDto(s.Id, s.Name, s.JoinCode, s.Status.ToString(), s.Participants.Count)));
    }
}
