namespace BiteShare.Api.Services;

public class SplitterService : ISplitterService
{
    public IReadOnlyList<ParticipantShare> Split(
        IReadOnlyList<ParticipantCartTotal> participants,
        decimal tax,
        decimal tip,
        decimal deliveryFee,
        SplitModeOption mode)
    {
        if (participants.Count == 0)
        {
            return Array.Empty<ParticipantShare>();
        }

        return mode switch
        {
            SplitModeOption.Equal => SplitEqual(participants, tax, tip, deliveryFee),
            SplitModeOption.PerItem => SplitPerItem(participants, tax, tip, deliveryFee),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static IReadOnlyList<ParticipantShare> SplitEqual(
        IReadOnlyList<ParticipantCartTotal> participants,
        decimal tax,
        decimal tip,
        decimal deliveryFee)
    {
        // Only participants who actually ordered something (subtotal > 0) share the bill;
        // someone who joined but never ordered owes nothing and isn't included.
        var eligible = participants.Where(p => p.Subtotal > 0m).ToList();
        if (eligible.Count == 0)
        {
            return Array.Empty<ParticipantShare>();
        }

        var total = participants.Sum(p => p.Subtotal) + tax + tip + deliveryFee;
        var shares = AllocateCents(ToCents(total), eligible.Count);

        var result = new List<ParticipantShare>(eligible.Count);
        for (var i = 0; i < eligible.Count; i++)
        {
            result.Add(new ParticipantShare(eligible[i].ParticipantId, FromCents(shares[i])));
        }

        return result;
    }

    private static IReadOnlyList<ParticipantShare> SplitPerItem(
        IReadOnlyList<ParticipantCartTotal> participants,
        decimal tax,
        decimal tip,
        decimal deliveryFee)
    {
        var extra = tax + tip + deliveryFee;
        var subtotalTotal = participants.Sum(p => p.Subtotal);
        var extraCents = ToCents(extra);

        long[] extraShareCents;
        if (subtotalTotal <= 0m || extraCents == 0)
        {
            extraShareCents = new long[participants.Count];
        }
        else
        {
            var rawShares = participants
                .Select(p => extraCents * (double)(p.Subtotal / subtotalTotal))
                .ToArray();
            extraShareCents = AllocateWeighted(extraCents, rawShares);
        }

        var result = new List<ParticipantShare>(participants.Count);
        for (var i = 0; i < participants.Count; i++)
        {
            var amount = participants[i].Subtotal + FromCents(extraShareCents[i]);
            result.Add(new ParticipantShare(participants[i].ParticipantId, amount));
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="totalCents"/> into <paramref name="count"/> shares as evenly as
    /// possible; any leftover cents go to the first participants in order, so the same input
    /// always produces the same split and nothing is lost to rounding.
    /// </summary>
    private static long[] AllocateCents(long totalCents, int count)
    {
        var baseShare = totalCents / count;
        var remainder = totalCents - baseShare * count;

        var shares = new long[count];
        for (var i = 0; i < count; i++)
        {
            shares[i] = baseShare + (i < remainder ? 1 : 0);
        }

        return shares;
    }

    /// <summary>Largest-remainder allocation of <paramref name="totalCents"/> across weighted raw shares.</summary>
    private static long[] AllocateWeighted(long totalCents, double[] rawShares)
    {
        var floors = rawShares.Select(r => (long)Math.Floor(r)).ToArray();
        var remainder = totalCents - floors.Sum();

        var order = Enumerable.Range(0, rawShares.Length)
            .OrderByDescending(i => rawShares[i] - floors[i])
            .ToArray();

        var shares = (long[])floors.Clone();
        for (var i = 0; i < remainder && i < order.Length; i++)
        {
            shares[order[i]]++;
        }

        return shares;
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static decimal FromCents(long cents) => cents / 100m;
}
