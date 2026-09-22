using BiteShare.Api.Hubs;
using BiteShare.Api.Services;
using BiteShare.Data;
using BiteShare.Shared.DTOs;
using BiteShare.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly BiteShareDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public CartController(BiteShareDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost]
    public async Task<ActionResult<CartItemDto>> AddItem(Guid sessionId, AddCartItemRequest request)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId) return Forbid();

        var menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == request.MenuItemId && m.SessionId == sessionId);
        if (menuItem is null) return NotFound("Menu item not found for this session.");
        if (!menuItem.Available) return BadRequest("That item is no longer available.");
        if (request.Quantity < 1) return BadRequest("Quantity must be at least 1.");

        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            ParticipantId = caller.ParticipantId,
            MenuItemId = menuItem.Id,
            Quantity = request.Quantity,
            Notes = request.Notes
        };

        _db.CartItems.Add(cartItem);
        await _db.SaveChangesAsync();

        var dto = new CartItemDto(cartItem.Id, caller.ParticipantId, caller.DisplayName, menuItem.Id, menuItem.Name, menuItem.Price, cartItem.Quantity, cartItem.Notes);

        await _hub.Clients.Group(OrderHub.SessionGroup(sessionId))
            .SendAsync("CartUpdated", new CartEvent("added", sessionId, dto));

        return Ok(dto);
    }

    [HttpDelete("{cartItemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid sessionId, Guid cartItemId)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId) return Forbid();

        var cartItem = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.ParticipantId == caller.ParticipantId);
        if (cartItem is null) return NotFound();

        var menuItem = await _db.MenuItems.FirstAsync(m => m.Id == cartItem.MenuItemId);
        var removedDto = new CartItemDto(cartItem.Id, caller.ParticipantId, caller.DisplayName, menuItem.Id, menuItem.Name, menuItem.Price, cartItem.Quantity, cartItem.Notes);

        _db.CartItems.Remove(cartItem);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(OrderHub.SessionGroup(sessionId))
            .SendAsync("CartUpdated", new CartEvent("removed", sessionId, removedDto));

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CartItemDto>>> GetCart(Guid sessionId)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId) return Forbid();

        var items = await (
            from c in _db.CartItems
            join p in _db.Participants on c.ParticipantId equals p.Id
            join m in _db.MenuItems on c.MenuItemId equals m.Id
            where p.SessionId == sessionId
            select new CartItemDto(c.Id, p.Id, p.DisplayName, m.Id, m.Name, m.Price, c.Quantity, c.Notes)
        ).ToListAsync();

        return Ok(items);
    }
}
