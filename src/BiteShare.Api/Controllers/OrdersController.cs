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
public class OrdersController : ControllerBase
{
    private static readonly HashSet<string> ValidStatusTransitions = new()
    {
        "Preparing", "OutForDelivery", "Delivered"
    };

    private readonly BiteShareDbContext _db;
    private readonly ISplitterService _splitter;
    private readonly IStripePaymentService _payments;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(BiteShareDbContext db, ISplitterService splitter, IStripePaymentService payments, IHubContext<OrderHub> hub, ILogger<OrdersController> logger)
    {
        _db = db;
        _splitter = splitter;
        _payments = payments;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// Host submits the session's cart as an order: computes the split
    /// (equal vs per-item — see SplitMode), captures payment per participant
    /// via Stripe, and kicks off the status pipeline.
    /// </summary>
    [HttpPost("submit")]
    public async Task<ActionResult<OrderSummaryDto>> Submit(Guid sessionId, SubmitOrderRequest request)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId || !caller.IsHost)
            return Forbid();

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound();
        if (session.Status != SessionStatus.Open) return BadRequest("This session has already been submitted.");

        if (!Enum.TryParse<SplitModeOption>(request.SplitMode, ignoreCase: true, out var splitModeOption))
            return BadRequest("SplitMode must be 'Equal' or 'PerItem'.");

        var cartRows = await (
            from c in _db.CartItems
            join p in _db.Participants on c.ParticipantId equals p.Id
            join m in _db.MenuItems on c.MenuItemId equals m.Id
            where p.SessionId == sessionId
            select new { p.Id, LineTotal = m.Price * c.Quantity }
        ).ToListAsync();

        // Zero orders: don't throw — submit an empty order so the host sees it reflected.
        var participantTotals = cartRows
            .GroupBy(r => r.Id)
            .Select(g => new ParticipantCartTotal(g.Key, g.Sum(r => r.LineTotal)))
            .ToList();

        var subtotal = participantTotals.Sum(p => p.Subtotal);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Status = OrderStatus.Confirmed,
            Subtotal = subtotal,
            Tax = request.Tax,
            Tip = request.Tip,
            DeliveryFee = request.DeliveryFee,
            SplitMode = splitModeOption == SplitModeOption.Equal ? SplitMode.Equal : SplitMode.PerItem
        };
        _db.Orders.Add(order);

        var splits = _splitter.Split(participantTotals, request.Tax, request.Tip, request.DeliveryFee, splitModeOption);

        var receipts = new List<Receipt>();
        foreach (var split in splits)
        {
            var captured = false;
            string? paymentIntentId = null;

            if (request.PaymentMethodIds.TryGetValue(split.ParticipantId, out var paymentMethodId) && !string.IsNullOrWhiteSpace(paymentMethodId))
            {
                var result = await _payments.CapturePaymentAsync(split.AmountOwed, "usd", paymentMethodId, $"BiteShare order for session {session.Name}", HttpContext.RequestAborted);
                captured = result.Succeeded;
                paymentIntentId = result.PaymentIntentId;
                if (!result.Succeeded)
                    _logger.LogWarning("Payment capture failed for participant {ParticipantId}: {Error}", split.ParticipantId, result.ErrorMessage);
            }

            receipts.Add(new Receipt
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ParticipantId = split.ParticipantId,
                AmountOwed = split.AmountOwed,
                PaymentCaptured = captured,
                StripePaymentIntentId = paymentIntentId,
                PaidAt = captured ? DateTime.UtcNow : null
            });
        }
        _db.Receipts.AddRange(receipts);

        session.Status = SessionStatus.Confirmed;
        await _db.SaveChangesAsync();

        var participantNames = await _db.Participants.Where(p => p.SessionId == sessionId).ToDictionaryAsync(p => p.Id, p => p.DisplayName);
        var receiptDtos = receipts.Select(r => new ReceiptDto(r.ParticipantId, participantNames.GetValueOrDefault(r.ParticipantId, "Unknown"), r.AmountOwed, r.PaymentCaptured)).ToList();

        await _hub.Clients.Group(OrderHub.SessionGroup(sessionId))
            .SendAsync("OrderStatusChanged", new OrderStatusUpdate(sessionId, order.Id, order.Status.ToString()));

        return Ok(new OrderSummaryDto(order.Id, sessionId, order.Status.ToString(), order.Subtotal, order.Tax, order.Tip, order.DeliveryFee, receiptDtos));
    }

    /// <summary>
    /// Advances confirmed -> preparing -> out for delivery -> delivered
    /// and pushes the update via OrderHub.
    /// </summary>
    [HttpPost("{orderId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid sessionId, Guid orderId, [FromBody] string status)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId || !caller.IsHost)
            return Forbid();

        if (!ValidStatusTransitions.Contains(status))
            return BadRequest($"Status must be one of: {string.Join(", ", ValidStatusTransitions)}.");

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.SessionId == sessionId);
        if (order is null) return NotFound();

        if (!Enum.TryParse<OrderStatus>(status, out var newStatus))
            return BadRequest("Unrecognized status.");

        order.Status = newStatus;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(OrderHub.SessionGroup(sessionId))
            .SendAsync("OrderStatusChanged", new OrderStatusUpdate(sessionId, order.Id, order.Status.ToString()));

        return NoContent();
    }
}
