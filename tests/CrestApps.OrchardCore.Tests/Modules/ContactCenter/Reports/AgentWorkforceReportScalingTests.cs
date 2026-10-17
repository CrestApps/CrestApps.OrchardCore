using System.Diagnostics;
using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The agent workforce reports timed out on a busy tenant once the period was more than a few weeks long: they read
/// every state change of every agent in the period in one statement and built every interval from it. These run a
/// month of a synthetic busy log through the real store, migrations and summaries on SQLite, and require the reports
/// to read whole days from the daily summaries, to stay inside a budget of rows read and time taken, and to show
/// exactly what the same reports computed over the whole log in memory show.
/// </summary>
public sealed class AgentWorkforceReportScalingTests
{
    private const int AgentCount = 20;
    private const int SeedDays = 36;
    private const int FirstNightAgent = 14;
    private const int SupersededAgent = 15;

    private static readonly DateTime _seedStart = new(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _periodFrom = new(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _periodTo = _periodFrom.AddDays(31).AddTicks(-1);

    private static readonly AgentWorkforceReportKind[] _summarizedKinds =
    [
        AgentWorkforceReportKind.TimeSummary,
        AgentWorkforceReportKind.StatusDuration,
        AgentWorkforceReportKind.ReadyNotReady,
        AgentWorkforceReportKind.Utilization,
        AgentWorkforceReportKind.Occupancy,
        AgentWorkforceReportKind.ReasonBreakdown,
        AgentWorkforceReportKind.QueueMembershipHours,
        AgentWorkforceReportKind.CampaignMembershipHours,
        AgentWorkforceReportKind.PayrollTimecard,
    ];

    private static int _sequence;

    [Fact]
    public Task WorkforceReports_OverAMonthOfABusyLog_ReadWholeDaysFromTheSummaries_AndShowWhatTheWholeLogShows()
        => CrestApps.OrchardCore.Tests.CiFailureAnnotationAttribute.AnnotateAsync(WorkforceReports_OverAMonthOfABusyLog_ReadWholeDaysFromTheSummaries_AndShowWhatTheWholeLogShowsCoreAsync);

    private static async Task WorkforceReports_OverAMonthOfABusyLog_ReadWholeDaysFromTheSummaries_AndShowWhatTheWholeLogShowsCoreAsync()
    {
        // Arrange
        var log = SyntheticLog();
        var inPeriod = log.Count(interactionEvent => interactionEvent.OccurredUtc >= _periodFrom && interactionEvent.OccurredUtc <= _periodTo);

        Assert.True(log.Count >= 20_000, $"The synthetic log holds only {log.Count} state changes.");
        Assert.True(inPeriod >= 15_000, $"The period holds only {inPeriod} state changes.");

        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-workforce-scaling-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, log);
            await RollUntilCurrentAsync(store);

            // Act & Assert: every kind that reads the summaries shows what the whole log shows.
            foreach (var kind in _summarizedKinds)
            {
                var expected = Render(await RunInMemoryAsync(kind, log));
                var summarized = await RunAgainstStoreAsync(store, kind, useSummaries: true);

                Assert.Equal(expected, Render(summarized.Document));
            }

            // The same report read from the log alone, through the paged window and the batched opening states.
            var expectedReady = Render(await RunInMemoryAsync(AgentWorkforceReportKind.ReadyNotReady, log));
            var fromLog = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.ReadyNotReady, useSummaries: false);
            var fromSummaries = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.ReadyNotReady, useSummaries: true);

            Assert.Equal(expectedReady, Render(fromLog.Document));
            Assert.Equal(expectedReady, Render(fromSummaries.Document));

            // The budget: the log alone reads the whole period; the summaries read the first and last day of it, and
            // the hour before the last day, with each agent's opening state.
            Assert.True(fromLog.EventsRead >= inPeriod, $"The report over the log read {fromLog.EventsRead} events of the {inPeriod} in the period.");
            Assert.True(
                fromSummaries.EventsRead * 8 < fromLog.EventsRead,
                $"Reading whole days from the summaries still read {fromSummaries.EventsRead} events, against {fromLog.EventsRead} for the log alone.");
            Assert.True(
                fromSummaries.Elapsed < TimeSpan.FromSeconds(30),
                $"The report took {fromSummaries.Elapsed.TotalSeconds:N1} seconds.");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// A state change recorded late for a day that already has a summary makes the log disagree with the summary. The
    /// report reads that day, and the days after it, from the log until the summary is recomputed, and the roller
    /// recomputes it once it finds the disagreement.
    /// </summary>
    [Fact]
    public Task WorkforceReports_AfterALateChangeToASummarizedDay_StillShowWhatTheWholeLogShows()
        => CrestApps.OrchardCore.Tests.CiFailureAnnotationAttribute.AnnotateAsync(WorkforceReports_AfterALateChangeToASummarizedDay_StillShowWhatTheWholeLogShowsCoreAsync);

    private static async Task WorkforceReports_AfterALateChangeToASummarizedDay_StillShowWhatTheWholeLogShowsCoreAsync()
    {
        // Arrange
        var log = SyntheticLog();
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-workforce-late-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, log);
            await RollUntilCurrentAsync(store);

            var before = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.StatusDuration, useSummaries: true);

            // Act: a break nobody saw until now, on a recent day the roller re-checks on every run.
            var recent = LateBreak(log, "agent-03", new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)).ToArray();
            log.AddRange(recent);
            await SeedAsync(store, recent);

            var afterRecentChange = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.StatusDuration, useSummaries: true);
            var expectedAfterRecentChange = Render(await RunInMemoryAsync(AgentWorkforceReportKind.StatusDuration, log));

            await RollUntilCurrentAsync(store);
            var afterRoll = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.StatusDuration, useSummaries: true);

            // Act: one on an older day, which the report itself finds the log no longer matches.
            var older = LateBreak(log, "agent-04", new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc)).ToArray();
            log.AddRange(older);
            await SeedAsync(store, older);

            var afterOlderChange = await RunAgainstStoreAsync(store, AgentWorkforceReportKind.StatusDuration, useSummaries: true);
            var expectedAfterOlderChange = Render(await RunInMemoryAsync(AgentWorkforceReportKind.StatusDuration, log));

            // Assert
            Assert.NotEqual(Render(before.Document), expectedAfterRecentChange);
            Assert.Equal(expectedAfterRecentChange, Render(afterRecentChange.Document));
            Assert.True(afterRecentChange.EventsRead > before.EventsRead, "The day the log no longer matches was not read from the log.");

            Assert.Equal(expectedAfterRecentChange, Render(afterRoll.Document));
            Assert.True(afterRoll.EventsRead < afterRecentChange.EventsRead, "The roller did not recompute the day it re-checks.");

            Assert.NotEqual(expectedAfterRecentChange, expectedAfterOlderChange);
            Assert.Equal(expectedAfterOlderChange, Render(afterOlderChange.Document));
            Assert.True(afterOlderChange.EventsRead > afterRoll.EventsRead, "The older day the log no longer matches was not read from the log.");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<(ReportDocument Document, int EventsRead, TimeSpan Elapsed)> RunAgainstStoreAsync(
        IStore store,
        AgentWorkforceReportKind kind,
        bool useSummaries)
    {
        await using var session = store.CreateSession();

        var options = new ContactCenterReportingOptions();
        var counter = new EventCounter();
        var eventStore = counter.Wrap(new InteractionEventStore(session, new DefaultInteractionEventUpcastService([])));
        var reader = useSummaries
            ? new AgentTimeSummaryReader(session, eventStore, Options.Create(options), NullLogger<AgentTimeSummaryReader>.Instance)
            : null;

        var stopwatch = Stopwatch.StartNew();
        var document = await CreateProvider(kind, eventStore, options, reader).RunAsync(Context(), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        return (Document: document, EventsRead: counter.Read, Elapsed: stopwatch.Elapsed);
    }

    private static Task<ReportDocument> RunInMemoryAsync(AgentWorkforceReportKind kind, List<InteractionEvent> log)
        => CreateProvider(kind, InMemoryLog(log), new ContactCenterReportingOptions(), summaryReader: null)
            .RunAsync(Context(), TestContext.Current.CancellationToken);

    private static async Task RollUntilCurrentAsync(IStore store)
    {
        for (var run = 0; run < 10; run++)
        {
            await using var session = store.CreateSession();

            var roller = new AgentTimeSummaryRoller(
                session,
                new InteractionEventStore(session, new DefaultInteractionEventUpcastService([])),
                Agents(),
                Options.Create(new ContactCenterReportingOptions()),
                Mock.Of<IClock>(clock => clock.UtcNow == _now),
                NullLogger<AgentTimeSummaryRoller>.Instance);

            if (await roller.RollAsync(TestContext.Current.CancellationToken) == 0)
            {
                return;
            }
        }

        Assert.Fail("The summaries were still being computed after ten runs.");
    }

    private static AgentWorkforceReportProvider CreateProvider(
        AgentWorkforceReportKind kind,
        IInteractionEventStore eventStore,
        ContactCenterReportingOptions options,
        AgentTimeSummaryReader summaryReader)
    {
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new AgentWorkforceReportProvider(
            eventStore,
            Agents(),
            campaigns.Object,
            kind,
            guard.Object,
            new PassThroughStringLocalizer<AgentWorkforceReportProvider>(),
            Mock.Of<IClock>(clock => clock.UtcNow == _now),
            Mock.Of<ILocalClock>(),
            Options.Create(options),
            summaryReader);
    }

    private static IAgentProfileManager Agents()
    {
        var profiles = Enumerable.Range(0, AgentCount)
            .Select(agent => new AgentProfile
            {
                ItemId = AgentId(agent),
                UserName = $"user{agent:00}",
            })
            .ToArray();

        var agents = new Mock<IAgentProfileManager>();
        agents
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. profiles]);

        return agents.Object;
    }

    private static ReportContext Context()
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _periodFrom, ToUtc = _periodTo });

        return new ReportContext(filter);
    }

    // The whole log in memory, read the way the store's contract says it is read, with nothing paged or batched: the
    // computation the reports did before any of this existed.
    private static IInteractionEventStore InMemoryLog(List<InteractionEvent> log)
    {
        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);

        eventStore
            .Setup(value => value.GetByAggregateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns((string aggregateType, IEnumerable<string> eventTypes, IEnumerable<string> aggregateIds, DateTime fromUtc, DateTime throughUtc, CancellationToken _) =>
            {
                var types = eventTypes?.ToHashSet(StringComparer.Ordinal) ?? [];
                var ids = aggregateIds?.ToHashSet(StringComparer.Ordinal);

                return Task.FromResult<IReadOnlyList<InteractionEvent>>(log
                    .Where(interactionEvent => interactionEvent.AggregateType == aggregateType &&
                        interactionEvent.OccurredUtc >= fromUtc &&
                        interactionEvent.OccurredUtc <= throughUtc &&
                        (types.Count == 0 || types.Contains(interactionEvent.EventType)) &&
                        (ids is null || ids.Contains(interactionEvent.AggregateId)))
                    .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
                    .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal)
                    .ToArray());
            });

        eventStore
            .Setup(value => value.GetLatestBeforeAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns((string aggregateType, IEnumerable<string> eventTypes, IEnumerable<string> aggregateIds, DateTime beforeUtc, CancellationToken _) =>
            {
                var types = eventTypes?.ToHashSet(StringComparer.Ordinal) ?? [];

                return Task.FromResult<IReadOnlyList<InteractionEvent>>((aggregateIds ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .Select(aggregateId => log
                        .Select((interactionEvent, position) => (Event: interactionEvent, Position: position))
                        .Where(entry => entry.Event.AggregateType == aggregateType &&
                            entry.Event.AggregateId == aggregateId &&
                            entry.Event.OccurredUtc < beforeUtc &&
                            (types.Count == 0 || types.Contains(entry.Event.EventType)))
                        .OrderByDescending(entry => entry.Event.OccurredUtc)
                        .ThenByDescending(entry => entry.Position)
                        .Select(entry => entry.Event)
                        .FirstOrDefault())
                    .Where(interactionEvent => interactionEvent is not null)
                    .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
                    .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal)
                    .ToArray());
            });

        return eventStore.Object;
    }

    private static string Render(ReportDocument document)
        => string.Join(
            Environment.NewLine,
            document.Sections.SelectMany(section => new[] { section.Title, section.Description ?? string.Empty }
                .Concat(section.Metrics.Select(metric => metric.Label + "=" + metric.Value))
                .Concat(section.Rows.Select(row => row.Kind + ":" + string.Join("|", row.Cells)))));

    // A month and a bit of a busy contact center: day-shift agents from eight in the morning, night-shift agents across
    // midnight, a day off a week, a call every twenty to fifty minutes, breaks with reasons, a change of queues after the
    // first break, one agent on the older presence events for its first ten days, and one sign-off dated by the agent's
    // last heartbeat before midnight and recorded after it, superseding the reservation recorded in between.
    private static List<InteractionEvent> SyntheticLog()
    {
        var random = new Random(926);
        var log = new List<InteractionEvent>();

        for (var agent = 0; agent < AgentCount; agent++)
        {
            var agentId = AgentId(agent);
            var night = agent >= FirstNightAgent;
            string[] campaigns = agent % 4 == 0 ? [$"campaign-{agent % 2}"] : [];
            var agentLog = new List<InteractionEvent>();

            for (var day = 0; day < SeedDays; day++)
            {
                if ((day + agent) % 7 == 6)
                {
                    continue;
                }

                var date = _seedStart.AddDays(day);
                var start = date.AddHours(night ? 20 : 8).AddMinutes(random.Next(0, 30)).AddSeconds(random.Next(0, 60));
                var end = start.AddHours(8).AddMinutes(random.Next(0, 30));
                string[] queues = [$"queue-{agent % 3}"];

                if (agent == 7 && day < 10)
                {
                    var lunch = start.AddHours(4);
                    agentLog.Add(Presence(agentId, start, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, queues, campaigns));
                    agentLog.Add(Presence(agentId, lunch, AgentPresenceStatus.Available, AgentPresenceStatus.Break, queues, campaigns, "Lunch"));
                    agentLog.Add(Presence(agentId, lunch.AddMinutes(30), AgentPresenceStatus.Break, AgentPresenceStatus.Available, queues, campaigns));
                    agentLog.Add(Presence(agentId, end, AgentPresenceStatus.Available, AgentPresenceStatus.Offline, queues, campaigns));

                    continue;
                }

                agentLog.Add(State(agentId, start, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn, queues, campaigns));

                var at = start;
                var breaks = 0;

                while (true)
                {
                    at = at.AddMinutes(random.Next(20, 50)).AddSeconds(random.Next(0, 60));

                    if (at >= end)
                    {
                        break;
                    }

                    if (breaks < 2 && random.Next(0, 5) == 0)
                    {
                        var reason = breaks == 0 ? "Lunch" : "Coffee";
                        agentLog.Add(State(agentId, at, AgentPresenceStatus.Available, AgentPresenceStatus.Break, AgentStateChangeSources.SetState, queues, campaigns, reason));
                        at = at.AddMinutes(random.Next(10, 30)).AddSeconds(random.Next(0, 60));
                        breaks++;

                        // Back from the first break signed in to one more queue.
                        if (breaks == 1)
                        {
                            queues = [$"queue-{agent % 3}", "queue-overflow"];
                        }

                        agentLog.Add(State(agentId, at, AgentPresenceStatus.Break, AgentPresenceStatus.Available, AgentStateChangeSources.SetState, queues, campaigns));

                        continue;
                    }

                    var callId = $"call-{agent:00}-{day:00}-{at:HHmmss}";
                    agentLog.Add(State(agentId, at, AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, queues, campaigns, interactionId: callId));
                    at = at.AddSeconds(random.Next(10, 40));
                    agentLog.Add(State(agentId, at, AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted, queues, campaigns, interactionId: callId));
                    at = at.AddMinutes(random.Next(3, 15)).AddSeconds(random.Next(0, 60));
                    agentLog.Add(State(agentId, at, AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted, queues, campaigns, interactionId: callId));
                    at = at.AddSeconds(random.Next(30, 180));
                    agentLog.Add(State(agentId, at, AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted, queues, campaigns));
                }

                agentLog.Add(State(agentId, at > end ? at.AddSeconds(30) : end, AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut, queues, campaigns));
            }

            if (agent == SupersededAgent)
            {
                // The sweep signs the agent off at 23:59:30 by its last heartbeat, but only records it at 00:01; the
                // reservation routing recorded at 00:00:20 in between does not count.
                var midnight = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
                agentLog.RemoveAll(interactionEvent => interactionEvent.OccurredUtc >= midnight.AddMinutes(-2) && interactionEvent.OccurredUtc <= midnight.AddMinutes(6));
                agentLog.Add(State(agentId, midnight.AddSeconds(-30), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut, ["queue-2"], [], recordedUtc: midnight.AddMinutes(1)));
                agentLog.Add(State(agentId, midnight.AddSeconds(20), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, ["queue-2"], [], interactionId: "call-superseded"));
                agentLog.Add(State(agentId, midnight.AddMinutes(5), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn, ["queue-2"], []));
            }

            log.AddRange(agentLog);
        }

        return [.. log.OrderBy(interactionEvent => interactionEvent.OccurredUtc).ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal)];
    }

    private static IEnumerable<InteractionEvent> LateBreak(List<InteractionEvent> log, string agentId, DateTime at)
    {
        var taken = log
            .Where(interactionEvent => interactionEvent.AggregateId == agentId)
            .Select(interactionEvent => interactionEvent.OccurredUtc)
            .ToHashSet();

        while (taken.Contains(at) || taken.Contains(at.AddMinutes(5)))
        {
            at = at.AddSeconds(1);
        }

        yield return State(agentId, at, AgentPresenceStatus.Available, AgentPresenceStatus.Break, AgentStateChangeSources.SetState, ["queue-0"], [], "Unplanned");
        yield return State(agentId, at.AddMinutes(5), AgentPresenceStatus.Break, AgentPresenceStatus.Available, AgentStateChangeSources.SetState, ["queue-0"], []);
    }

    private static InteractionEvent State(
        string agentId,
        DateTime changedUtc,
        AgentPresenceStatus previous,
        AgentPresenceStatus current,
        string source,
        string[] queueIds,
        string[] campaignIds,
        string reason = null,
        string interactionId = null,
        DateTime? recordedUtc = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = ContactCenterConstants.Events.AgentStateChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            InteractionId = interactionId,
            OccurredUtc = changedUtc,
            RecordedUtc = recordedUtc ?? changedUtc,
            ActorType = ContactCenterActorType.Agent,
        };

        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = agentId,
            PreviousState = previous,
            CurrentState = current,
            Source = source,
            InteractionId = interactionId,
            ReasonName = reason,
            QueueIds = [.. queueIds],
            CampaignIds = [.. campaignIds],
            ChangedUtc = changedUtc,
        });

        return interactionEvent;
    }

    private static InteractionEvent Presence(
        string agentId,
        DateTime changedUtc,
        AgentPresenceStatus previous,
        AgentPresenceStatus current,
        string[] queueIds,
        string[] campaignIds,
        string reason = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            OccurredUtc = changedUtc,
            RecordedUtc = changedUtc,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            AgentId = agentId,
            PreviousStatus = previous,
            CurrentStatus = current,
            Reason = reason,
            QueueIds = [.. queueIds],
            CampaignIds = [.. campaignIds],
            ChangedUtc = changedUtc,
        });

        return interactionEvent;
    }

    private static string AgentId(int agent)
        => string.Create(CultureInfo.InvariantCulture, $"agent-{agent:00}");

    private static string NextId()
        => "scale-" + Interlocked.Increment(ref _sequence).ToString("0000000", CultureInfo.InvariantCulture);

    private static async Task SeedAsync(IStore store, IEnumerable<InteractionEvent> events)
    {
        foreach (var batch in events.Chunk(2_000))
        {
            await using var session = store.CreateSession();

            foreach (var interactionEvent in batch)
            {
                await session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionEventIndexProvider(), new AgentDailyTimeSummaryIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var events = new InteractionEventIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };

        await events.CreateAsync();
        await events.UpdateFrom2Async();
        await events.UpdateFrom3Async();
        await events.UpdateFrom6Async();

        var summaries = new AgentDailyTimeSummaryIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };

        await summaries.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// Counts the events a report reads through the store.
    /// </summary>
    private sealed class EventCounter
    {
        public int Read { get; private set; }

        public IInteractionEventStore Wrap(InteractionEventStore inner)
        {
            var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);

            eventStore
                .Setup(value => value.GetByAggregateWindowAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string aggregateType, IEnumerable<string> eventTypes, IEnumerable<string> aggregateIds, DateTime fromUtc, DateTime throughUtc, CancellationToken cancellationToken) =>
                {
                    var events = await inner.GetByAggregateWindowAsync(aggregateType, eventTypes, aggregateIds, fromUtc, throughUtc, cancellationToken);
                    Read += events.Count;

                    return events;
                });

            eventStore
                .Setup(value => value.GetLatestBeforeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string aggregateType, IEnumerable<string> eventTypes, IEnumerable<string> aggregateIds, DateTime beforeUtc, CancellationToken cancellationToken) =>
                {
                    var events = await inner.GetLatestBeforeAsync(aggregateType, eventTypes, aggregateIds, beforeUtc, cancellationToken);
                    Read += events.Count;

                    return events;
                });

            return eventStore.Object;
        }
    }
}
