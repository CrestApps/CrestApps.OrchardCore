using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Integrations;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Webhooks;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Ticket webhooks: a public reply is published and an internal note never leaves the site; moving into Solved or Closed
/// publishes once, a move within a category doesn't; the payload is the ticket as the API gives it.
/// </summary>
public sealed class TicketWebhookTests
{
    [Fact]
    public async Task PublicRepliesAndMovesIntoSolvedArePublished_NotesAndMovesWithinACategoryArent()
    {
        // Arrange
        var published = new List<(string Name, object Payload)>();
        var publisher = new Mock<IWebhookPublisher>();
        publisher.Setup(value => value.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, object payload, string _, CancellationToken _) => published.Add((name, payload)))
            .Returns(Task.CompletedTask);
        var observer = new TicketWebhookObserver(publisher.Object);
        var ticket = new ContentItem { ContentItemId = "t1", DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-1", DeskId = "support", StatusCategory = TicketStatusCategory.Solved });
        var open = new TicketStatus { ItemId = "open", Category = TicketStatusCategory.Open };
        var solved = new TicketStatus { ItemId = "solved", Category = TicketStatusCategory.Solved };
        var resolved = new TicketStatus { ItemId = "resolved", Category = TicketStatusCategory.Solved };

        // Act
        await observer.RepliedAsync(ticket, new TimelineEntry { ItemId = "e1", Visibility = TimelineVisibility.Public, Body = "Try this" }, fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, new TimelineEntry { ItemId = "e2", Visibility = TimelineVisibility.Internal, Body = "Secret" }, fromRequester: false, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, open, solved, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, solved, resolved, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([TicketWebhookEvents.ReplyAdded, TicketWebhookEvents.Solved], published.Select(item => item.Name));
        var model = (TicketApiModel)published[1].Payload.GetType().GetProperty("ticket")!.GetValue(published[1].Payload);
        Assert.Equal("T-1", model.Number);
        Assert.Equal("Solved", model.StatusCategory);
    }
}
