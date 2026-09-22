namespace BiteShare.Api.Services;

public class SplitterService : ISplitterService
{
    public IReadOnlyList<SplitResult> Split(
        IReadOnlyList<ParticipantCartTotal> participantTotals,
        decimal tax,
        decimal tip,
        decimal deliveryFee,
        SplitModeOption splitMode)
    {
        // Zero orders: nothing to split, don't throw.
        var participants = participantTotals.Where(p => p.Subtotal > 0m).ToList();
        if (participants.Count == 0)
            return Array.Empty<SplitResult>();

        var subtotal = participants.Sum(p => p.Subtotal);
        var grandTotal = subtotal + tax + tip + deliveryFee;

        return splitMode switch
        {
            SplitModeOption.Equal => SplitEqual(participants, grandTotal),
            SplitModeOption.PerItem => SplitPerItem(participants, subtotal, grandTotal),
            _ => throw new ArgumentOutOfRangeException(nameof(splitMode))
        };
    }

    private static List<SplitResult> SplitEqual(List<ParticipantCartTotal> participants, decimal grandTotal)
    {
        var ordered = participants.OrderBy(p => p.ParticipantId).ToList();
        var n = ordered.Count;

        // Work in whole cents to avoid floating remainder drift, then allocate the
        // leftover cents one at a time (deterministic order) so the sum always
        // equals the grand total exactly.
        var totalCents = decimal.Round(grandTotal * 100m, 0, MidpointRounding.AwayFromZero);
        var baseCents = (long)totalCents / n;
        var remainderCents = (long)totalCents % n;

        var results = new List<SplitResult>(n);
        for (var i = 0; i < n; i++)
        {
            var cents = baseCents + (i < remainderCents ? 1 : 0);
            results.Add(new SplitResult(ordered[i].ParticipantId, cents / 100m));
        }
        return results;
    }

    private static List<SplitResult> SplitPerItem(List<ParticipantCartTotal> participants, decimal subtotal, decimal grandTotal)
    {
        var ordered = participants.OrderBy(p => p.ParticipantId).ToList();

        // Each participant's share of tax/tip/delivery is proportional to their share
        // of the subtotal. Compute in cents, then patch the rounding remainder onto
        // the last participant (by ParticipantId order) so the totals reconcile exactly.
        var totalCents = decimal.Round(grandTotal * 100m, 0, MidpointRounding.AwayFromZero);

        var raw = ordered
            .Select(p => (p.ParticipantId, Cents: subtotal == 0m ? 0m : (p.Subtotal / subtotal) * totalCents))
            .ToList();

        var floored = raw.Select(r => ((long)Math.Floor(r.Cents))).ToList();
        var allocated = floored.Sum();
        var remainder = (long)totalCents - allocated;

        // Give the leftover cents to the participants with the largest fractional
        // remainder first (largest-remainder method) — the standard way to make
        // proportional rounding add back up to the total.
        var fractionalOrder = raw
            .Select((r, i) => (Index: i, Fraction: r.Cents - floored[i]))
            .OrderByDescending(x => x.Fraction)
            .ThenBy(x => ordered[x.Index].ParticipantId)
            .ToList();

        var cents = floored.ToArray();
        for (var i = 0; i < remainder && i < fractionalOrder.Count; i++)
            cents[fractionalOrder[i].Index] += 1;

        var results = new List<SplitResult>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
            results.Add(new SplitResult(ordered[i].ParticipantId, cents[i] / 100m));
        return results;
    }
}
