namespace BiteShare.Api.Services;

public record ReceiptPdfLine(string ParticipantName, decimal AmountOwed, bool PaymentCaptured);

public interface IReceiptPdfService
{
    byte[] GenerateItemizedReceipt(
        string sessionName,
        DateTime submittedAtUtc,
        decimal subtotal,
        decimal tax,
        decimal tip,
        decimal deliveryFee,
        IReadOnlyList<ReceiptPdfLine> lines);
}
