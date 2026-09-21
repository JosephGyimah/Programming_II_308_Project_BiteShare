namespace BiteShare.Api.Services;

public enum SplitModeOption
{
    Equal,
    PerItem
}

public record ParticipantCartTotal(Guid ParticipantId, decimal Subtotal);

public record ParticipantShare(Guid ParticipantId, decimal AmountOwed);

public interface ISplitterService
{
    /// <summary>
    /// Splits an order's grand total across participants.
    /// Equal divides everything evenly among participants who actually ordered
    /// something; PerItem charges each participant their own subtotal plus a
    /// proportional share of tax/tip/delivery fee.
    /// </summary>
    IReadOnlyList<ParticipantShare> Split(
        IReadOnlyList<ParticipantCartTotal> participants,
        decimal tax,
        decimal tip,
        decimal deliveryFee,
        SplitModeOption mode);
}
