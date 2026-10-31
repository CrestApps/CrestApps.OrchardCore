using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// External calendar synchronization on a real SQLite store with a fake provider: reads, incremental reads, writing back,
/// conflicts, and failures that must not break the calendar.
/// </summary>
public sealed class CalendarSyncTests
{
    [Fact]
    public async Task OneWay_ImportsEventsReadOnly_AndASecondRunChangesNothing()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: false);

        // Act
        var first = await SyncAsync(harness, calendar.ItemId);
        var second = await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created + second.Updated + second.Removed);

        var imported = Assert.Single(await ListEventsAsync(harness, calendar.ItemId));
        var mapping = Assert.Single(imported.ProviderMappings);

        Assert.False(imported.Editable);
        Assert.Equal("a", mapping.ExternalEventId);
        Assert.Equal(calendar.ItemId, mapping.ConnectionId);
        Assert.Empty(remote.Pushes);
    }

    [Fact]
    public async Task ProviderFailure_KeepsTheEvents_RecordsTheError_AndOtherCalendarsStillSync()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var failing = await SubscribeAsync(harness, twoWay: false, url: "https://example.com/failing.ics");
        var neverSynced = await SubscribeAsync(harness, twoWay: false, url: "https://example.com/failing.ics");
        var healthy = await SubscribeAsync(harness, twoWay: false);

        await SyncAsync(harness, failing.ItemId);
        remote.FailUrl = "https://example.com/failing.ics";

        // Act
        var failed = await SyncAsync(harness, failing.ItemId);
        var synced = await harness.RunAsync(services => services.GetRequiredService<CalendarSyncService>().SyncDueAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(failed);
        Assert.Equal(2, synced);
        Assert.Single(await ListEventsAsync(harness, failing.ItemId));
        Assert.Empty(await ListEventsAsync(harness, neverSynced.ItemId));
        Assert.Single(await ListEventsAsync(harness, healthy.ItemId));

        var subscription = (await FindCalendarAsync(harness, failing.ItemId)).GetOrCreate<CalendarSubscription>();

        Assert.Equal(1, subscription.Failures);
        Assert.Equal("The feed is down.", subscription.LastError);
        Assert.Equal(1, (await FindCalendarAsync(harness, neverSynced.ItemId)).GetOrCreate<CalendarSubscription>().Failures);
        Assert.Equal(0, (await FindCalendarAsync(harness, healthy.ItemId)).GetOrCreate<CalendarSubscription>().Failures);
    }

    [Fact]
    public async Task RefusedAccess_PausesTheSubscription_UntilItIsSavedAgain()
    {
        // Arrange
        var remote = new FakeRemoteCalendar { RefuseAccess = true };
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: false);

        // Act
        var result = await SyncAsync(harness, calendar.ItemId);
        var due = await harness.RunAsync(services => services.GetRequiredService<CalendarSyncService>().ListDueAsync(TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Null(result);
        Assert.True((await FindCalendarAsync(harness, calendar.ItemId)).GetOrCreate<CalendarSubscription>().NeedsReconnect);
        Assert.DoesNotContain(calendar.ItemId, due);
    }

    [Fact]
    public async Task TwoWay_WritesALocalEventBack_AndMatchesItOnTheNextRead()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: true);
        var created = await harness.CreateEventAsync(calendar.ItemId, Hour(10), Hour(11), e => e.Title = "Local");

        Assert.True(created.Succeeded);

        // Act
        var first = await SyncAsync(harness, calendar.ItemId);
        var second = await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal(1, first.Created);
        Assert.Single(remote.Pushes);
        Assert.Null(remote.Pushes[0].Mapping);

        var local = Assert.Single(await ListEventsAsync(harness, calendar.ItemId));
        var mapping = Assert.Single(local.ProviderMappings);

        Assert.Equal(mapping.ExternalEventId, local.Uid);
        Assert.True(remote.Events.ContainsKey(mapping.ExternalEventId));
        Assert.True(local.Editable);
        Assert.Equal(0, second.Created + second.Updated + second.Conflicts);
        Assert.Single(remote.Pushes);
    }

    [Fact]
    public async Task TwoWay_LocalChange_IsWrittenBackWithTheLastVersionAsPrecondition()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: true);

        await SyncAsync(harness, calendar.ItemId);
        await ChangeLocallyAsync(harness, calendar.ItemId, "Planning (moved)");

        // Act
        var result = await SyncAsync(harness, calendar.ItemId);

        // Assert
        var push = Assert.Single(remote.Pushes);

        Assert.Equal("v1", push.Mapping.Version);
        Assert.Equal("Planning (moved)", remote.Events["a"].Data.Summary);
        Assert.Equal(0, result.Conflicts);
        Assert.Equal("v2", Assert.Single(Assert.Single(await ListEventsAsync(harness, calendar.ItemId)).ProviderMappings).Version);
    }

    [Fact]
    public async Task Conflict_ExternalWins_OverwritesTheLocalChange()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: true, policy: CalendarSyncConflictPolicy.ExternalWins);

        await SyncAsync(harness, calendar.ItemId);
        await ChangeLocallyAsync(harness, calendar.ItemId, "Local title");
        remote.Change("a", "External title");

        // Act
        var result = await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal(1, result.Conflicts);
        Assert.Equal("External title", Assert.Single(await ListEventsAsync(harness, calendar.ItemId)).Title);
        Assert.Empty(remote.Pushes);
    }

    [Fact]
    public async Task Conflict_LocalWins_KeepsTheLocalChange_AndOverwritesTheExternalCopy()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: true, policy: CalendarSyncConflictPolicy.LocalWins);

        await SyncAsync(harness, calendar.ItemId);
        await ChangeLocallyAsync(harness, calendar.ItemId, "Local title");
        remote.Change("a", "External title");

        // Act
        var result = await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal(1, result.Conflicts);
        Assert.Equal("Local title", Assert.Single(await ListEventsAsync(harness, calendar.ItemId)).Title);

        var push = Assert.Single(remote.Pushes);

        Assert.Null(push.Mapping.Version);
        Assert.Equal("Local title", remote.Events["a"].Data.Summary);
    }

    [Fact]
    public async Task Incremental_ReadsOnlyChanges_RemovesDeletedEvents_AndStartsOverWhenTheTokenExpires()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        remote.Add("b", "Review", 13);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: false);

        await SyncAsync(harness, calendar.ItemId);
        remote.Delete("b");
        remote.Add("c", "Retro", 15);

        // Act
        var incremental = await SyncAsync(harness, calendar.ItemId);

        remote.ExpireTokens = true;
        remote.Add("d", "Lunch", 12);

        var restarted = await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal(2, remote.FullReads);
        Assert.Equal(1, incremental.Created);
        Assert.Equal(1, incremental.Removed);
        Assert.Equal(1, restarted.Created);
        Assert.Equal(["Lunch", "Planning", "Retro"], (await ListEventsAsync(harness, calendar.ItemId)).Select(e => e.Title).Order());
        Assert.Equal(remote.CurrentToken, (await FindCalendarAsync(harness, calendar.ItemId)).GetOrCreate<CalendarSubscription>().SyncToken);
    }

    [Fact]
    public async Task TwoWay_LocalDelete_IsQueued_AndDeletedExternally()
    {
        // Arrange
        var remote = new FakeRemoteCalendar();

        remote.Add("a", "Planning", 9);
        await using var harness = await CreateHarnessAsync(remote);
        var calendar = await SubscribeAsync(harness, twoWay: true);

        await SyncAsync(harness, calendar.ItemId);

        var imported = Assert.Single(await ListEventsAsync(harness, calendar.ItemId));

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ICalendarEventManager>();
            var current = await manager.FindByIdAsync(imported.ItemId, TestContext.Current.CancellationToken);

            await manager.DeleteAsync(current, new CalendarChangeOptions { Actor = "user1" }, TestContext.Current.CancellationToken);
        });

        // The change queue runs observers after commit; the test calls the observers itself.
        await harness.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<ICalendarEventObserver>())
            {
                await observer.OnChangedAsync(new CalendarEventChange { Kind = CalendarEventChangeKind.Deleted, Event = imported, Actor = "user1" }, TestContext.Current.CancellationToken);
            }
        });

        var queued = (await FindCalendarAsync(harness, calendar.ItemId)).GetOrCreate<CalendarSubscription>();

        // Act
        await SyncAsync(harness, calendar.ItemId);

        // Assert
        Assert.True(queued.PendingPush);
        Assert.Equal("a", Assert.Single(queued.PendingDeletes).ExternalEventId);
        Assert.Equal(["a"], remote.Deletes);
        Assert.Empty(remote.Events);
        Assert.Empty((await FindCalendarAsync(harness, calendar.ItemId)).GetOrCreate<CalendarSubscription>().PendingDeletes);
    }

    private static DateTime Hour(int hour) => new(2026, 11, 10, hour, 0, 0);

    private static Task<CalendarTestHarness> CreateHarnessAsync(FakeRemoteCalendar remote)
    {
        return CalendarTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(remote);
            services.AddCalendarProvider<FakeCalendarProvider>();
        });
    }

    private static Task<Calendar> SubscribeAsync(CalendarTestHarness harness, bool twoWay, CalendarSyncConflictPolicy policy = CalendarSyncConflictPolicy.ExternalWins, string url = "https://example.com/calendar.ics")
    {
        return harness.CreateCalendarAsync(CalendarTestHarness.General, "Synced " + Guid.NewGuid().ToString("N"), calendar =>
        {
            calendar.TimeZoneId = "UTC";
            calendar.Put(new CalendarSubscription
            {
                Provider = FakeCalendarProvider.ProviderName,
                Url = url,
                TwoWay = twoWay,
                ConflictPolicy = policy,
            });
        });
    }

    private static Task<ICalendarImportResult> SyncAsync(CalendarTestHarness harness, string calendarId)
    {
        return harness.RunAsync(services => services.GetRequiredService<CalendarSyncService>().SyncAsync(calendarId, TestContext.Current.CancellationToken));
    }

    private static Task<Calendar> FindCalendarAsync(CalendarTestHarness harness, string calendarId)
    {
        return harness.RunAsync(async services => await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarId, TestContext.Current.CancellationToken), commit: false);
    }

    private static Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(CalendarTestHarness harness, string calendarId)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICalendarEventStore>().ListByCalendarAsync(calendarId, 0, 100, TestContext.Current.CancellationToken), commit: false);
    }

    private static Task ChangeLocallyAsync(CalendarTestHarness harness, string calendarId, string title)
    {
        return harness.RunAsync(async services =>
        {
            var calendarEvent = Assert.Single(await services.GetRequiredService<ICalendarEventStore>().ListByCalendarAsync(calendarId, 0, 100, TestContext.Current.CancellationToken));
            var updated = await services.GetRequiredService<ICalendarEventManager>().UpdateAsync(calendarEvent, target => target.Title = title, new CalendarChangeOptions { Actor = "user1" }, TestContext.Current.CancellationToken);

            Assert.True(updated.Succeeded);
        });
    }

    /// <summary>
    /// An external calendar kept in memory, with ETag-style versions and change tokens.
    /// </summary>
    internal sealed class FakeRemoteCalendar
    {
        private readonly List<(int Token, string Id, bool Deleted)> _log = [];
        private int _token;
        private int _ids;

        public Dictionary<string, (ICalendarEventData Data, int Version)> Events { get; } = new(StringComparer.Ordinal);

        public List<CalendarProviderPushContext> Pushes { get; } = [];

        public List<string> Deletes { get; } = [];

        public string FailUrl { get; set; }

        public bool RefuseAccess { get; set; }

        public bool ExpireTokens { get; set; }

        public int FullReads { get; set; }

        public string CurrentToken => _token.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void Add(string id, string title, int hour)
        {
            Events[id] = (new ICalendarEventData { Uid = id, Summary = title, TimeZoneId = "UTC", Start = Hour(hour), End = Hour(hour + 1), Version = "v1" }, 1);
            _log.Add((++_token, id, false));
        }

        public void Change(string id, string title)
        {
            var (data, version) = Events[id];

            data.Summary = title;
            data.Version = "v" + (version + 1);
            Events[id] = (data, version + 1);
            _log.Add((++_token, id, false));
        }

        public void Delete(string id)
        {
            Events.Remove(id);
            _log.Add((++_token, id, true));
        }

        public IReadOnlyList<ICalendarEventData> ReadAll(CalendarSubscription subscription)
        {
            Check(subscription);
            FullReads++;

            return Events.Values.Select(item => Clone(item.Data)).ToList();
        }

        public CalendarProviderChanges ReadChanges(CalendarSubscription subscription, string token)
        {
            Check(subscription);

            if (ExpireTokens)
            {
                ExpireTokens = false;

                return new CalendarProviderChanges { FullSyncRequired = true };
            }

            var since = int.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
            var changed = _log.Where(entry => entry.Token > since).Select(entry => entry.Id).Distinct().ToList();

            return new CalendarProviderChanges
            {
                Events = changed.Where(Events.ContainsKey).Select(id => Clone(Events[id].Data)).ToList(),
                DeletedIds = changed.Where(id => !Events.ContainsKey(id)).ToList(),
                NextSyncToken = CurrentToken,
            };
        }

        public CalendarProviderPushResult Push(CalendarProviderPushContext context)
        {
            Pushes.Add(new CalendarProviderPushContext { Event = context.Event, Mapping = context.Mapping is null ? null : new CalendarProviderMapping { ExternalEventId = context.Mapping.ExternalEventId, Version = context.Mapping.Version } });

            var id = context.Mapping?.ExternalEventId ?? "remote-" + (++_ids);

            if (context.Mapping?.Version is not null && Events.TryGetValue(id, out var current) && current.Data.Version != context.Mapping.Version)
            {
                throw new CalendarProviderConflictException();
            }

            var version = Events.TryGetValue(id, out var existing) ? existing.Version + 1 : 1;
            var data = new ICalendarEventData
            {
                Uid = id,
                Summary = context.Event.Title,
                TimeZoneId = context.Event.TimeZoneId,
                Start = context.Event.Start,
                End = context.Event.End,
                Version = "v" + version,
            };

            Events[id] = (data, version);

            // Our own write isn't reported back as a change.
            _token++;

            return new CalendarProviderPushResult { ExternalEventId = id, Version = data.Version };
        }

        public void Remove(CalendarProviderPushContext context)
        {
            Deletes.Add(context.Mapping.ExternalEventId);
            Events.Remove(context.Mapping.ExternalEventId);
            _token++;
        }

        private void Check(CalendarSubscription subscription)
        {
            if (RefuseAccess)
            {
                throw new CalendarProviderAuthorizationException();
            }

            if (FailUrl is not null && subscription.Url == FailUrl)
            {
                throw new HttpRequestException("The feed is down.");
            }
        }

        private static ICalendarEventData Clone(ICalendarEventData data)
        {
            return new ICalendarEventData { Uid = data.Uid, Summary = data.Summary, TimeZoneId = data.TimeZoneId, Start = data.Start, End = data.End, Version = data.Version };
        }
    }

    internal sealed class FakeCalendarProvider : IIncrementalCalendarProvider, IWritableCalendarProvider
    {
        public const string ProviderName = "fake";

        private readonly FakeRemoteCalendar _remote;

        public FakeCalendarProvider(FakeRemoteCalendar remote)
        {
            _remote = remote;
        }

        public string Name => ProviderName;

        public LocalizationSource DisplayName { get; } = new("Fake");

        public Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default)
            => Task.FromResult(_remote.ReadAll(subscription));

        public Task<CalendarProviderChanges> PullChangesAsync(Calendar calendar, CalendarSubscription subscription, string syncToken, CancellationToken cancellationToken = default)
            => Task.FromResult(_remote.ReadChanges(subscription, syncToken));

        public Task<string> GetSyncTokenAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default)
            => Task.FromResult(_remote.CurrentToken);

        public Task<CalendarProviderPushResult> PushAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_remote.Push(context));

        public Task DeleteAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default)
        {
            _remote.Remove(context);

            return Task.CompletedTask;
        }
    }
}
