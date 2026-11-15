using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Problems and incidents on a real store: a ticket linked to another as its problem is indexed so the problem finds it,
/// and solving the problem solves its open incidents (not one already solved, not one that is only related).
/// </summary>
public sealed class TicketProblemTests
{
    [Fact]
    public async Task SolvingAProblem_SolvesItsOpenIncidents()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var problem = (await kit.CreateAsync("Mail server down")).Ticket.ContentItemId;
        var incident = (await kit.CreateAsync("Can't send mail")).Ticket.ContentItemId;
        var related = (await kit.CreateAsync("Calendar slow")).Ticket.ContentItemId;
        await LinkAsync(kit, incident, problem, TicketLinkKinds.Problem);
        await LinkAsync(kit, related, problem, TicketLinkKinds.Related);

        // The problem observer runs as a real observer would, after the problem's change is committed.
        kit.Observers.React = (services, name, ticket) => name == "StatusChanged:" + TicketKit.Solved.ItemId && ticket.ContentItemId == problem
            ? new TicketProblemObserver(services, NullLogger<TicketProblemObserver>.Instance, RecipeRoundTrip.Localizer<TicketProblemObserver>())
                .StatusChangedAsync(ticket, TicketKit.Open, TicketKit.Solved, TestContext.Current.CancellationToken)
            : Task.CompletedTask;

        // Act
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(problem, new TicketUpdate { StatusId = TicketKit.Solved.ItemId }, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        var links = await kit.ReadAsync(async services => (await services.GetRequiredService<ISession>().QueryIndex<TicketLinkIndex>(index => index.LinkedTicketId == problem).ListAsync(TestContext.Current.CancellationToken)).ToList());
        Assert.Contains(links, link => link.TicketId == incident && link.Kind == TicketLinkKinds.Problem);
        Assert.Equal(TicketStatusCategory.Solved, (await kit.GetAsync(incident)).GetOrCreate<TicketPart>().StatusCategory);
        Assert.NotEqual(TicketStatusCategory.Solved, (await kit.GetAsync(related)).GetOrCreate<TicketPart>().StatusCategory);
    }

    private static Task<TicketResult> LinkAsync(TicketKit kit, string ticketId, string otherId, string kind)
        => kit.RunAsync(services => services.GetRequiredService<ITicketService>().LinkAsync(ticketId, new RecordRef(TicketsConstants.RecordKind, otherId), kind, cancellationToken: TestContext.Current.CancellationToken));
}
