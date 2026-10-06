using System.Collections.Concurrent;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
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
    private readonly bool _durableEventHistory;
    private readonly HarnessShared _shared;
    private readonly List<string> _agentIds = [];

    private DialerModeIntegrationHarness(
        IStore store,
        ISession session,
        string databasePath,
        ServiceProvider provider,
        TestClock clock,
        bool durableEventHistory,
        HarnessShared shared)
    {
        _store = store;
        _session = session;
        _databasePath = databasePath;
        _provider = provider;
        _clock = clock;
        _durableEventHistory = durableEventHistory;
        _shared = shared;
    }

    /// <summary>
    /// Gets the doubles every unit of work over the harness database shares, as the tenant's singletons are shared by
    /// every scope: the activities, the provider commands, the provider, and the predictive doubles.
    /// </summary>
    internal HarnessShared Shared => _shared;

    /// <summary>
    /// The virtual campaign queue a predictive harness dials, which over-dialing requires.
    /// </summary>
    public static readonly string CampaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);

    /// <summary>
    /// Gets the queue the pacing cycle dials: the campaign queue in a predictive harness, <see cref="QueueId"/> otherwise.
    /// </summary>
    public string PacingQueueId => _shared.Predictive ? CampaignQueueId : QueueId;

    /// <summary>
    /// Creates an over-dialing Predictive profile that passes every safeguard: an enforced cap of 3%, a target of 2%, the
    /// abandoned-call message on, answering-machine screening off, and the profile registered with the harness's
    /// profile reader so the authorizer, the tracker and the connector read it.
    /// </summary>
    public DialerProfile CreateOverDialProfile(Action<DialerProfile> configure = null)
    {
        var profile = CreateProfile(DialerMode.Predictive);
        profile.PredictivePacingModel = PredictivePacingModel.OverDial;
        profile.EnforceAbandonmentCap = true;
        profile.MaxAbandonmentRatePercent = 3;
        profile.TargetAbandonmentRatePercent = 2;
        profile.AbandonmentSampleFloor = 30;
        profile.AnswerRateSampleFloor = 50;
        profile.MaxLinesPerAgent = 3;
        profile.MaxCallsInFlight = 100;
        profile.SafeHarborEnabled = true;
        profile.SafeHarborMessage = "This call was from {company}. Please call {number}.";
        profile.CallerId = "+15550001111";
        profile.AnsweringMachineDetection = DialerAnsweringMachineDetection.Disabled;
        configure?.Invoke(profile);
        _shared.Profiles.Add(profile);

        return profile;
    }

    /// <summary>
    /// Sets what the statistics report: an answer rate measured from <paramref name="settledAttempts"/> calls, and a
    /// rolling and long-run abandonment rate measured from <paramref name="liveAnswers"/> answers.
    /// </summary>
    public void SeedPacingStats(double answerRate = 0.25, long settledAttempts = 400, long liveAnswers = 100, long abandonedCalls = 0, long complianceAbandonedCalls = 0)
        => _shared.Statistics.Seed(answerRate, settledAttempts, liveAnswers, abandonedCalls, complianceAbandonedCalls);

    /// <summary>
    /// Runs one Predictive cycle through the dialer, commits, and places the dials it staged.
    /// </summary>
    public Task<int> RunPredictiveCycleAsync(DialerProfile profile)
        => RunPacingCycleAsync(profile);

    /// <summary>
    /// A person answers the placed call of the activity: the answer is ingested, committed, and the connect it schedules
    /// after the commit runs.
    /// </summary>
    public Task RaiseHumanAnswerAsync(string activityId, string idempotencySuffix = "connected")
        => RaiseCallStateAsync(activityId, VoiceCallState.Connected, idempotencySuffix);

    /// <summary>
    /// The provider reports a machine answered the screened call of the activity.
    /// </summary>
    public async Task RaiseMachineAnswerAsync(string activityId)
    {
        var interaction = await FindInteractionByActivityAsync(activityId);

        await _provider.GetRequiredService<IProviderVoiceEventService>().IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            State = VoiceCallState.Connected,
            AnswerClassification = AnswerClassification.Machine,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = $"{interaction.ProviderInteractionId}:machine",
        }, TestContext.Current.CancellationToken);

        await DrainAsync();
    }

    /// <summary>
    /// The agent's leg of the call of the activity answers, joining the agent to the person.
    /// </summary>
    public async Task RaiseAgentLegAnsweredAsync(string activityId, string agentLegId = null)
    {
        var interaction = await FindInteractionByActivityAsync(activityId);

        await _provider.GetRequiredService<IContactCenterAgentLegFailureService>().RecordAnsweredAsync(
            ProviderName,
            interaction.ProviderInteractionId,
            agentLegId ?? $"agent-leg-{activityId}",
            TestContext.Current.CancellationToken);

        await DrainAsync();
    }

    /// <summary>
    /// The call of the activity ends, and the routing of the ended call is released as the host's event handler does.
    /// </summary>
    public async Task RaiseCallEndedAsync(string activityId)
    {
        await RaiseCallStateAsync(activityId, VoiceCallState.Ended, "ended");

        var interaction = await FindInteractionByActivityAsync(activityId);
        var synchronization = _provider.GetService<IProviderVoiceOfferSynchronizationService>();

        if (synchronization is not null)
        {
            await synchronization.ReconcileEndedOfferAsync(interaction.ItemId, TestContext.Current.CancellationToken);
            await DrainAsync();
        }
    }

    /// <summary>
    /// Finds the queue item of the activity.
    /// </summary>
    public Task<QueueItem> FindQueueItemAsync(string activityId)
        => _provider.GetRequiredService<IQueueItemManager>().FindByActivityIdAsync(activityId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Counts the calls in flight without an agent.
    /// </summary>
    public Task<int> CountInFlightAsync()
        => _provider.GetRequiredService<IQueueItemStore>().CountDialerInFlightAsync(PacingQueueId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Lists every reservation stored.
    /// </summary>
    public async Task<IReadOnlyCollection<ActivityReservation>> GetReservationsAsync()
        => (await _session.Query<ActivityReservation, ActivityReservationIndex>(collection: ContactCenterStorage.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken)).ToArray();

    public FakeVoiceContactCenterCallRouter Router => (FakeVoiceContactCenterCallRouter)_provider.GetRequiredService<IVoiceContactCenterCallRouter>();

    public IAgentPresenceManager PresenceManager => _provider.GetRequiredService<IAgentPresenceManager>();

    public IDialerService DialerService => _provider.GetRequiredService<IDialerService>();

    public IInteractionManager InteractionManager => _provider.GetRequiredService<IInteractionManager>();

    public IAgentProfileManager AgentManager => _provider.GetRequiredService<IAgentProfileManager>();

    public TestClock Clock => _clock;

    /// <summary>
    /// Gets the harness container, so a test can compose a real service over the same store and doubles.
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// Gets the session every harness service shares.
    /// </summary>
    public ISession Session => _session;

    /// <summary>
    /// Gets every event published, in order, including the agent state audit the recorder writes.
    /// </summary>
    public IReadOnlyList<InteractionEvent> PublishedEvents
        => ((RecordingContactCenterEventPublisher)_provider.GetRequiredService<IContactCenterEventPublisher>()).Events;

    public IReadOnlyList<string> AgentIds => _agentIds;

    /// <summary>
    /// Creates the harness over a fresh temp SQLite database.
    /// </summary>
    /// <param name="busyTimeoutSeconds">
    /// How long a flow waits for SQLite's write lock before failing with "database is locked", or
    /// <see langword="null"/> for the provider default. A short timeout makes a flow that cannot get the lock fail
    /// fast instead of stalling the test.
    /// </param>
    /// <param name="durableEventHistory">
    /// Whether every published event is also written to the durable event history, through the real
    /// <see cref="DefaultContactCenterEventPublisher"/> and <see cref="InteractionEventStore"/>, as the host does. A service
    /// that reads the history back (the stuck-Busy recovery reads each agent's latest state change) then sees exactly
    /// what the pipeline recorded. Off by default, so the history every other flow reads stays empty as it always was.
    /// </param>
    /// <param name="predictive">
    /// Whether the Predictive strategy and everything over-dialing needs are registered: the pacer, the agent connector,
    /// the system-dial authorizer, the real abandonment tracker and policy over settable statistics, a recording
    /// abandoned-call message, a voice provider that joins the agent through a leg of its own, and recording pacing and
    /// deadline schedulers. Off by default, so every other flow is exactly what it always was.
    /// </param>
    public static async Task<DialerModeIntegrationHarness> CreateAsync(int? busyTimeoutSeconds = null, bool durableEventHistory = false, bool predictive = false)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-dialer-integration-{Guid.NewGuid():N}.db");
        var connectionString = busyTimeoutSeconds.HasValue
            ? $"Data Source={databasePath};Pooling=False;Default Timeout={busyTimeoutSeconds.Value}"
            : $"Data Source={databasePath};Pooling=False";
        // Thousands of commits: a journal deleted at every one of them is a "disk I/O error" waiting for whatever
        // opens new files in the temp folder, so the journal is kept and emptied instead.
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite(connectionString);
            configuration.ConnectionFactory = new KeptJournalConnectionFactory(configuration.ConnectionFactory);
        });
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
            new ContactCenterWorkStateIndexProvider(),
            new InteractionIndexProvider(),
            new CallSessionIndexProvider(new ProviderIdentityResolver([])),
            new InteractionEventIndexProvider(),
            new PredictivePacingStateIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);
        await CreateSchemaAsync(store);

        var session = store.CreateSession();
        var clock = new TestClock();
        var shared = new HarnessShared(predictive);
        var provider = BuildServiceProvider(session, clock, CreateAlwaysGrantingLock(), durableEventHistory, shared);

        // Late-bind the harness scope executor and command processor to the built container so their deferred
        // work can resolve the real services.
        ((HarnessScopeExecutor)provider.GetRequiredService<IContactCenterScopeExecutor>()).Bind(provider);

        return new DialerModeIntegrationHarness(store, session, databasePath, provider, clock, durableEventHistory, shared);
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
        agent.AllowedQueueIds = [PacingQueueId];
        agent.AllowedCampaignIds = [CampaignId];
        agent.QueueIds = [PacingQueueId];
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
    public Task SeedQueuedActivityAsync(string activityId, string destination)
        => SeedQueuedActivityAsync(new OmnichannelActivity { ItemId = activityId, PreferredDestination = destination }, PacingQueueId);

    /// <summary>
    /// Seeds a waiting queue item in <paramref name="queueId"/> for <paramref name="activity"/>, which is stored as
    /// the CRM activity unless it is only an id: pass an activity with no status to model one that was deleted.
    /// </summary>
    public async Task<QueueItem> SeedQueuedActivityAsync(OmnichannelActivity activity, string queueId, bool storeActivity = true)
    {
        var activityId = activity.ItemId;

        if (storeActivity)
        {
            _provider.GetRequiredService<InMemoryOmnichannelActivities>().Add(activity);
        }

        var queueItemManager = _provider.GetRequiredService<IQueueItemManager>();
        var workStateService = _provider.GetRequiredService<IContactCenterWorkStateService>();

        var queueItem = await queueItemManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.QueueId = queueId;
        queueItem.ActivityItemId = activityId;
        queueItem.TransitionTo(QueueItemStatus.Waiting);
        queueItem.EnqueuedUtc = _clock.UtcNow;
        await queueItemManager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);

        await workStateService.MutateAsync(activityId, workState =>
            workState.TransitionTo(ActivityAssignmentStatus.Available), TestContext.Current.CancellationToken);

        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return queueItem;
    }

    public static DialerProfile CreateProfile(DialerMode mode, int callsPerAgent = 1)
    {
        return new DialerProfile
        {
            ItemId = "profile-1",
            Name = $"{mode} profile",
            Mode = mode,
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
        var started = await DialerService.RunCycleAsync(profile, PacingQueueId, TestContext.Current.CancellationToken);
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
            .GetWaitingAsync(PacingQueueId, TestContext.Current.CancellationToken);
    }

    public async Task<Interaction> FindInteractionByActivityAsync(string activityId)
    {
        return await _provider.GetRequiredService<IInteractionManager>()
            .FindByActivityIdAsync(activityId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Opens an independent unit of work over the harness database: its own session and its own copy of every real
    /// service, as a webhook delivery processed side by side with another gets its own shell scope. Flows that must
    /// serialize on a call share <paramref name="distributedLock"/>. <paramref name="configure"/> replaces services of
    /// this unit of work only, as a node of its own would run them.
    /// </summary>
    public HarnessFlow OpenFlow(IDistributedLock distributedLock, Action<IServiceCollection> configure = null)
    {
        var session = _store.CreateSession();
        var provider = BuildServiceProvider(session, _clock, distributedLock, _durableEventHistory, _shared, configure);
        var scopeExecutor = (HarnessScopeExecutor)provider.GetRequiredService<IContactCenterScopeExecutor>();
        scopeExecutor.Bind(provider);

        // A predictive flow retries a lost claim in a fresh unit of work, as the host does.
        if (_shared.Predictive)
        {
            scopeExecutor.UseFreshScopes(() => OpenFlow(distributedLock, configure));
        }

        return new HarnessFlow(session, provider);
    }

    /// <summary>
    /// Commits the shared session and runs the work deferred until after commit, as a shell scope would.
    /// </summary>
    public Task CommitAsync() => DrainAsync();

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

    private static ServiceProvider BuildServiceProvider(ISession session, TestClock clock, IDistributedLock distributedLock, bool durableEventHistory, HarnessShared shared, Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(session);
        services.AddSingleton(clock);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(distributedLock);

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

        // CRM activities (in-memory), shared by every unit of work over the database.
        services.AddSingleton(shared.Activities);
        services.AddSingleton(sp => sp.GetRequiredService<InMemoryOmnichannelActivities>().BuildManager());
        services.AddSingleton<IContactCenterActivityWriter, ContactCenterActivityWriter>();

        // Harness doubles for the seams outside the agent-state machine.
        services.AddSingleton(shared.Router);
        services.AddSingleton<IVoiceContactCenterCallRouter>(sp => sp.GetRequiredService<FakeVoiceContactCenterCallRouter>());
        services.AddSingleton(shared.Commands);
        services.AddSingleton<IProviderCommandStateService>(sp => sp.GetRequiredService<InMemoryProviderCommandStateService>());
        services.AddSingleton<HarnessScopeExecutor>();
        services.AddSingleton<IContactCenterScopeExecutor>(sp => sp.GetRequiredService<HarnessScopeExecutor>());

        if (durableEventHistory)
        {
            // The host's publisher over the real event store: each event is written to the history, then handed to the
            // outbox, whose handler dispatch has its own tests and is not what a flow here asserts.
            services.AddSingleton<IInteractionEventUpcastService>(new DefaultInteractionEventUpcastService([]));
            services.AddSingleton<IInteractionEventStore>(sp => new InteractionEventStore(session, sp.GetRequiredService<IInteractionEventUpcastService>()));
            services.AddSingleton(Mock.Of<IContactCenterOutbox>());
            services.AddSingleton<ContactCenterEventDispatchContext>();
            services.AddSingleton<DefaultContactCenterEventPublisher>();
            services.AddSingleton<IContactCenterEventPublisher>(sp => new RecordingContactCenterEventPublisher(sp.GetRequiredService<DefaultContactCenterEventPublisher>()));
        }
        else
        {
            services.AddSingleton<IContactCenterEventPublisher>(new RecordingContactCenterEventPublisher());
            services.AddSingleton(CreateEmptyInteractionEventStore());
        }

        services.AddSingleton<IContactCenterAuditRecorder, ContactCenterAuditRecorder>();
        services.AddSingleton<IAgentAvailabilityService, HarnessAvailabilityService>();
        services.AddSingleton(CreateEligibilityService());
        services.AddSingleton(CreateFeatureWorkManager());
        services.AddSingleton(Mock.Of<IActivityQueueManager>());
        services.AddSingleton(Mock.Of<IActivityQueueService>());
        services.AddSingleton(shared.Predictive ? CreateAgentLegVoiceProviderResolver() : Mock.Of<IContactCenterVoiceProviderResolver>());
        services.AddSingleton(Mock.Of<ITelephonyProviderResolver>());
        services.AddSingleton<IProviderIdentityResolver>(new ProviderIdentityResolver([]));
        // As registered in the host, the gate releases the scope's own open work before it waits on a held call.
        services.AddSingleton<IVoiceIngressGate>(sp => new VoiceIngressGate(sp.GetRequiredService<IDistributedLock>(), session));

        // Real agent-state pipeline. Entitlements are not enforced in the harness, so agents may sign in to any
        // queue or campaign (the permissive default policy).
        services.AddSingleton<IAgentEntitlementPolicy, PermissiveAgentEntitlementPolicy>();
        services.AddSingleton<IDialDestinationPolicy>(DialDestinationPolicyFactory.Create());
        services.AddSingleton<IAgentWorkStateHealingService>(new NoAgentWorkStateHealingService());
        services.AddSingleton<IContactCenterAuditRecorder, ContactCenterAuditRecorder>();
        services.AddSingleton(Mock.Of<IAgentStateReasonCodeManager>());
        services.AddSingleton<IAgentStateTransitionService, AgentStateTransitionService>();
        services.AddSingleton<IAgentPresenceManager, AgentPresenceManagerService>();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();
        if (shared.Predictive)
        {
            AddPredictive(services, shared);
        }
        else
        {
            services.AddSingleton(Mock.Of<IDialerAbandonmentTracker>());
        }

        services.AddSingleton<IProviderVoiceEventService, ProviderVoiceEventService>();
        services.AddSingleton(Mock.Of<ITelephonyService>());
        services.AddSingleton<IContactCenterAgentLegFailureService, ContactCenterAgentLegFailureService>();
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

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    // Everything over-dialing adds to the pipeline. The pacer, the connector, the claim, the authorizer, the tracker
    // and the policy are the real ones; the statistics, the abandoned-call message, the routing order and the two
    // in-process schedulers are doubles a test reads or drives.
    private static void AddPredictive(ServiceCollection services, HarnessShared shared)
    {
        services.AddSingleton(shared.Profiles);
        services.AddSingleton<IDialerProfileReader>(shared.Profiles);
        services.AddSingleton(shared.Statistics);
        services.AddSingleton<IDialerPacingStatisticsProvider>(shared.Statistics);
        services.AddSingleton<IDialerAbandonmentStatisticsProvider>(shared.Statistics);
        services.AddSingleton<IDialerAbandonmentPolicyService, DefaultDialerAbandonmentPolicyService>();
        services.AddSingleton(shared.Treatment);
        services.AddSingleton<IQueueTreatmentProvider>(shared.Treatment);
        services.AddSingleton(Mock.Of<ISiteService>());
        services.AddSingleton(Mock.Of<IShellHost>());
        services.AddSingleton(new ShellSettings());
        services.AddSingleton<IDialerAbandonmentTracker, DialerAbandonmentTracker>();

        services.AddSingleton(shared.Deadlines);
        services.AddSingleton<IContactCenterDeadlineScheduler>(shared.Deadlines);
        services.AddSingleton(shared.Pacing);
        services.AddSingleton<IPredictivePacingScheduler>(shared.Pacing);

        services.AddSingleton<IActivityRoutingService, HarnessRoutingService>();
        services.AddSingleton(CreateNoWithdrawalService());
        services.AddSingleton(CreateOpenDialerWorkGate());
        services.AddSingleton<IPredictivePacingStateStore, PredictivePacingStateStore>();
        services.AddSingleton<IPredictiveSystemDialAuthorizer, PredictiveSystemDialAuthorizer>();
        services.AddSingleton<IPredictiveOverDialPacer, PredictiveOverDialPacer>();
        services.AddSingleton<IPredictiveAgentConnector, PredictiveAgentConnector>();
        services.AddSingleton<IDialerStrategy, PredictiveDialerStrategy>();

        // What a call's end runs through the event handlers in the host: releasing the routing of a call that ended.
        services.AddSingleton(sp => new Lazy<IContactCenterAuditRecorder>(sp.GetRequiredService<IContactCenterAuditRecorder>));
        services.AddSingleton<IProviderVoiceOfferSynchronizationService, ProviderVoiceOfferSynchronizationService>();
    }

    private static IContactCenterVoiceProviderResolver CreateAgentLegVoiceProviderResolver()
    {
        // A provider like Telnyx: the customer is dialed first, and the agent joins through a leg of their own once a
        // person answers, connected by the Answer command.
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.As<IContactCenterVoiceCallControlProvider>();
        provider.SetupGet(value => value.TechnicalName).Returns(ProviderName);
        provider.SetupGet(value => value.DeliveryModel).Returns(VoiceProviderDeliveryModel.ServerSideAcd);
        provider.SetupGet(value => value.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.AgentConnect | ContactCenterVoiceProviderCapabilities.DialerDial);

        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(value => value.Get(It.IsAny<string>())).Returns(provider.Object);

        return resolver.Object;
    }

    private static IQueuedWorkWithdrawalService CreateNoWithdrawalService()
    {
        var mock = new Mock<IQueuedWorkWithdrawalService>();
        mock.Setup(service => service.TryWithdrawUnroutableAsync(It.IsAny<QueueItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return mock.Object;
    }

    private static IQueuedDialerWorkGate CreateOpenDialerWorkGate()
    {
        var mock = new Mock<IQueuedDialerWorkGate>();
        mock.Setup(gate => gate.TryHoldBackAsync(It.IsAny<QueueItem>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return mock.Object;
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

        await builder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<string>("IdempotencyClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName);

        await builder.CreateMapIndexTableAsync<PredictivePacingStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.NotNull().Unique().WithLength(ContactCenterStorage.QueueIdLength)),
            collection: ContactCenterStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
