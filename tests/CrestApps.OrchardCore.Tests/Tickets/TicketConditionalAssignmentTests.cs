using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// An assignment that must not take a ticket from someone (work accepted from a queue) is checked under the ticket's
/// lock, and nobody is given a ticket of a desk they can't work.
/// </summary>
public sealed class TicketConditionalAssignmentTests
{
    [Fact]
    public async Task AnOnlyIfUnassignedUpdate_IsRefused_WhenSomeoneElseHasTheTicket()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        await UpdateAsync(kit, ticketId, new TicketUpdate { AssigneeId = "ana" });

        // Act
        var taken = await UpdateAsync(kit, ticketId, new TicketUpdate { AssigneeId = "ben", OnlyIfUnassigned = true });
        var again = await UpdateAsync(kit, ticketId, new TicketUpdate { AssigneeId = "ana", OnlyIfUnassigned = true });

        // Assert
        Assert.False(taken.Succeeded);
        Assert.True(again.Succeeded);
        Assert.Equal("ana", (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AssigneeId);
    }

    [Fact]
    public async Task AnOnlyIfUnassignedUpdate_GivesAnUnassignedTicket()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var result = await UpdateAsync(kit, ticketId, new TicketUpdate { AssigneeId = "ben", OnlyIfUnassigned = true });

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal("ben", (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AssigneeId);
    }

    [Fact]
    public async Task NobodyIsGivenATicket_OfADeskTheyCantWork()
    {
        // Arrange: a queue's members aren't necessarily the desk's people.
        var access = new Mock<ITicketAccessService>();
        access.Setup(value => value.CanBeAssignedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string _, CancellationToken _) => userId != "outsider");
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services.AddSingleton(access.Object));
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var refused = await UpdateAsync(kit, ticketId, new TicketUpdate { AssigneeId = "outsider", OnlyIfUnassigned = true });

        // Assert
        Assert.False(refused.Succeeded);
        Assert.Null((await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AssigneeId);
    }

    private static Task<TicketResult> UpdateAsync(TicketKit kit, string ticketId, TicketUpdate update)
        => kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, update, cancellationToken: TestContext.Current.CancellationToken));
}
