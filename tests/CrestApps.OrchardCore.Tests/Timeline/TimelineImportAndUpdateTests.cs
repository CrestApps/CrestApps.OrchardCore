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
/// The paths of the timeline other than adding: importing history checks and stamps an entry but announces nothing and
/// never cuts a long body, and importing the same entry twice stores it once; an entry over the indexed lengths is
/// refused; a change that doesn't rewrite an entry is saved and found again by its external id; deleting a record's
/// timeline tells the handlers of each entry; and entries of the same time page in a stable order.
/// </summary>
public sealed class TimelineImportAndUpdateTests
{
    private static readonly RecordRef _order = new("Order", "o1");

    private readonly List<string> _heard = [];

    [Fact]
    public async Task Importing_AnnouncesNothing_KeepsALongBody_AndStoresAnEntryOnce()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var longBody = new string('x', TimelineConstants.MaxBodyLength + 5_000);
        var id = TimelineEntryIds.FromSource("Order:o1", "note", "n1");

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ImportAsync(Note(id, longBody)));
        var second = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ImportAsync(Note(id, "Changed since.")));
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true }), commit: false);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(longBody, Assert.Single(page.Entries).Body);
        Assert.Empty(_heard);
    }

    [Fact]
    public async Task AnEntryOverTheIndexedLengths_IsRefused_ThroughEveryPath()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var tooLong = new string('k', TimelineConstants.MaxIndexedLength + 1);

        // Act
        var (added, imported) = await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();

            return (
                await timeline.AddAsync(new TimelineEntry { Subject = new RecordRef("Order", tooLong), Kind = TimelineConstants.Kinds.Note, Body = "Hi" }),
                await timeline.ImportAsync(new TimelineEntry { Subject = _order, Kind = tooLong, Body = "Hi" }));
        }, commit: false);

        // Assert
        Assert.False(added.Succeeded);
        Assert.False(imported.Succeeded);
    }

    [Fact]
    public async Task AChangeThatDoesntRewriteTheEntry_IsSaved_AndFoundByItsExternalId()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var id = await harness.RunAsync(async services => (await services.GetRequiredService<ITimelineService>().AddAsync(new TimelineEntry
        {
            Subject = _order,
            Kind = TimelineConstants.Kinds.Comment,
            Visibility = TimelineVisibility.Public,
            Body = "Your order shipped.",
            Channel = "Email",
        })).Entry.ItemId);

        // Act
        var updated = await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();
            var entry = await timeline.FindAsync(id);
            entry.ExternalId = "<msg-1@example.com>";

            return await timeline.UpdateAsync(entry);
        });
        var found = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().FindByExternalIdAsync("<msg-1@example.com>"), commit: false);

        // Assert
        Assert.True(updated.Succeeded);
        Assert.Equal(id, found?.ItemId);
        Assert.Contains("updated " + id, _heard);
    }

    [Fact]
    public async Task DeletingARecordsTimeline_TellsTheHandlersOfEachEntry()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();
            await timeline.AddAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Body = "One" });
            await timeline.AddAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Body = "Two" });
        });
        _heard.Clear();

        // Act
        var deleted = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().DeleteAllAsync(_order));
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true }), commit: false);

        // Assert
        Assert.Equal(2, deleted);
        Assert.Equal(0, page.Total);
        Assert.Equal(2, _heard.Count(line => line.StartsWith("deleted ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task EntriesOfTheSameTime_PageInAStableOrder()
    {
        // Arrange: five entries with one time, read two at a time.
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();

            for (var index = 0; index < 5; index++)
            {
                await timeline.ImportAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Body = $"Note {index}", CreatedUtc = CalendarTestHarness.Now });
            }
        });

        // Act
        var ids = new List<string>();

        for (var skip = 0; skip < 5; skip += 2)
        {
            var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true, Skip = skip, Take = 2 }), commit: false);
            ids.AddRange(page.Entries.Select(entry => entry.ItemId));
        }

        // Assert
        Assert.Equal(5, ids.Distinct().Count());
    }

    private static TimelineEntry Note(string id, string body)
        => new()
        {
            ItemId = id,
            Subject = _order,
            Kind = TimelineConstants.Kinds.Note,
            Visibility = TimelineVisibility.Internal,
            Body = body,
            CreatedUtc = CalendarTestHarness.Now.AddDays(-30),
        };

    private Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTimelineCore();
                services.AddSingleton<ITimelineEntryHandler>(new RecordingHandler(_heard));
            },
            TimelineIndexSchema.CreateAsync,
            [new TimelineEntryIndexProvider()],
            [TimelineConstants.CollectionName]);

    private sealed class RecordingHandler : ITimelineEntryHandler
    {
        private readonly List<string> _heard;

        public RecordingHandler(List<string> heard)
        {
            _heard = heard;
        }

        public Task AddingAsync(TimelineEntry entry, IList<string> errors, CancellationToken cancellationToken = default)
        {
            _heard.Add("adding " + entry.ItemId);

            return Task.CompletedTask;
        }

        public Task AddedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        {
            _heard.Add("added " + entry.ItemId);

            return Task.CompletedTask;
        }

        public Task UpdatedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        {
            _heard.Add("updated " + entry.ItemId);

            return Task.CompletedTask;
        }

        public Task DeletedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        {
            _heard.Add("deleted " + entry.ItemId);

            return Task.CompletedTask;
        }
    }
}
