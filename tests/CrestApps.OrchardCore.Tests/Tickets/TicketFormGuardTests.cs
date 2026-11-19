using CrestApps.OrchardCore.Tickets.WebForm;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket web form keeps robots out: a filled trap field gives a post away, and each visitor address may send a few
/// requests per window, counted again once the window has passed; other visitors aren't held back.
/// </summary>
public sealed class TicketFormGuardTests
{
    [Fact]
    public void AFilledTrapField_GivesAPostAway()
    {
        Assert.True(TicketFormGuard.IsTrapped("http://spam.example"));
        Assert.False(TicketFormGuard.IsTrapped(null));
        Assert.False(TicketFormGuard.IsTrapped("  "));
    }

    [Fact]
    public void AVisitor_MaySendAFewRequestsPerWindow_AndAgainOnceItHasPassed()
    {
        // Arrange
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => now);
        var guard = new TicketFormGuard(clock.Object);

        // Act
        var taken = Enumerable.Range(0, TicketFormGuard.SubmissionsPerWindow).Select(_ => guard.TryTake("10.0.0.1")).ToList();
        var overLimit = guard.TryTake("10.0.0.1");
        var otherVisitor = guard.TryTake("10.0.0.2");
        now = now.Add(TicketFormGuard.Window);
        var afterWindow = guard.TryTake("10.0.0.1");

        // Assert
        Assert.All(taken, Assert.True);
        Assert.False(overLimit);
        Assert.True(otherVisitor);
        Assert.True(afterWindow);
    }
}
