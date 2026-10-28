using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The dialer decision log answers "why was this record not called?" and "what did routing decide about this agent?"
/// from indexed columns. These run the real index provider, migration and decision log against SQLite, so a filter that
/// leaks another record's or agent's decisions, a repeat that is not counted on the event it repeats, or a decision
/// written before the log existed that the backfill leaves out, fails here rather than as a report that is quietly
/// wrong.
/// </summary>
public sealed class DialerDecisionLogPersistenceTests
{
    private static readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListAsync_FilteredByARecord_ShowsTheLastDecisionsMadeAboutIt_NewestFirst_WithTheirReasons()
    {
        // Arrange
        var databasePath = DatabasePath("record");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Routing("first", "queue-1", "item-1", "act-1", _start, ActivityRoutingReasons.NoAgentsAvailable),
                Suppression("suppressed", "act-1", _start.AddMinutes(5), DialerSuppressionReason.DoNotCall, campaignId: "campaign-1", queueId: "queue-1"),
                Routing("other-record", "queue-1", "item-2", "act-2", _start.AddMinutes(6), ActivityRoutingReasons.Selected, succeeded: true, selectedAgentId: "agent-1", candidates: [Candidate("agent-1", eligible: true)]),
                Routing("latest", "queue-1", "item-1", "act-1", _start.AddMinutes(10), ActivityRoutingReasons.NoAgentMatchedPolicy, candidates: [Candidate("agent-1", eligible: false, "At capacity (1/1 active interactions).")]));

            await using var session = store.CreateSession();

            // Act
            var events = await CreateLog(session).ListAsync(new DialerDecisionLogQuery { ActivityItemId = "act-1" }, 100, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["latest", "suppressed", "first"], events.Select(interactionEvent => interactionEvent.ItemId));

            var latest = events[0].GetData<ActivityRoutingDecisionEventData>();
            Assert.Equal(ActivityRoutingReasons.NoAgentMatchedPolicy, latest.Reason);
            Assert.Equal(["At capacity (1/1 active interactions)."], Assert.Single(latest.Candidates).Reasons);

            var suppression = events[1].GetData<DialerSuppressionEventData>();
            Assert.Equal(DialerSuppressionReason.DoNotCall, suppression.Reason);
            Assert.Equal("+15550000001", suppression.Destination);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ListAsync_FilteredByAnAgent_ShowsTheDecisionsThatWeighedSelectedOrReservedTheAgent()
    {
        // Arrange
        var databasePath = DatabasePath("agent");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Routing(
                    "weighed",
                    "queue-1",
                    "item-1",
                    "act-1",
                    _start,
                    ActivityRoutingReasons.Selected,
                    succeeded: true,
                    selectedAgentId: "agent-2",
                    candidates: [Candidate("agent-1", eligible: false, "Lacks the required skill 'Spanish'."), Candidate("agent-2", eligible: true)]),
                Routing("other-agent", "queue-1", "item-2", "act-2", _start.AddMinutes(1), ActivityRoutingReasons.NoAgentMatchedPolicy, candidates: [Candidate("agent-3", eligible: false, "Busy.")]),
                Suppression("reserved", "act-3", _start.AddMinutes(2), DialerSuppressionReason.OutsideCallingWindow, campaignId: "campaign-1", queueId: "queue-1", agentId: "agent-1"),
                Routing("nobody", "queue-1", "item-3", "act-4", _start.AddMinutes(3), ActivityRoutingReasons.NoAgentsAvailable));

            await using var session = store.CreateSession();
            var log = CreateLog(session);

            // Act
            var aboutAgentOne = await log.ListAsync(new DialerDecisionLogQuery { AgentId = "agent-1" }, 100, TestContext.Current.CancellationToken);
            var aboutAgentTwo = await log.ListAsync(new DialerDecisionLogQuery { AgentId = "agent-2" }, 100, TestContext.Current.CancellationToken);
            var entries = await log.GetEntriesAsync(new DialerDecisionLogQuery { AgentId = "agent-1" }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["reserved", "weighed"], aboutAgentOne.Select(interactionEvent => interactionEvent.ItemId));
            Assert.Equal(["weighed"], aboutAgentTwo.Select(interactionEvent => interactionEvent.ItemId));
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.EventType == ContactCenterConstants.Events.DialSuppressed && entry.Reason == nameof(DialerSuppressionReason.OutsideCallingWindow));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetEntriesAsync_FiltersByCampaignQueueReasonAndPeriod()
    {
        // Arrange
        var databasePath = DatabasePath("filters");
        var store = await CreateStoreAsync(databasePath);
        var campaignQueue = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

        try
        {
            await SeedAsync(
                store,
                Routing("campaign-routing", campaignQueue, "item-1", "act-1", _start.AddMinutes(1), ActivityRoutingReasons.NoAgentsAvailable),
                Suppression("campaign-suppression", "act-2", _start.AddMinutes(2), DialerSuppressionReason.DoNotCall, campaignId: "campaign-1", queueId: campaignQueue),
                Routing("inbound", "queue-1", "item-3", "act-3", _start.AddMinutes(3), ActivityRoutingReasons.NoAgentsAvailable),
                Routing("before-the-period", "queue-1", "item-4", "act-4", _start.AddHours(-2), ActivityRoutingReasons.NoAgentsAvailable),
                Routing("repeated-into-the-period", "queue-2", "item-5", "act-5", _start.AddMinutes(-30), ActivityRoutingReasons.NoAgentsAvailable, occurrenceCount: 40, lastSeenUtc: _start.AddMinutes(9)));

            await using var session = store.CreateSession();
            var log = CreateLog(session);
            var period = new DialerDecisionLogQuery { FromUtc = _start, ToUtc = _start.AddHours(1) };

            // Act
            var all = await log.GetEntriesAsync(period, TestContext.Current.CancellationToken);
            var campaign = await log.GetEntriesAsync(new DialerDecisionLogQuery { FromUtc = _start, ToUtc = _start.AddHours(1), CampaignId = "campaign-1" }, TestContext.Current.CancellationToken);
            var queue = await log.GetEntriesAsync(new DialerDecisionLogQuery { FromUtc = _start, ToUtc = _start.AddHours(1), QueueId = "queue-1" }, TestContext.Current.CancellationToken);
            var doNotCall = await log.GetEntriesAsync(new DialerDecisionLogQuery { Reason = nameof(DialerSuppressionReason.DoNotCall) }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                ["campaign-routing", "campaign-suppression", "inbound", "repeated-into-the-period"],
                all.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
            Assert.Equal(["campaign-routing", "campaign-suppression"], campaign.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
            Assert.Equal(["inbound"], queue.Select(entry => entry.ItemId));
            Assert.Equal(["campaign-suppression"], doNotCall.Select(entry => entry.ItemId));

            var repeated = Assert.Single(all, entry => entry.ItemId == "repeated-into-the-period");
            Assert.Equal(40, repeated.OccurrenceCount);
            Assert.Equal(_start.AddMinutes(9), repeated.LastSeenUtc);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task TryCoalesceRoutingDecisionAsync_WhenAFailureRepeatsTheQueuesLastDecision_CountsItOnThatEvent()
    {
        // Arrange
        var databasePath = DatabasePath("coalesce");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, Routing("first", "queue-1", "item-1", "act-1", _start, ActivityRoutingReasons.NoAgentsAvailable));

            // Act
            var coalesced = new List<bool>();

            foreach (var minutes in new[] { 1, 2 })
            {
                await using var session = store.CreateSession();

                coalesced.Add(await CreateLog(session).TryCoalesceRoutingDecisionAsync(
                    RoutingData("queue-1", "item-1", "act-1", ActivityRoutingReasons.NoAgentsAvailable),
                    _start.AddMinutes(minutes),
                    TestContext.Current.CancellationToken));

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(2, coalesced.Count(value => value));

            await using var readSession = store.CreateSession();
            var log = CreateLog(readSession);
            var entry = Assert.Single(await log.GetEntriesAsync(new DialerDecisionLogQuery(), TestContext.Current.CancellationToken));
            Assert.Equal("first", entry.ItemId);
            Assert.Equal(3, entry.OccurrenceCount);
            Assert.Equal(_start, entry.OccurredUtc);
            Assert.Equal(_start.AddMinutes(2), entry.LastSeenUtc);

            var interactionEvent = Assert.Single(await log.ListAsync(new DialerDecisionLogQuery(), 10, TestContext.Current.CancellationToken));
            var data = interactionEvent.GetData<ActivityRoutingDecisionEventData>();
            Assert.Equal(3, data.OccurrenceCount);
            Assert.Equal(_start.AddMinutes(2), data.LastSeenUtc);

            var seen = await log.GetRoutingDecisionsSeenSinceAsync(["queue-1"], _start.AddSeconds(90), TestContext.Current.CancellationToken);
            Assert.Equal("first", Assert.Single(seen).ItemId);

            var last = await log.GetLastRoutingDecisionTimesAsync(["queue-1", "queue-without-decisions"], TestContext.Current.CancellationToken);
            Assert.Equal(_start.AddMinutes(2), Assert.Single(last).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData("another-reason")]
    [InlineData("another-record")]
    [InlineData("another-candidate-reason")]
    [InlineData("a-success")]
    [InlineData("after-a-long-pause")]
    public async Task TryCoalesceRoutingDecisionAsync_WhenTheDecisionSaysSomethingNew_LeavesItToBePublished(string change)
    {
        // Arrange
        var databasePath = DatabasePath($"no-coalesce-{change}");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, Routing("first", "queue-1", "item-1", "act-1", _start, ActivityRoutingReasons.NoAgentMatchedPolicy, candidates: [Candidate("agent-1", eligible: false, "Busy.")]));

            var next = RoutingData("queue-1", "item-1", "act-1", ActivityRoutingReasons.NoAgentMatchedPolicy, candidates: [Candidate("agent-1", eligible: false, "Busy.")]);
            var decidedUtc = _start.AddMinutes(1);

            switch (change)
            {
                case "another-reason":
                    next.Reason = ActivityRoutingReasons.NoAgentsAvailable;
                    next.Candidates = [];
                    break;
                case "another-record":
                    next.QueueItemId = "item-2";
                    next.ActivityItemId = "act-2";
                    break;
                case "another-candidate-reason":
                    next.Candidates = [Candidate("agent-1", eligible: false, "At capacity (1/1 active interactions).")];
                    break;
                case "a-success":
                    next.Succeeded = true;
                    next.Reason = ActivityRoutingReasons.Selected;
                    break;
                case "after-a-long-pause":
                    decidedUtc = _start + RoutingDecisionCoalescing.MaximumGap + TimeSpan.FromMinutes(1);
                    break;
            }

            await using var session = store.CreateSession();

            // Act
            var coalesced = await CreateLog(session).TryCoalesceRoutingDecisionAsync(next, decidedUtc, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(coalesced);

            await using var readSession = store.CreateSession();
            var entry = Assert.Single(await CreateLog(readSession).GetEntriesAsync(new DialerDecisionLogQuery(), TestContext.Current.CancellationToken));
            Assert.Equal(1, entry.OccurrenceCount);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task TryCoalesceRoutingDecisionAsync_AfterTheQueueAssignedWork_StartsANewEvent()
    {
        // Arrange
        var databasePath = DatabasePath("after-success");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Routing("failed", "queue-1", "item-1", "act-1", _start, ActivityRoutingReasons.NoAgentsAvailable),
                Routing("assigned", "queue-1", "item-9", "act-9", _start.AddSeconds(30), ActivityRoutingReasons.Selected, succeeded: true, selectedAgentId: "agent-1", candidates: [Candidate("agent-1", eligible: true)]));

            await using var session = store.CreateSession();

            // Act
            var coalesced = await CreateLog(session).TryCoalesceRoutingDecisionAsync(
                RoutingData("queue-1", "item-1", "act-1", ActivityRoutingReasons.NoAgentsAvailable),
                _start.AddMinutes(1),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.False(coalesced);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Backfill_IndexesTheDecisionsWrittenBeforeTheLogExisted_AndGivesOldSuppressionsTheirRecordsCampaign()
    {
        // Arrange
        var databasePath = DatabasePath("backfill");
        var legacyStore = await CreateStoreAsync(databasePath, withDecisionLog: false);
        IStore store = null;

        try
        {
            await using (var session = legacyStore.CreateSession())
            {
                await session.SaveAsync(
                    new OmnichannelActivity
                    {
                        ItemId = "act-2",
                        CampaignId = "campaign-1",
                    },
                    collection: OmnichannelConstants.CollectionName,
                    cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await SeedAsync(
                legacyStore,
                Routing("old-routing", "queue-1", "item-1", "act-1", _start, ActivityRoutingReasons.NoAgentsAvailable),
                Suppression("old-suppression", "act-2", _start.AddMinutes(1), DialerSuppressionReason.NumberNotInService, campaignId: null, queueId: null),
                Routing("old-assignment", "queue-1", "item-3", "act-3", _start.AddMinutes(2), ActivityRoutingReasons.Selected, succeeded: true, selectedAgentId: "agent-1", candidates: [Candidate("agent-1", eligible: true)]));

            legacyStore.Dispose();
            legacyStore = null;

            store = await CreateStoreAsync(databasePath, withDecisionLog: true, createSchema: false);
            await CreateDecisionSchemaAsync(store);

            // Act
            var indexed = 0;
            var finished = true;

            foreach (var source in DialerDecisionIndexBackfill.Sources)
            {
                var result = await DialerDecisionIndexBackfill.RunAsync(store, source, cursor: null, maxPages: 5, TestContext.Current.CancellationToken);

                indexed += result.Indexed;
                finished &= result.Finished;
            }

            // A second pass finds every event already indexed and writes nothing.
            var again = 0;

            foreach (var source in DialerDecisionIndexBackfill.Sources)
            {
                again += (await DialerDecisionIndexBackfill.RunAsync(store, source, cursor: null, maxPages: 5, TestContext.Current.CancellationToken)).Indexed;
            }

            // Assert
            Assert.Equal(3, indexed);
            Assert.True(finished);
            Assert.Equal(0, again);

            await using var readSession = store.CreateSession();
            var log = CreateLog(readSession);
            var entries = await log.GetEntriesAsync(new DialerDecisionLogQuery(), TestContext.Current.CancellationToken);

            Assert.Equal(["old-assignment", "old-routing", "old-suppression"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
            Assert.Equal(["old-suppression"], (await log.GetEntriesAsync(new DialerDecisionLogQuery { CampaignId = "campaign-1" }, TestContext.Current.CancellationToken)).Select(entry => entry.ItemId));
            Assert.Equal(["old-assignment"], (await log.ListAsync(new DialerDecisionLogQuery { AgentId = "agent-1" }, 10, TestContext.Current.CancellationToken)).Select(interactionEvent => interactionEvent.ItemId));
        }
        finally
        {
            legacyStore?.Dispose();
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static DialerDecisionLog CreateLog(ISession session)
        => new(session, new DefaultInteractionEventUpcastService([]));

    private static ActivityRoutingCandidateDecisionData Candidate(string agentId, bool eligible, params string[] reasons)
        => new()
        {
            AgentId = agentId,
            IsEligible = eligible,
            Reasons = [.. reasons],
        };

    private static ActivityRoutingDecisionEventData RoutingData(
        string queueId,
        string queueItemId,
        string activityItemId,
        string reason,
        bool succeeded = false,
        string selectedAgentId = null,
        ActivityRoutingCandidateDecisionData[] candidates = null)
        => new()
        {
            QueueId = queueId,
            QueueItemId = queueItemId,
            ActivityItemId = activityItemId,
            Reason = reason,
            Succeeded = succeeded,
            SelectedAgentId = selectedAgentId,
            Candidates = candidates ?? [],
        };

    private static InteractionEvent Routing(
        string id,
        string queueId,
        string queueItemId,
        string activityItemId,
        DateTime occurredUtc,
        string reason,
        bool succeeded = false,
        string selectedAgentId = null,
        ActivityRoutingCandidateDecisionData[] candidates = null,
        int occurrenceCount = 1,
        DateTime? lastSeenUtc = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = id,
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            AggregateType = nameof(QueueItem),
            AggregateId = queueItemId,
            OccurredUtc = occurredUtc,
            RecordedUtc = occurredUtc,
            IdempotencyKey = id,
        };

        var data = RoutingData(queueId, queueItemId, activityItemId, reason, succeeded, selectedAgentId, candidates);
        data.OccurrenceCount = occurrenceCount;
        data.LastSeenUtc = lastSeenUtc;
        interactionEvent.SetData(data);

        return interactionEvent;
    }

    private static InteractionEvent Suppression(
        string id,
        string activityItemId,
        DateTime occurredUtc,
        DialerSuppressionReason reason,
        string campaignId,
        string queueId,
        string agentId = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = id,
            EventType = ContactCenterConstants.Events.DialSuppressed,
            AggregateType = nameof(OmnichannelActivity),
            AggregateId = activityItemId,
            OccurredUtc = occurredUtc,
            RecordedUtc = occurredUtc,
            IdempotencyKey = id,
        };

        interactionEvent.SetData(new DialerSuppressionEventData
        {
            ProfileItemId = "profile-1",
            ActivityItemId = activityItemId,
            Reason = reason,
            Description = "Suppressed by the compliance gate.",
            Destination = "+15550000001",
            CampaignId = campaignId,
            QueueId = queueId,
            AgentId = agentId,
        });

        return interactionEvent;
    }

    private static async Task SeedAsync(IStore store, params InteractionEvent[] events)
    {
        await using var session = store.CreateSession();

        foreach (var interactionEvent in events)
        {
            await session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath, bool withDecisionLog = true, bool createSchema = true)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        var providers = new List<IIndexProvider>
        {
            new InteractionEventIndexProvider(),
            new OmnichannelActivityIndexProvider(),
        };

        if (withDecisionLog)
        {
            providers.Add(new DialerDecisionIndexProvider());
        }

        store.RegisterIndexes(providers.ToArray());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);

        if (!createSchema)
        {
            return store;
        }

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        Assert.Equal(2, await new InteractionEventIndexMigrations(store) { SchemaBuilder = schemaBuilder }.CreateAsync());
        await new OmnichannelActivityIndexMigrations(store) { SchemaBuilder = schemaBuilder }.CreateAsync();

        if (withDecisionLog)
        {
            Assert.Equal(2, await new DialerDecisionIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync());
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task CreateDecisionSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, await new DialerDecisionIndexMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) }.CreateAsync());

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static string DatabasePath(string name)
        => Path.Combine(Path.GetTempPath(), $"cc-decision-log-{name}-{Guid.NewGuid():N}.db");
}
