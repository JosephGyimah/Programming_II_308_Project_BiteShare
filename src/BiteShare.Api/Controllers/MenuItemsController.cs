using BiteShare.Data;
using BiteShare.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

public record MenuItemDto(Guid Id, string Name, string? Description, decimal Price, bool Available);
public record AddMenuItemRequest(string Name, string? Description, decimal Price);

[ApiController]
[Route("api/sessions/{sessionId:guid}/[controller]")]
[Authorize]
public class MenuItemsController : ControllerBase
{
    private readonly BiteShareDbContext _db;

    public MenuItemsController(BiteShareDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetForSession(Guid sessionId)
    {
        var items = await _db.MenuItems
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Name)
            .ToListAsync();

        return Ok(items.Select(m => new MenuItemDto(m.Id, m.Name, m.Description, m.Price, m.Available)));
    }

    /// <summary>Host adds a menu item (e.g. transcribed from the restaurant's menu) for participants to order from.</summary>
    [HttpPost]
    public async Task<ActionResult<MenuItemDto>> Add(Guid sessionId, AddMenuItemRequest request)
    {
        var caller = Services.CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId || !caller.IsHost)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
            return BadRequest("A menu item needs a name and a non-negative price.");

        var item = new MenuItem
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Available = true
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        return Ok(new MenuItemDto(item.Id, item.Name, item.Description, item.Price, item.Available));
    }
}
