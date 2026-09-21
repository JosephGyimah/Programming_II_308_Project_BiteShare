namespace BiteShare.Shared.Models;

public class Receipt
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ParticipantId { get; set; }
    public decimal AmountOwed { get; set; }
    public bool PaymentCaptured { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime? PaidAt { get; set; }
}
