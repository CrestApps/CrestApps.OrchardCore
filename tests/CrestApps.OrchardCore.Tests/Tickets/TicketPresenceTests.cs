using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Agents see who else has a ticket open and who is writing on it; someone who stopped checking in, or closed the page,
/// is no longer shown; and nobody sees themselves.
/// </summary>
public sealed class TicketPresenceTests
{
    [Fact]
    public async Task AgentsSeeEachOther_UntilOneLeavesOrStopsCheckingIn()
    {
        // Arrange
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => now);
        var presence = new TicketPresence(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), clock.Object);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var anaFirst = await presence.CheckInAsync("t1", "ana", "Ana", writing: false, ct);
        var benSees = await presence.CheckInAsync("t1", "ben", "Ben", writing: true, ct);
        var anaSees = await presence.CheckInAsync("t1", "ana", "Ana", writing: false, ct);
        now = now.Add(TicketPresence.Timeout).AddSeconds(1);
        var afterBenStopped = await presence.CheckInAsync("t1", "ana", "Ana", writing: false, ct);
        await presence.CheckInAsync("t1", "cy", "Cy", writing: false, ct);
        await presence.LeaveAsync("t1", "cy", ct);
        var afterCyLeft = await presence.CheckInAsync("t1", "ana", "Ana", writing: false, ct);
        var otherTicket = await presence.CheckInAsync("t2", "ben", "Ben", writing: false, ct);

        // Assert
        Assert.Empty(anaFirst);
        Assert.Equal(["Ana"], benSees.Select(viewer => viewer.Name));
        var ben = Assert.Single(anaSees);
        Assert.True(ben.Writing);
        Assert.Empty(afterBenStopped);
        Assert.Empty(afterCyLeft);
        Assert.Empty(otherTicket);
    }
}
