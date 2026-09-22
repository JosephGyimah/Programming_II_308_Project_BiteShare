namespace BiteShare.Api.Services;

public record ParticipantCartTotal(Guid ParticipantId, decimal Subtotal);
public record SplitResult(Guid ParticipantId, decimal AmountOwed);

public interface ISplitterService
{
    /// <summary>
    /// Splits an order total across participants under either split mode.
    /// - Equal: subtotal + tax + tip + deliveryFee divided evenly across everyone with items in the cart.
    /// - PerItem: each participant pays their own item subtotal plus a proportional share
    ///   of tax/tip/deliveryFee based on their fraction of the overall subtotal.
    /// Rounding remainders (from splitting cents) are allocated one cent at a time, in
    /// ParticipantId order, so totals always reconcile exactly to the order total.
    /// </summary>
    IReadOnlyList<SplitResult> Split(
        IReadOnlyList<ParticipantCartTotal> participantTotals,
        decimal tax,
        decimal tip,
        decimal deliveryFee,
        SplitModeOption splitMode);
}

public enum SplitModeOption
{
    Equal,
    PerItem
}
