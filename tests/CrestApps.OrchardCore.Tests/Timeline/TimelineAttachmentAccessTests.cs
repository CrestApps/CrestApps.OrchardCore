using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The files of a timeline entry are as visible as the entry: an internal note's files only for those who may read
/// internal entries, a public comment's for everyone who may read the record, nothing once the entry is redacted, and
/// changes only for those who may add such an entry.
/// </summary>
public sealed class TimelineAttachmentAccessTests
{
    private static readonly RecordRef _order = new("Order", "o1");

    [Fact]
    public async Task AnEntrysFiles_AreAsVisibleAsTheEntry()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (note, comment) = await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();

            var added = await timeline.AddAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Visibility = TimelineVisibility.Internal, Body = "Photo of the damage." });
            var shared = await timeline.AddAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Comment, Visibility = TimelineVisibility.Public, Body = "Your invoice." });

            return (added.Entry.ItemId, shared.Entry.ItemId);
        });

        var agent = CalendarTestHarness.User("agent-1", "role:Agent");
        var customer = CalendarTestHarness.User("customer-1");

        // Act
        var results = await harness.RunAsync(async services =>
        {
            var access = new CrestApps.OrchardCore.Timeline.Services.TimelineEntryAttachmentAccess(services.GetRequiredService<ITimelineService>(), services.GetRequiredService<ITimelineAccessService>());

            return new
            {
                AgentSeesNote = await access.CanViewAsync(agent, Owner(note)),
                CustomerSeesNote = await access.CanViewAsync(customer, Owner(note)),
                CustomerSeesComment = await access.CanViewAsync(customer, Owner(comment)),
                CustomerChangesComment = await access.CanManageAsync(customer, Owner(comment)),
                AgentChangesNote = await access.CanManageAsync(agent, Owner(note)),
                UnknownEntry = await access.CanViewAsync(agent, Owner("missing")),
                Kind = access.OwnerKind,
            };
        }, commit: false);

        // Assert
        Assert.True(results.AgentSeesNote);
        Assert.False(results.CustomerSeesNote);
        Assert.True(results.CustomerSeesComment);
        Assert.False(results.CustomerChangesComment);
        Assert.True(results.AgentChangesNote);
        Assert.False(results.UnknownEntry);
        Assert.Equal(TimelineConstants.AttachmentOwnerKind, results.Kind);
    }

    [Fact]
    public async Task ARedactedEntrysFiles_AreNotServed()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var id = await harness.RunAsync(async services =>
            (await services.GetRequiredService<ITimelineService>().AddAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Visibility = TimelineVisibility.Internal, Body = "Card number." })).Entry.ItemId);

        await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().RedactAsync(id, default));

        // Act
        var sees = await harness.RunAsync(
            services => new CrestApps.OrchardCore.Timeline.Services.TimelineEntryAttachmentAccess(services.GetRequiredService<ITimelineService>(), services.GetRequiredService<ITimelineAccessService>())
                .CanViewAsync(CalendarTestHarness.User("agent-1", "role:Agent"), Owner(id)),
            commit: false);

        // Assert
        Assert.False(sees);
    }

    private static RecordRef Owner(string entryId)
        => new(TimelineConstants.AttachmentOwnerKind, entryId);

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTimelineCore();
                services.AddTimelineAccessProvider<OrdersForAgents>();
            },
            TimelineIndexSchema.CreateAsync,
            [new TimelineEntryIndexProvider()],
            [TimelineConstants.CollectionName]);

    private sealed class OrdersForAgents : ITimelineAccessProvider
    {
        public string SubjectKind => "Order";

        public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(visibility == TimelineVisibility.Public || user.IsInRole("Agent"));

        public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(user.IsInRole("Agent"));
    }
}
