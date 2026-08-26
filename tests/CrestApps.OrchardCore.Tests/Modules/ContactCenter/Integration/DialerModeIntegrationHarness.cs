using System.Collections.Concurrent;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A SQLite-backed integration harness that drives the real Contact Center dialing pipeline
/// (<see cref="DialerService"/> and its strategies, <see cref="ActivityReservationService"/>,
/// <see cref="AgentPresenceManagerService"/>, and <see cref="ProviderVoiceEventService"/>) so a test can assert
/// the agent-state lifecycle for each dialing mode end to end.
/// </summary>
/// <remarks>
/// Only the outbound provider media call (<see cref="IVoiceContactCenterCallRouter"/>) is faked; every service
/// that owns agent state runs for real against a temp SQLite store with a local distributed lock. The durable
/// provider-command transport (claim/lease store and <c>ProviderCommandProcessor</c>) — which has its own tests —
/// is replaced by an in-memory dispatcher that runs the real <see cref="DialProviderCommandTypeExecutor"/>, and
/// the agent-selection policy (<c>ActivityRoutingService</c>) is replaced by a thin harness picker that still
/// calls the real reservation service. Time is controlled by a fixed, advanceable clock.
/// </remarks>
internal sealed class DialerModeIntegrationHarness : IAsyncDisposable
{
    public const string ProviderName = "FakeVoice";
    public const string QueueId = "queue-1";
    public const string CampaignId = "campaign-1";

    private readonly IStore _store;
    private readonly ISession _session;
    private readonly string _databasePath;
    private readonly ServiceProvider _provider;
    private readonly TestClock _clock;
    private readonly List<string> _agentIds = [];

    private DialerModeIntegrationHarness(
        IStore store,
        ISession session,
        string databasePath,
        ServiceProvider provider,
        TestClock clock)
    {
        _store = store;
        _session = session;
        _databasePath = databasePath;
        _provider = provider;
        _clock = clock;
    }

    public FakeVoiceContactCenterCallRouter Router => (FakeVoiceContactCenterCallRouter)_provider.GetRequiredService<IVoiceContactCenterCallRouter>();

    public IAgentPresenceManager PresenceManager => _provider.GetRequiredService<IAgentPresenceManager>();

    public IDialerService DialerService => _provider.GetRequiredService<IDialerService>();

    public IInteractionManager InteractionManager => _provider.GetRequiredService<IInteractionManager>();

    public IAgentProfileManager AgentManager => _provider.GetRequiredService<IAgentProfileManager>();

    public TestClock Clock => _clock;

    public IReadOnlyList<string> AgentIds => _agentIds;

    public static async Task<DialerModeIntegrationHarness> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-dialer-integration-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
            new ContactCenterWorkStateIndexProvider(),
            new InteractionIndexProvider(),
            new CallSessionIndexProvider(new ProviderIdentityResolver([])),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);
        await CreateSchemaAsync(store);

        var session = store.CreateSession();
        var clock = new TestClock();
        var provider = BuildServiceProvider(session, clock);

        // Late-bind the harness scope executor and command processor to the built container so their deferred
        // work can resolve the real services.
        ((HarnessScopeExecutor)provider.GetRequiredService<IContactCenterScopeExecutor>()).Bind(provider);

        return new DialerModeIntegrationHarness(store, session, databasePath, provider, clock);
    }

    /// <summary>
    /// Seeds a signed-in, Available agent entitled to the campaign queue.
    /// </summary>
    public async Task<AgentProfile> SignInAgentAsync(string agentId, string userId)
    {
        var manager = _provider.GetRequiredService<IAgentProfileManager>();
        var agent = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        agent.ItemId = agentId;
        agent.UserId = userId;
        agent.UserName = userId;
        agent.Name = userId;
        agent.AllowedQueueIds = [QueueId];
        agent.AllowedCampaignIds = [CampaignId];
        agent.QueueIds = [QueueId];
        agent.CampaignIds = [CampaignId];
        agent.MaxConcurrentInteractions = 1;
        agent.PresenceStatus = AgentPresenceStatus.Available;
        await manager.CreateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);

        _provider.GetRequiredService<HarnessAssignmentService>().RegisterAgent(agentId);
        _agentIds.Add(agentId);

        return agent;
    }

    /// <summary>
    /// Signs in <paramref name="count"/> Available agents (<c>agent-1</c>..<c>agent-N</c>).
    /// </summary>
    public async Task SignInAgentsAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await SignInAgentAsync($"agent-{i}", $"user-{i}");
        }
    }

    /// <summary>
    /// Seeds <paramref name="count"/> queued campaign activities (<c>activity-1</c>..<c>activity-N</c>) each with a
    /// distinct destination.
    /// </summary>
    public async Task SeedQueuedActivitiesAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await SeedQueuedActivityAsync($"activity-{i}", $"+1555{i:D7}");
        }
    }

    /// <summary>
    /// Seeds a queued campaign activity (a waiting queue item plus its CRM activity) that a pacing cycle can dial.
    /// </summary>
    public async Task SeedQueuedActivityAsync(string activityId, string destination)
    {
        _provider.GetRequiredService<InMemoryOmnichannelActivities>().Add(new OmnichannelActivity
        {
            ItemId = activityId,
            PreferredDestination = destination,
        });

        var queueItemManager = _provider.GetRequiredService<IQueueItemManager>();
        var workStateService = _provider.GetRequiredService<IContactCenterWorkStateService>();

        var queueItem = await queueItemManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.QueueId = QueueId;
        queueItem.ActivityItemId = activityId;
        queueItem.TransitionTo(QueueItemStatus.Waiting);
        queueItem.EnqueuedUtc = _clock.UtcNow;
        await queueItemManager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);

        await workStateService.MutateAsync(activityId, workState =>
            workState.TransitionTo(ActivityAssignmentStatus.Available), TestContext.Current.CancellationToken);

        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public DialerProfile CreateProfile(DialerMode mode, int callsPerAgent = 1)
    {
        return new DialerProfile
        {
            ItemId = "profile-1",
            Name = $"{mode} profile",
            Mode = mode,
            QueueId = QueueId,
            CampaignId = CampaignId,
            ProviderName = ProviderName,
            CallsPerAgent = callsPerAgent,
            Enabled = true,
            RespectDoNotCall = false,
            EnforceCallingWindow = false,
            EnforceAbandonmentCap = false,
        };
    }

    /// <summary>
    /// Runs one pacing cycle for the profile, commits the routing transaction, then drains the deferred
    /// after-commit work (the provider dial dispatch and CRM activity writes) exactly as the real shell scope
    /// would after commit.
    /// </summary>
    public async Task<int> RunPacingCycleAsync(DialerProfile profile)
    {
        var started = await DialerService.RunCycleAsync(profile, TestContext.Current.CancellationToken);
        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);
        await DrainAsync();

        return started;
    }

    /// <summary>
    /// Feeds a provider voice event for the placed call bound to <paramref name="activityId"/> through the real
    /// <see cref="ProviderVoiceEventService"/>.
    /// </summary>
    public async Task RaiseCallStateAsync(string activityId, VoiceCallState state, string idempotencySuffix)
    {
        var interaction = await FindInteractionByActivityAsync(activityId);

        Assert.NotNull(interaction);
        Assert.False(string.IsNullOrEmpty(interaction.ProviderInteractionId), "The dial was never placed, so no provider call id exists to drive.");

        await _provider.GetRequiredService<IProviderVoiceEventService>().IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            State = state,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = $"{interaction.ProviderInteractionId}:{idempotencySuffix}",
        }, TestContext.Current.CancellationToken);

        await DrainAsync();
    }

    /// <summary>
    /// Answers then hangs up the placed call for the activity, driving the full connected→ended lifecycle.
    /// </summary>
    public async Task AnswerAndHangupAsync(string activityId)
    {
        await RaiseCallStateAsync(activityId, VoiceCallState.Connected, "connected");
        _clock.Advance(TimeSpan.FromSeconds(30));
        await RaiseCallStateAsync(activityId, VoiceCallState.Ended, "ended");
    }

    /// <summary>
    /// Completes the agent's work as a disposition would, returning them to their ready state.
    /// </summary>
    public async Task DispositionAsync(string agentId)
    {
        await PresenceManager.CompleteWorkAsync(agentId, TestContext.Current.CancellationToken);
        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async Task<AgentPresenceStatus> GetPresenceAsync(string agentId)
    {
        var agent = await AgentManager.FindByIdAsync(agentId, TestContext.Current.CancellationToken);

        return agent!.PresenceStatus;
    }

    public async Task<IReadOnlyCollection<QueueItem>> GetWaitingQueueItemsAsync()
    {
        return await _provider.GetRequiredService<IQueueItemManager>()
            .GetWaitingAsync(QueueId, TestContext.Current.CancellationToken);
    }

    public async Task<Interaction> FindInteractionByActivityAsync(string activityId)
    {
        return await _provider.GetRequiredService<IInteractionManager>()
            .FindByActivityIdAsync(activityId, TestContext.Current.CancellationToken);
    }

    private async Task DrainAsync()
    {
        var scopeExecutor = (HarnessScopeExecutor)_provider.GetRequiredService<IContactCenterScopeExecutor>();

        while (scopeExecutor.TryDequeue(out var work))
        {
            await work();
        }

        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _session.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
    }

    private static ServiceProvider BuildServiceProvider(ISession session, TestClock clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(session);
        services.AddSingleton(clock);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(CreateAlwaysGrantingLock());

        // Stores + catalog managers over the shared session.
        services.AddSingleton<IInteractionStore>(new InteractionStore(session));
        services.AddSingleton<IInteractionManager>(sp => new InteractionManager(
            sp.GetRequiredService<IInteractionStore>(), [], NullLogger<CatalogManager<Interaction>>.Instance));

        services.AddSingleton<ICallSessionStore>(new CallSessionStore(session));
        services.AddSingleton<ICallSessionManager>(sp => new CallSessionManager(
            sp.GetRequiredService<ICallSessionStore>(), [], NullLogger<CatalogManager<CallSession>>.Instance));

        services.AddSingleton<IAgentProfileStore>(new AgentProfileStore(session));
        services.AddSingleton<IAgentProfileManager>(sp => new AgentProfileManager(
            sp.GetRequiredService<IAgentProfileStore>(), [], NullLogger<CatalogManager<AgentProfile>>.Instance));

        services.AddSingleton<IQueueItemStore>(new QueueItemStore(session));
        services.AddSingleton<IQueueItemManager>(sp => new QueueItemManager(
            sp.GetRequiredService<IQueueItemStore>(), [], NullLogger<CatalogManager<QueueItem>>.Instance));

        services.AddSingleton<IActivityReservationStore>(new ActivityReservationStore(session));
        services.AddSingleton<IActivityReservationManager>(sp => new ActivityReservationManager(
            sp.GetRequiredService<IActivityReservationStore>(), [], NullLogger<CatalogManager<ActivityReservation>>.Instance));

        services.AddSingleton<IContactCenterWorkStateStore>(new ContactCenterWorkStateStore(session));
        services.AddSingleton<IContactCenterWorkStateManager>(sp => new ContactCenterWorkStateManager(
            sp.GetRequiredService<IContactCenterWorkStateStore>(), [], NullLogger<CatalogManager<ContactCenterWorkState>>.Instance));
        services.AddSingleton<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>();
        services.AddSingleton<IContactCenterWorkStateService, ContactCenterWorkStateService>();

        // CRM activities (in-memory).
        services.AddSingleton<InMemoryOmnichannelActivities>();
        services.AddSingleton(sp => sp.GetRequiredService<InMemoryOmnichannelActivities>().BuildManager());
        services.AddSingleton<IContactCenterActivityWriter, ContactCenterActivityWriter>();

        // Harness doubles for the seams outside the agent-state machine.
        services.AddSingleton<FakeVoiceContactCenterCallRouter>();
        services.AddSingleton<IVoiceContactCenterCallRouter>(sp => sp.GetRequiredService<FakeVoiceContactCenterCallRouter>());
        services.AddSingleton<InMemoryProviderCommandStateService>();
        services.AddSingleton<IProviderCommandStateService>(sp => sp.GetRequiredService<InMemoryProviderCommandStateService>());
        services.AddSingleton<HarnessScopeExecutor>();
        services.AddSingleton<IContactCenterScopeExecutor>(sp => sp.GetRequiredService<HarnessScopeExecutor>());
        services.AddSingleton<IContactCenterEventPublisher>(new RecordingContactCenterEventPublisher());
        services.AddSingleton<IAgentAvailabilityService, HarnessAvailabilityService>();
        services.AddSingleton(CreateEligibilityService());
        services.AddSingleton(CreateFeatureWorkManager());
        services.AddSingleton(Mock.Of<IActivityQueueManager>());
        services.AddSingleton(Mock.Of<IActivityQueueService>());
        services.AddSingleton(Mock.Of<IContactCenterVoiceProviderResolver>());
        services.AddSingleton(Mock.Of<ITelephonyProviderResolver>());
        services.AddSingleton(CreateEmptyInteractionEventStore());
        services.AddSingleton<IProviderIdentityResolver>(new ProviderIdentityResolver([]));
        services.AddSingleton<IVoiceIngressGate>(sp => new VoiceIngressGate(sp.GetRequiredService<IDistributedLock>()));

        // Real agent-state pipeline.
        services.AddSingleton<IAgentPresenceManager, AgentPresenceManagerService>();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();
        services.AddSingleton<IProviderVoiceEventService, ProviderVoiceEventService>();
        services.AddSingleton<DialProviderCommandTypeExecutor>();
        services.AddSingleton<HarnessProviderCommandProcessor>();
        services.AddSingleton<IProviderCommandProcessor>(sp => sp.GetRequiredService<HarnessProviderCommandProcessor>());

        // Real dialing pipeline.
        services.AddSingleton<IDialerAttemptCompensationService, DialerAttemptCompensationService>();
        services.AddSingleton<IDialerAttemptService, DialerAttemptService>();
        services.AddSingleton<IDialerStrategy, PowerDialerStrategy>();
        services.AddSingleton<IDialerStrategy, ProgressiveDialerStrategy>();
        services.AddSingleton<IDialerStrategyResolver, DialerStrategyResolver>();
        services.AddSingleton<IDialerService, DialerService>();
        services.AddSingleton<HarnessAssignmentService>();
        services.AddSingleton<IActivityAssignmentService>(sp => sp.GetRequiredService<HarnessAssignmentService>());

        return services.BuildServiceProvider();
    }

    private static IDialerEligibilityService CreateEligibilityService()
    {
        var mock = new Mock<IDialerEligibilityService>();
        mock.Setup(service => service.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());

        return mock.Object;
    }

    private static IContactCenterFeatureWorkManager CreateFeatureWorkManager()
    {
        var lease = Mock.Of<IContactCenterFeatureWorkLease>();
        var mock = new Mock<IContactCenterFeatureWorkManager>();
        mock.Setup(manager => manager.TryEnter(It.IsAny<string>())).Returns(lease);

        return mock.Object;
    }

    private static IInteractionEventStore CreateEmptyInteractionEventStore()
    {
        var mock = new Mock<IInteractionEventStore>();
        mock.Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(store => store.GetByInteractionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return mock.Object;
    }

    private static IDistributedLock CreateAlwaysGrantingLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private static async Task CreateSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var builder = new SchemaBuilder(store.Configuration, transaction);

        await builder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull())
            .Column<DateTime>("DequeuedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("Direction", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc")
            .Column<DateTime>("WrapUpStartedUtc")
            .Column<DateTime>("WrapUpCompletedUtc")
            .Column<bool>("RecordingLegalHold")
            .Column<RecordingState>("RecordingState")
            .Column<DateTime>("RecordingPausedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("ProviderCallClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(261))
            .Column<string>("State", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentSessionId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("MediaTopologyId", column => column.WithLength(128))
            .Column<string>("ConferenceId", column => column.WithLength(128))
            .Column<string>("RecordingId", column => column.WithLength(128))
            .Column<string>("SupervisorAgentId", column => column.WithLength(26))
            .Column<string>("SupervisorLegId", column => column.WithLength(128))
            .Column<string>("DurableCommandId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
