using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The time-elapsed scan on a real store: it reads the tickets whose moment has passed (so a desk with many newer
/// tickets still reaches its older due ones), starts each once per ticket and moment however long ago that moment was
/// (a ticket opened 181 days ago is not started again every run), and forgets a marker once its ticket is solved.
/// </summary>
public sealed class TicketTimeElapsedScannerTests
{
    [Fact]
    public async Task ADueTicket_StartsOnce_EvenWhenItsMomentIsOlderThanHalfAYear_AndNewerTicketsDontHideIt()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var now = CalendarTestHarness.Now;
        await SaveAsync(kit, Ticket("old", now.AddDays(-181), TicketStatusCategory.Open));
        await SaveAsync(kit, [.. Enumerable.Range(1, 5).Select(number => Ticket($"new{number}", now.AddHours(-1), TicketStatusCategory.Open))]);
        var started = new List<string>();

        // Act
        var first = await ScanAsync(kit, started);
        var second = await ScanAsync(kit, started);
        var third = await ScanAsync(kit, started);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);
        Assert.Equal(["old"], started);
    }

    [Fact]
    public async Task AMarker_IsForgottenOnceItsTicketIsSolved()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var now = CalendarTestHarness.Now;
        await SaveAsync(kit, Ticket("t1", now.AddDays(-2), TicketStatusCategory.Open));
        await ScanAsync(kit, []);

        // Act
        await SaveAsync(kit, Ticket("t1", now.AddDays(-2), TicketStatusCategory.Solved));
        await ScanAsync(kit, []);

        // Assert
        var marks = await kit.ReadAsync(async services => await services.GetRequiredService<ISession>().QueryIndex<TicketTimeElapsedMarkIndex>(TicketsConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, marks);
    }

    private static Task<int> ScanAsync(TicketKit kit, List<string> started)
        => kit.RunAsync(services =>
        {
            var activity = new ActivityRecord
            {
                ActivityId = "a1",
                Name = TicketsConstants.Events.TimeElapsed,
                IsStart = true,
                Properties = new JsonObject { [nameof(TicketTimeElapsedEvent.Hours)] = 24, [nameof(TicketTimeElapsedEvent.Since)] = TicketTimeElapsedSince.Created },
            };
            var types = new Mock<IWorkflowTypeStore>();
            types.Setup(store => store.GetByStartActivityAsync(TicketsConstants.Events.TimeElapsed))
                .ReturnsAsync([new WorkflowType { WorkflowTypeId = "w1", IsEnabled = true, Activities = [activity] }]);
            var workflows = new Mock<IWorkflowManager>();
            workflows.Setup(manager => manager.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Callback((string _, IDictionary<string, object> _, string correlationId, bool _, bool _) => started.Add(correlationId))
                .ReturnsAsync([]);

            return new TicketTimeElapsedScanner(
                types.Object,
                workflows.Object,
                services.GetRequiredService<ITicketService>(),
                services.GetRequiredService<ICatalog<TicketDesk>>(),
                services.GetRequiredService<IDocumentManager<TicketTimeElapsedDocument>>(),
                services.GetRequiredService<ISession>(),
                services,
                services.GetRequiredService<IClock>()).ScanAsync(TestContext.Current.CancellationToken);
        });

    private static Task<bool> SaveAsync(TicketKit kit, params ContentItem[] tickets)
        => kit.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var ticket in tickets)
            {
                if (await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == ticket.ContentItemId).FirstOrDefaultAsync() is { } existing)
                {
                    existing.Apply(ticket.Get<TicketPart>(nameof(TicketPart)));
                    await session.SaveAsync(existing);
                }
                else
                {
                    await session.SaveAsync(ticket);
                }
            }

            return true;
        });

    private static ContentItem Ticket(string id, DateTime createdUtc, TicketStatusCategory category)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, Latest = true, Published = true, DisplayText = id, CreatedUtc = createdUtc, ModifiedUtc = createdUtc };
        item.Apply(new TicketPart { Number = id, DeskId = "support", StatusId = "open", StatusCategory = category });

        return item;
    }
}
