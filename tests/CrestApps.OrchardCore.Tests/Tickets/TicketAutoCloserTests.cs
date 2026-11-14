using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Solved tickets close once their desk's delay has passed: the query asks each desk with a delay for tickets solved
/// before now minus that delay, each found ticket moves to the first Closed status, and a desk with no delay is skipped.
/// </summary>
public sealed class TicketAutoCloserTests
{
    private static readonly DateTime _now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ADeskWithADelay_ClosesTheTicketsSolvedBeforeIt_AndADeskWithoutOneIsSkipped()
    {
        // Arrange
        var queries = new List<TicketScopedQuery>();
        var updates = new List<(string Id, TicketUpdate Update)>();
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.ListAsync(It.IsAny<TicketScopedQuery>(), It.IsAny<CancellationToken>()))
            .Callback((TicketScopedQuery query, CancellationToken _) => queries.Add(query))
            .ReturnsAsync(([new ContentItem { ContentItemId = "t1" }, new ContentItem { ContentItemId = "t2" }], 2));
        tickets.Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .Callback((string id, TicketUpdate update, TicketActor _, CancellationToken _) => updates.Add((id, update)))
            .ReturnsAsync((string id, TicketUpdate _, TicketActor _, CancellationToken _) => id == "t2" ? new TicketResult { Errors = ["Busy"] } : new TicketResult());

        var closer = new TicketAutoCloser(
            tickets.Object,
            Catalog<TicketDesk>(new TicketDesk { ItemId = "support", AutoCloseDays = 4 }, new TicketDesk { ItemId = "hr", AutoCloseDays = 0 }),
            Catalog<TicketStatus>(
                new TicketStatus { ItemId = "solved", Category = TicketStatusCategory.Solved, Order = 50 },
                new TicketStatus { ItemId = "closed", Category = TicketStatusCategory.Closed, Order = 60 }),
            Clock(),
            NullLogger<TicketAutoCloser>.Instance,
            RecipeRoundTrip.Localizer<TicketAutoCloser>());

        // Act
        var closed = await closer.CloseDueAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var query = Assert.Single(queries);
        Assert.Equal(["support"], query.Query.DeskIds);
        Assert.Equal([TicketStatusCategory.Solved], query.Query.Categories);
        Assert.Equal(_now.AddDays(-4), query.Query.SolvedBeforeUtc);
        Assert.Equal(["support"], query.AllDeskIds);
        Assert.All(updates, update => Assert.Equal("closed", update.Update.StatusId));
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task WithoutAClosedStatus_NothingIsClosed()
    {
        // Arrange
        var tickets = new Mock<ITicketService>(MockBehavior.Strict);
        var closer = new TicketAutoCloser(
            tickets.Object,
            Catalog<TicketDesk>(new TicketDesk { ItemId = "support", AutoCloseDays = 4 }),
            Catalog<TicketStatus>(new TicketStatus { ItemId = "solved", Category = TicketStatusCategory.Solved }),
            Clock(),
            NullLogger<TicketAutoCloser>.Instance,
            RecipeRoundTrip.Localizer<TicketAutoCloser>());

        // Act
        var closed = await closer.CloseDueAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, closed);
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static ICatalog<T> Catalog<T>(params T[] items)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var catalog = new Mock<ICatalog<T>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([.. items]);

        return catalog.Object;
    }
}
