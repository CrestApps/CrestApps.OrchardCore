using CrestApps.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets.Integrations;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A staff member who leaves, or whose user is unlinked, hands back the tickets still being worked on: each is unassigned.
/// A new member, or one whose user stays the same, hands back nothing.
/// </summary>
public sealed class TicketStaffLifecycleObserverTests
{
    [Fact]
    public async Task ATerminatedMember_HandsBackTheirOpenTickets()
    {
        // Arrange
        var (observer, tickets, queries) = Create(["t1", "t2"]);

        // Act
        await observer.ChangedAsync(new StaffChangedContext
        {
            StaffMember = new StaffMember { UserId = "u1" },
            PreviousUserId = "u1",
            Terminated = true,
        }, TestContext.Current.CancellationToken);

        // Assert
        var query = Assert.Single(queries).Query;
        Assert.Equal("u1", query.Assignee);
        Assert.DoesNotContain(TicketStatusCategory.Solved, query.Categories);
        Assert.DoesNotContain(TicketStatusCategory.Closed, query.Categories);
        tickets.Verify(service => service.UpdateAsync("t1", It.Is<TicketUpdate>(update => update.AssigneeId == string.Empty), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
        tickets.Verify(service => service.UpdateAsync("t2", It.Is<TicketUpdate>(update => update.AssigneeId == string.Empty), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUnlinkedUser_HandsBackTheTicketsOfThePreviousUser()
    {
        // Arrange
        var (observer, tickets, queries) = Create(["t1"]);

        // Act
        await observer.ChangedAsync(new StaffChangedContext
        {
            StaffMember = new StaffMember { UserId = "u2" },
            PreviousUserId = "u1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("u1", Assert.Single(queries).Query.Assignee);
        tickets.Verify(service => service.UpdateAsync("t1", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ANewMemberOrAnUnchangedUser_HandsBackNothing()
    {
        // Arrange
        var (observer, tickets, queries) = Create(["t1"]);

        // Act
        await observer.ChangedAsync(new StaffChangedContext { StaffMember = new StaffMember { UserId = "u1" }, IsNew = true }, TestContext.Current.CancellationToken);
        await observer.ChangedAsync(new StaffChangedContext { StaffMember = new StaffMember { UserId = "u1" }, PreviousUserId = "u1", TeamsChanged = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(queries);
        tickets.Verify(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (TicketStaffLifecycleObserver Observer, Mock<ITicketService> Tickets, List<TicketScopedQuery> Queries) Create(string[] ticketIds)
    {
        var queries = new List<TicketScopedQuery>();
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.ListAsync(It.IsAny<TicketScopedQuery>(), It.IsAny<CancellationToken>()))
            .Callback((TicketScopedQuery query, CancellationToken _) => queries.Add(query))
            .ReturnsAsync(([.. ticketIds.Select(id => new ContentItem { ContentItemId = id })], ticketIds.Length));
        tickets.Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult());

        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketDesk { ItemId = "support" }]);

        return (new TicketStaffLifecycleObserver(tickets.Object, desks.Object, NullLogger<TicketStaffLifecycleObserver>.Instance, RecipeRoundTrip.Localizer<TicketStaffLifecycleObserver>()), tickets, queries);
    }
}
