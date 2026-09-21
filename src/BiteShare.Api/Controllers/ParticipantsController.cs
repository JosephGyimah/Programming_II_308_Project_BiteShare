using BiteShare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

public record ParticipantDto(Guid Id, string DisplayName, bool IsHost, bool IsGuest);

[ApiController]
[Route("api/sessions/{sessionId:guid}/[controller]")]
[Authorize]
public class ParticipantsController : ControllerBase
{
    private readonly BiteShareDbContext _db;

    public ParticipantsController(BiteShareDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParticipantDto>>> GetForSession(Guid sessionId)
    {
        var participants = await _db.Participants
            .Where(p => p.SessionId == sessionId)
            .OrderByDescending(p => p.IsHost)
            .ThenBy(p => p.JoinedAt)
            .ToListAsync();

        return Ok(participants.Select(p => new ParticipantDto(p.Id, p.DisplayName, p.IsHost, p.GuestToken != null)));
    }

    /// <summary>Host removes a participant from the session (e.g. before the deadline).</summary>
    [HttpDelete("{participantId:guid}")]
    public async Task<IActionResult> Remove(Guid sessionId, Guid participantId)
    {
        var caller = Services.CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId || !caller.IsHost)
            return Forbid();

        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Id == participantId && p.SessionId == sessionId);
        if (participant is null) return NotFound();
        if (participant.IsHost) return BadRequest("The host cannot be removed.");

        _db.Participants.Remove(participant);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
