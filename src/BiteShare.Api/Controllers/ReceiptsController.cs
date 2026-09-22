using BiteShare.Api.Services;
using BiteShare.Data;
using BiteShare.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}/orders/{orderId:guid}/[controller]")]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly BiteShareDbContext _db;
    private readonly IReceiptPdfService _pdfService;

    public ReceiptsController(BiteShareDbContext db, IReceiptPdfService pdfService)
    {
        _db = db;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceipts(Guid sessionId, Guid orderId)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId) return Forbid();

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.SessionId == sessionId);
        if (order is null) return NotFound();

        var receipts = await (
            from r in _db.Receipts
            join p in _db.Participants on r.ParticipantId equals p.Id
            where r.OrderId == orderId
            select new ReceiptDto(r.ParticipantId, p.DisplayName, r.AmountOwed, r.PaymentCaptured)
        ).ToListAsync();

        return Ok(receipts);
    }

    /// <summary>Generates the itemized PDF receipt (QuestPDF).</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf(Guid sessionId, Guid orderId)
    {
        var caller = CurrentParticipant.FromUser(User);
        if (caller is null || caller.SessionId != sessionId) return Forbid();

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.SessionId == sessionId);
        if (session is null || order is null) return NotFound();

        var lines = await (
            from r in _db.Receipts
            join p in _db.Participants on r.ParticipantId equals p.Id
            where r.OrderId == orderId
            select new ReceiptPdfLine(p.DisplayName, r.AmountOwed, r.PaymentCaptured)
        ).ToListAsync();

        var pdfBytes = _pdfService.GenerateItemizedReceipt(session.Name, order.SubmittedAt, order.Subtotal, order.Tax, order.Tip, order.DeliveryFee, lines);

        return File(pdfBytes, "application/pdf", $"biteshare-receipt-{orderId}.pdf");
    }
}
