using System.Collections.Concurrent;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class AutomatedActivitiesProcessorBackgroundTaskTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DoWorkAsync_ProcessesEveryDueAutomatedActivityExactlyOnce()
    {
        // Arrange
        // Seed more activities than a single page so the keyset pagination spans multiple batches. An earlier
        // revision combined the moving document-id cursor with an OFFSET skip, which advanced the window twice per
        // batch and silently skipped every other page. This test fails on that regression and passes on the fix.
        var databasePath = DatabasePath("processor-pagination");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        const int activityCount = 250;

        try
        {
            var expectedItemIds = new HashSet<string>(StringComparer.Ordinal);

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < activityCount; i++)
                {
                    expectedItemIds.Add(await SaveAutomatedSmsActivityAsync(seedSession));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var processor = new RecordingSmsProcessor();

            await using (var workSession = store.CreateSession())
            {
                var serviceProvider = BuildServiceProvider(workSession, processor);
                var task = new AutomatedActivitiesProcessorBackgroundTask();

                // Act
                await task.DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(activityCount, processor.ProcessedItemIds.Count);
            Assert.Equal(
                processor.ProcessedItemIds.Count,
                processor.ProcessedItemIds.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                expectedItemIds.OrderBy(id => id, StringComparer.Ordinal),
                processor.ProcessedItemIds.OrderBy(id => id, StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenAProcessorThrows_ReschedulesTheFailureAndDoesNotReattemptItNextInvocation()
    {
        // Arrange
        // A permanently failing activity (for example a misconfigured automated inventory load) must not keep
        // re-matching the due query on every invocation and consuming a bounded processing slot forever. Running two
        // invocations proves the failure is transitioned out of the due set: poison activities are attempted once in
        // the first run and never re-attempted in the second, and the healthy tail is still reached on the first run.
        var databasePath = DatabasePath("processor-failure");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        const int poisonCount = 50;
        const int healthyCount = 100;

        try
        {
            var poisonItemIds = new HashSet<string>(StringComparer.Ordinal);
            var healthyItemIds = new HashSet<string>(StringComparer.Ordinal);

            await using (var seedSession = store.CreateSession())
            {
                // Poison activities are seeded first so their failures sit at the head of the due query and cannot
                // mask the healthy tail behind them.
                for (var i = 0; i < poisonCount; i++)
                {
                    poisonItemIds.Add(await SaveAutomatedSmsActivityAsync(seedSession));
                }

                for (var i = 0; i < healthyCount; i++)
                {
                    healthyItemIds.Add(await SaveAutomatedSmsActivityAsync(seedSession));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var processor = new RecordingSmsProcessor(poisonItemIds);

            // Act
            for (var invocation = 0; invocation < 2; invocation++)
            {
                await using var workSession = store.CreateSession();
                var serviceProvider = BuildServiceProvider(workSession, processor);
                var task = new AutomatedActivitiesProcessorBackgroundTask();

                await task.DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);
            }

            // Assert
            // Every activity is attempted exactly once across both invocations: healthy ones leave the due set by
            // succeeding, poison ones by being rescheduled. If the failure were not transitioned out, the poison
            // activities would be re-attempted on the second invocation and the count would exceed the distinct set.
            Assert.Equal(poisonCount + healthyCount, processor.ProcessedItemIds.Count);
            Assert.Equal(
                processor.ProcessedItemIds.Count,
                processor.ProcessedItemIds.Distinct(StringComparer.Ordinal).Count());
            Assert.True(healthyItemIds.IsSubsetOf(processor.ProcessedItemIds));
            Assert.True(poisonItemIds.IsSubsetOf(processor.ProcessedItemIds));

            // The failure transition must use the internal ProcessingAttempts counter and must never touch the
            // routing-owned Attempts field (projected from the contact-center work state and surfaced in reports and
            // the UI). Reload the poison activities and assert Attempts is still its seeded default while
            // ProcessingAttempts recorded the failure.
            await using (var assertSession = store.CreateSession())
            {
                var poisonActivities = await assertSession.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                        x => x.Status == ActivityStatus.NotStated,
                        collection: OmnichannelConstants.CollectionName)
                    .ListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(poisonCount, poisonActivities.Count());

                foreach (var activity in poisonActivities)
                {
                    Assert.Contains(activity.ItemId, poisonItemIds);
                    Assert.Equal(1, activity.Attempts);
                    Assert.True(activity.ProcessingAttempts >= 1);
                }
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_ExpiresOnlyNoResponseTimeoutSubjectsAndNeverRewritesScheduledUtc()
    {
        // Arrange
        // The expiry pass must fail only conversations whose subject flow defines a no-response timeout, must leave
        // no-timeout conversations completely untouched (no status change and, critically, no sentinel written to the
        // user-visible ScheduledUtc), and must never modify ScheduledUtc of either set. The other tests' no-op flow
        // settings skip this path entirely, so it is exercised here with a stub that configures a timeout on one
        // subject type only.
        var databasePath = DatabasePath("processor-expiry");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        const int timeoutCount = 120;
        const int noTimeoutCount = 30;
        const string timeoutSubjectType = "TimeoutSubject";
        const string noTimeoutSubjectType = "NoTimeoutSubject";
        var scheduledUtc = _now.AddHours(-1);

        try
        {
            var timeoutItemIds = new HashSet<string>(StringComparer.Ordinal);
            var noTimeoutItemIds = new HashSet<string>(StringComparer.Ordinal);

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < timeoutCount; i++)
                {
                    timeoutItemIds.Add(await SaveAwaitingSmsActivityAsync(seedSession, timeoutSubjectType, scheduledUtc));
                }

                for (var i = 0; i < noTimeoutCount; i++)
                {
                    noTimeoutItemIds.Add(await SaveAwaitingSmsActivityAsync(seedSession, noTimeoutSubjectType, scheduledUtc));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var flowSettingsService = new StubSubjectFlowSettingsService(
            [
                new SubjectFlowSettings { SubjectContentType = timeoutSubjectType, NoResponseTimeoutInMinutes = 30 },
                new SubjectFlowSettings { SubjectContentType = noTimeoutSubjectType, NoResponseTimeoutInMinutes = null },
            ]);

            await using (var workSession = store.CreateSession())
            {
                var serviceProvider = BuildServiceProvider(workSession, new RecordingSmsProcessor(), flowSettingsService);
                var task = new AutomatedActivitiesProcessorBackgroundTask();

                // Act
                await task.DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);
            }

            // Assert
            await using (var assertSession = store.CreateSession())
            {
                var activities = await assertSession.Query<OmnichannelActivity>(
                        collection: OmnichannelConstants.CollectionName)
                    .ListAsync(TestContext.Current.CancellationToken);

                var byId = activities.ToDictionary(activity => activity.ItemId, StringComparer.Ordinal);

                // Every timeout-configured conversation expired, and its ScheduledUtc (the deadline) is unchanged.
                foreach (var itemId in timeoutItemIds)
                {
                    var activity = byId[itemId];

                    Assert.Equal(ActivityStatus.Failed, activity.Status);
                    Assert.Equal(scheduledUtc, activity.ScheduledUtc);
                }

                // Every no-timeout conversation is completely untouched: still awaiting, and no sentinel schedule.
                foreach (var itemId in noTimeoutItemIds)
                {
                    var activity = byId[itemId];

                    Assert.Equal(ActivityStatus.AwaitingCustomerAnswer, activity.Status);
                    Assert.Equal(scheduledUtc, activity.ScheduledUtc);
                }
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ServiceProvider BuildServiceProvider(ISession session, IOmnichannelProcessor processor)
        => BuildServiceProvider(session, processor, new NoOpSubjectFlowSettingsService());

    private static ServiceProvider BuildServiceProvider(
        ISession session,
        IOmnichannelProcessor processor,
        ISubjectFlowSettingsService subjectFlowSettingsService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(session);
        services.AddSingleton<IClock>(new StubClock(_now));
        services.AddSingleton(processor);
        services.AddSingleton(subjectFlowSettingsService);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services.BuildServiceProvider();
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));
        store.RegisterIndexes([new OmnichannelActivityIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType"),
            collection: OmnichannelConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task<string> SaveAutomatedSmsActivityAsync(ISession session)
    {
        var itemId = IdGenerator.GenerateId();

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = itemId,
                Channel = "SMS",
                ChannelEndpointId = "endpoint",
                ContactContentItemId = IdGenerator.GenerateId(),
                ContactContentType = "Lead",
                SubjectContentType = "LeadFollowUp",
                PreferredDestination = "+15555550100",
                ScheduledUtc = _now.AddHours(-1),
                CreatedUtc = _now.AddHours(-1),
                InteractionType = ActivityInteractionType.Automated,
                Status = ActivityStatus.NotStated,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        return itemId;
    }

    private static async Task<string> SaveAwaitingSmsActivityAsync(
        ISession session,
        string subjectContentType,
        DateTime scheduledUtc)
    {
        var itemId = IdGenerator.GenerateId();

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = itemId,
                Channel = "SMS",
                ChannelEndpointId = "endpoint",
                ContactContentItemId = IdGenerator.GenerateId(),
                ContactContentType = "Lead",
                SubjectContentType = subjectContentType,
                PreferredDestination = "+15555550100",
                ScheduledUtc = scheduledUtc,
                CreatedUtc = _now.AddHours(-1),
                InteractionType = ActivityInteractionType.Automated,
                Status = ActivityStatus.AwaitingCustomerAnswer,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        return itemId;
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            $"automated-activities-processor-{suffix}-{Guid.NewGuid():N}.db");
    }

    private sealed class RecordingSmsProcessor : IOmnichannelProcessor
    {
        private readonly ConcurrentQueue<string> _processed = new();
        private readonly ISet<string> _poisonItemIds;

        public RecordingSmsProcessor(ISet<string> poisonItemIds = null)
        {
            _poisonItemIds = poisonItemIds ?? new HashSet<string>(StringComparer.Ordinal);
        }

        public string Channel => "SMS";

        public ConcurrentQueue<string> ProcessedItemIds => _processed;

        public Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
        {
            _processed.Enqueue(activity.ItemId);

            if (_poisonItemIds.Contains(activity.ItemId))
            {
                throw new InvalidOperationException("Simulated processor failure.");
            }

            // Mimic a successful send: the real SMS processor flips the status so the activity leaves the due query.
            activity.Status = ActivityStatus.AwaitingCustomerAnswer;

            return Task.CompletedTask;
        }
    }

    private sealed class NoOpSubjectFlowSettingsService : ISubjectFlowSettingsService
    {
        public Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectFlowSettings>>([]);

        public Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
            => Task.FromResult<SubjectFlowSettings>(null);

        public Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentTypeDefinition>>([]);

        public bool IsConfigured(SubjectFlowSettings flowSettings)
            => false;
    }

    private sealed class StubSubjectFlowSettingsService : ISubjectFlowSettingsService
    {
        private readonly IReadOnlyList<SubjectFlowSettings> _settings;

        public StubSubjectFlowSettingsService(IReadOnlyList<SubjectFlowSettings> settings)
        {
            _settings = settings;
        }

        public Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);

        public Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
            => Task.FromResult(_settings.FirstOrDefault(settings => string.Equals(settings.SubjectContentType, subjectContentType, StringComparison.Ordinal)));

        public Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentTypeDefinition>>([]);

        public bool IsConfigured(SubjectFlowSettings flowSettings)
            => flowSettings is not null;
    }
}
