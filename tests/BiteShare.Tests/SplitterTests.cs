using BiteShare.Api.Services;
using Xunit;

namespace BiteShare.Tests;

/// <summary>
/// Cost-splitter logic is the highest-risk area for silent bugs (Phase 3, Obadiah).
/// Covers the edge cases the execution guide calls out explicitly: rounding
/// remainders and zero-order sessions, plus the two split modes themselves.
/// </summary>
public class SplitterTests
{
    private readonly SplitterService _sut = new();

    [Fact]
    public void EqualSplit_DividesTotalEvenlyAcrossParticipants()
    {
        var participants = new[]
        {
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000001"), 10m),
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000002"), 20m)
        };

        var result = _sut.Split(participants, tax: 0m, tip: 0m, deliveryFee: 0m, SplitModeOption.Equal);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(15m, r.AmountOwed));
        Assert.Equal(30m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void PerItemSplit_ChargesEachParticipantForTheirOwnItems()
    {
        var participants = new[]
        {
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000001"), 10m),
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000002"), 30m)
        };

        // $4 tax on a $40 subtotal: participant 1 (25% of subtotal) owes $1 tax, participant 2 owes $3.
        var result = _sut.Split(participants, tax: 4m, tip: 0m, deliveryFee: 0m, SplitModeOption.PerItem);

        var p1 = result.Single(r => r.ParticipantId == participants[0].ParticipantId);
        var p2 = result.Single(r => r.ParticipantId == participants[1].ParticipantId);

        Assert.Equal(11m, p1.AmountOwed);
        Assert.Equal(33m, p2.AmountOwed);
    }

    [Fact]
    public void Split_WithRoundingRemainder_AllocatesRemainderConsistently()
    {
        // $10 split three ways doesn't divide evenly into cents — every cent must
        // still be accounted for, and the same input must always split the same way.
        var participants = new[]
        {
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000001"), 10m / 3m),
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000002"), 10m / 3m),
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000003"), 10m / 3m)
        };

        var result = _sut.Split(participants, tax: 0m, tip: 0m, deliveryFee: 0m, SplitModeOption.Equal);
        var resultAgain = _sut.Split(participants, tax: 0m, tip: 0m, deliveryFee: 0m, SplitModeOption.Equal);

        Assert.Equal(3, result.Count);
        Assert.Equal(result.Sum(r => r.AmountOwed), resultAgain.Sum(r => r.AmountOwed));
        // Every cent of the total is allocated to someone.
        var total = participants.Sum(p => p.Subtotal);
        Assert.Equal(decimal.Round(total, 2, MidpointRounding.AwayFromZero), decimal.Round(result.Sum(r => r.AmountOwed), 2));
    }

    [Fact]
    public void Split_WithZeroOrders_DoesNotThrow()
    {
        var result = _sut.Split(Array.Empty<ParticipantCartTotal>(), tax: 0m, tip: 0m, deliveryFee: 0m, SplitModeOption.Equal);

        Assert.Empty(result);
    }

    [Fact]
    public void Split_ParticipantWithNoItems_OwesNothing()
    {
        var participants = new[]
        {
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000001"), 20m),
            new ParticipantCartTotal(Guid.Parse("00000000-0000-0000-0000-000000000002"), 0m) // joined but never ordered
        };

        var result = _sut.Split(participants, tax: 0m, tip: 0m, deliveryFee: 0m, SplitModeOption.Equal);

        Assert.Single(result);
        Assert.Equal(20m, result[0].AmountOwed);
    }
}
