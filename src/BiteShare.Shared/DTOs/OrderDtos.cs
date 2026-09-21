namespace BiteShare.Shared.DTOs;

/// <summary>
/// PaymentMethodIds maps ParticipantId -> a Stripe PaymentMethod id collected
/// client-side (via Stripe.js) before the host submits. A participant with no
/// entry is skipped for payment capture (their receipt is generated as unpaid,
/// so the host can chase it up outside the app).
/// </summary>
public record SubmitOrderRequest(string SplitMode, decimal Tax, decimal Tip, decimal DeliveryFee, Dictionary<Guid, string> PaymentMethodIds);
public record OrderStatusUpdate(Guid SessionId, Guid OrderId, string Status);
public record ReceiptDto(Guid ParticipantId, string ParticipantName, decimal AmountOwed, bool PaymentCaptured);
public record OrderSummaryDto(Guid OrderId, Guid SessionId, string Status, decimal Subtotal, decimal Tax, decimal Tip, decimal DeliveryFee, IReadOnlyList<ReceiptDto> Receipts);
