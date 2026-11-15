using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Scrubbing a ticket's timeline on the real store for an erasure: shown text is rewritten, including a delivery notice
/// kept in an entry's properties, while a change's stable key and ids stay, so workflows and reports keep working.
/// </summary>
public sealed class TicketTimelineScrubTests
{
    [Fact]
    public async Task Scrub_RewritesShownTextAndNotices_AndKeepsChangeKeysAndIds()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var subject = new RecordRef(TicketsConstants.RecordKind, ticketId);
        var entryId = await kit.RunAsync(async services =>
        {
            var added = await services.GetRequiredService<ITimelineService>().AddAsync(new TimelineEntry
            {
                Subject = subject,
                Kind = TicketsConstants.EntryKinds.Event,
                Body = "Moved for pat@example.com",
                Changes = [new TimelineFieldChange { Key = TicketChangeKeys.Assignee, Field = "Assignee", From = "Pat Doe", To = "Ana", FromValue = "u-pat", ToValue = "u-ana" }],
                Properties = { [TimelineConstants.Properties.Notice] = "Not delivered: 550 pat@example.com unknown" },
            }, TestContext.Current.CancellationToken);

            return added.Entry.ItemId;
        });

        // Act
        var changed = await kit.RunAsync(services => services.GetRequiredService<ITimelineService>().ScrubAsync(subject, text => text?.Replace("pat@example.com", "[removed]", StringComparison.Ordinal).Replace("Pat Doe", "[removed]", StringComparison.Ordinal), TestContext.Current.CancellationToken));

        // Assert
        var entry = (await kit.EntriesAsync(ticketId)).Single(value => value.ItemId == entryId);
        Assert.True(changed >= 1);
        Assert.Equal("Moved for [removed]", entry.Body);
        Assert.Equal("Not delivered: 550 [removed] unknown", entry.Properties[TimelineConstants.Properties.Notice].ToString());
        var change = Assert.Single(entry.Changes);
        Assert.Equal(TicketChangeKeys.Assignee, change.Key);
        Assert.Equal("[removed]", change.From);
        Assert.Equal("u-pat", change.FromValue);
        Assert.Equal("u-ana", change.ToValue);
    }
}
