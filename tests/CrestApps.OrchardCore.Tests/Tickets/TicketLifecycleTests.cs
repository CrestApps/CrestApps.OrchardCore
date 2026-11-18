using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The rules of a ticket's life: a closed ticket is final and a solved one can reopen; a merged or closed ticket is
/// read-only; a requester's reply reopens a solved ticket inside the desk's window and starts a follow-up after it; a
/// solved ticket closes after the desk's delay; the first status of a category is the lowest in order.
/// </summary>
public sealed class TicketLifecycleTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TicketStatusCategory.New, TicketStatusCategory.Solved, true)]
    [InlineData(TicketStatusCategory.Solved, TicketStatusCategory.Open, true)]
    [InlineData(TicketStatusCategory.Pending, TicketStatusCategory.New, false)]
    [InlineData(TicketStatusCategory.Closed, TicketStatusCategory.Open, false)]
    [InlineData(TicketStatusCategory.Open, TicketStatusCategory.Open, true)]
    public void Moves_FollowTheTable(TicketStatusCategory from, TicketStatusCategory to, bool allowed)
        => Assert.Equal(allowed, TicketLifecycle.Transitions.CanMove(from, to));

    [Fact]
    public void ClosedOrMergedTickets_AreReadOnly()
    {
        Assert.True(TicketLifecycle.IsReadOnly(new TicketPart { StatusCategory = TicketStatusCategory.Closed }));
        Assert.True(TicketLifecycle.IsReadOnly(new TicketPart { StatusCategory = TicketStatusCategory.Open, MergedIntoId = "t2" }));
        Assert.False(TicketLifecycle.IsReadOnly(new TicketPart { StatusCategory = TicketStatusCategory.Solved }));
    }

    [Fact]
    public void ARequesterReply_ReopensInsideTheWindow_AndStartsAFollowUpAfterIt()
    {
        // Arrange
        var desk = new TicketDesk { ReopenWindowDays = 7 };
        var recent = new TicketPart { StatusCategory = TicketStatusCategory.Solved, SolvedUtc = _now.AddDays(-3) };
        var old = new TicketPart { StatusCategory = TicketStatusCategory.Solved, SolvedUtc = _now.AddDays(-8) };
        var closed = new TicketPart { StatusCategory = TicketStatusCategory.Closed };
        var open = new TicketPart { StatusCategory = TicketStatusCategory.Open };

        // Act + Assert
        Assert.True(TicketLifecycle.ReopensOnRequesterReply(recent, desk, _now));
        Assert.False(TicketLifecycle.StartsFollowUp(recent, desk, _now));
        Assert.False(TicketLifecycle.ReopensOnRequesterReply(old, desk, _now));
        Assert.True(TicketLifecycle.StartsFollowUp(old, desk, _now));
        Assert.True(TicketLifecycle.StartsFollowUp(closed, desk, _now));
        Assert.False(TicketLifecycle.StartsFollowUp(open, desk, _now));
    }

    [Fact]
    public void ASolvedTicket_ClosesAfterTheDesksDelay_OnlyWhenTheDeskCloses()
    {
        var waited = new TicketPart { StatusCategory = TicketStatusCategory.Solved, SolvedUtc = _now.AddDays(-5) };
        var fresh = new TicketPart { StatusCategory = TicketStatusCategory.Solved, SolvedUtc = _now.AddDays(-1) };

        Assert.True(TicketLifecycle.ShouldAutoClose(waited, new TicketDesk { AutoCloseDays = 4 }, _now));
        Assert.False(TicketLifecycle.ShouldAutoClose(fresh, new TicketDesk { AutoCloseDays = 4 }, _now));
        Assert.False(TicketLifecycle.ShouldAutoClose(waited, new TicketDesk { AutoCloseDays = 0 }, _now));
        Assert.False(TicketLifecycle.ShouldAutoClose(waited, null, _now));
    }

    [Fact]
    public void TheFirstStatusOfACategory_IsTheLowestInOrder()
    {
        TicketStatus[] statuses =
        [
            new() { ItemId = "b", Name = "Waiting on vendor", Category = TicketStatusCategory.OnHold, Order = 20 },
            new() { ItemId = "a", Name = "Open", Category = TicketStatusCategory.Open, Order = 10 },
            new() { ItemId = "c", Name = "In progress", Category = TicketStatusCategory.Open, Order = 5 },
        ];

        Assert.Equal("c", TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Open).ItemId);
        Assert.Null(TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Closed));
    }
}
