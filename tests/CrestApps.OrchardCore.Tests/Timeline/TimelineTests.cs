using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Notifications;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The timeline of a record: entries are checked and stamped, a record's timeline comes back newest first with the
/// internal entries only for those who may read them, a body can be redacted but the entry stays, access comes from
/// the record kind's provider (and is refused without one), and mentions are found without mistaking e-mail addresses.
/// </summary>
public sealed class TimelineTests
{
    private static readonly RecordRef _order = new("Order", "o1");
    private static readonly PartyRef _agent = new(PartyKinds.User, "agent-1");

    [Fact]
    public async Task AnEntry_NeedsARecordAKindAndSomethingToSay()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().AddAsync(new TimelineEntry
        {
            Kind = " ",
            Body = "   ",
            Changes = [new TimelineFieldChange { Field = "Status", From = "Open", To = "Open" }],
        }), commit: false);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(3, result.Errors.Count);
        Assert.Empty(result.Entry.Changes);
    }

    [Fact]
    public async Task ATimeline_ComesBackNewestFirst_WithInternalEntriesOnlyWhenAsked()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var now = CalendarTestHarness.Now;

        await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();

            await timeline.AddAsync(Entry(TimelineConstants.Kinds.Comment, TimelineVisibility.Public, "Thanks for your order.", now.AddMinutes(-30)));
            await timeline.AddAsync(Entry(TimelineConstants.Kinds.Note, TimelineVisibility.Internal, "Ask @maria about the refund.", now.AddMinutes(-20)));
            await timeline.AddAsync(Entry(TimelineConstants.Kinds.Event, TimelineVisibility.Public, null, now.AddMinutes(-10), new TimelineFieldChange { Field = "Status", From = "New", To = "Paid" }));
            await timeline.AddAsync(new TimelineEntry { Subject = new RecordRef("Order", "other"), Kind = TimelineConstants.Kinds.Note, Body = "Another order." });
        });

        // Act
        var (everything, publicOnly, notesOnly) = await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();

            return (
                await timeline.ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true }),
                await timeline.ListAsync(new TimelineQuery { Subject = _order }),
                await timeline.ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true, Kinds = [TimelineConstants.Kinds.Note] }));
        }, commit: false);

        // Assert
        Assert.Equal(3, everything.Total);
        Assert.Equal([TimelineConstants.Kinds.Event, TimelineConstants.Kinds.Note, TimelineConstants.Kinds.Comment], everything.Entries.Select(entry => entry.Kind));
        Assert.Equal(2, publicOnly.Total);
        Assert.DoesNotContain(publicOnly.Entries, entry => entry.Visibility == TimelineVisibility.Internal);
        Assert.Equal("Ask @maria about the refund.", Assert.Single(notesOnly.Entries).Body);
        Assert.Equal(_agent, everything.Entries[0].Author);
        Assert.Equal("Paid", Assert.Single(everything.Entries[0].Changes).To);
    }

    [Fact]
    public async Task Redacting_RemovesTheText_AndKeepsTheEntry()
    {
        // Arrange
        await using var harness = await CreateAsync();

        var id = await harness.RunAsync(async services => (await services.GetRequiredService<ITimelineService>()
            .AddAsync(Entry(TimelineConstants.Kinds.Comment, TimelineVisibility.Public, "My card is 4111...", CalendarTestHarness.Now))).Entry.ItemId);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().RedactAsync(id, new PartyRef(PartyKinds.User, "admin")));
        var entry = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().FindAsync(id), commit: false);

        // Assert
        Assert.Null(entry.Body);
        Assert.NotNull(entry.RedactedUtc);
        Assert.Equal("admin", entry.RedactedBy.Id);
    }

    [Fact]
    public async Task Access_ComesFromTheRecordKindsProvider_AndIsRefusedWithoutOne()
    {
        // Arrange
        var service = new DefaultTimelineAccessService([new OrdersOnlyForAgents()]);
        var agent = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Agent")], "test"));
        var customer = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Customer")], "test"));

        // Act and assert
        Assert.True(await service.CanViewAsync(agent, _order, TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.True(await service.CanViewAsync(customer, _order, TimelineVisibility.Public, TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(customer, _order, TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.False(await service.CanAddAsync(customer, _order, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(agent, new RecordRef("Invoice", "i1"), TimelineVisibility.Public, TestContext.Current.CancellationToken));
        Assert.False(await service.CanAddAsync(agent, _order, string.Empty, TimelineVisibility.Public, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Ask @maria and @Bob.Smith, then @maria again.", new[] { "maria", "Bob.Smith" })]
    [InlineData("Write to pat@example.com, not @pat.", new[] { "pat" })]
    [InlineData("No mentions here.", new string[0])]
    [InlineData("@start of the line", new[] { "start" })]
    public void Mentions_AreFoundOnce_AndNeverInAnEmailAddress(string text, string[] expected)
    {
        Assert.Equal(expected, TimelineMentionHandler.FindMentions(text));
    }

    private static TimelineEntry Entry(string kind, TimelineVisibility visibility, string body, DateTime createdUtc, params TimelineFieldChange[] changes)
        => new()
        {
            Subject = _order,
            Kind = kind,
            Visibility = visibility,
            Author = _agent,
            Body = body,
            CreatedUtc = createdUtc,
            Changes = [.. changes],
        };

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddTimelineCore(),
            TimelineIndexSchema.CreateAsync,
            [new TimelineEntryIndexProvider()],
            [TimelineConstants.CollectionName]);

    private sealed class OrdersOnlyForAgents : ITimelineAccessProvider
    {
        public string SubjectKind => "Order";

        public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(visibility == TimelineVisibility.Public || user.IsInRole("Agent"));

        public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(user.IsInRole("Agent"));
    }
}
