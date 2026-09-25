using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using ContactCenterTransferRequest = CrestApps.OrchardCore.ContactCenter.Core.Models.TransferRequest;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A live, answered call on the dialer harness's real SQLite store, with every Contact Center service a transfer runs
/// through composed for real: the transfer and consult services, the offer, queue, reservation and presence services,
/// and provider-truth ingestion. Only the provider is faked, at the provider seam, and it records what it was asked.
/// </summary>
internal sealed class TransferIntegrationFixture : IAsyncDisposable
{
    public const string ActivityId = "activity-1";
    public const string AgentA = "agent-1";
    public const string UserA = "user-1";
    public const string AgentB = "agent-2";
    public const string UserB = "user-2";
    public const string AgentLegA = "agent-leg-a";
    public const string SupportQueueId = "queue-support";
    public const string OwnNumber = "+15550001111";

    private readonly IContactCenterVoiceProvider _providerOverride;

    private TransferIntegrationFixture(DialerModeIntegrationHarness harness, IContactCenterVoiceProvider providerOverride)
    {
        Harness = harness;
        _providerOverride = providerOverride;
    }

    public DialerModeIntegrationHarness Harness { get; }

    public FakeTransferVoiceProvider Provider { get; } = new();

    public RecordingQueueTreatmentProvider Treatment { get; } = new();

    public ContactCenterExternalTransferSettings ExternalSettings { get; } = new();

    public string CallId { get; private set; }

    public IContactCenterTransferService TransferService { get; private set; }

    public IWarmTransferService WarmTransfers { get; private set; }

    public IConsultLegEventSink ConsultLegs { get; private set; }

    public IContactCenterEventHandler CallEndedHandler { get; private set; }

    public IContactCenterEventHandler OfferReconciliationHandler { get; private set; }

    public IContactCenterCallCommandService CallCommands { get; private set; }

    public IActivityReservationManager Reservations => Harness.Services.GetRequiredService<IActivityReservationManager>();

    public IQueueItemManager QueueItems => Harness.Services.GetRequiredService<IQueueItemManager>();

    public ICallSessionManager Sessions => Harness.Services.GetRequiredService<ICallSessionManager>();

    public IReadOnlyList<InteractionEvent> Events => Harness.PublishedEvents;

    /// <summary>
    /// Creates the fixture: agent A on an answered, queue-routed call with their leg joined, and agent B signed in
    /// and Available.
    /// </summary>
    /// <param name="provider">A real provider to run the transfer against instead of the recording fake.</param>
    public static async Task<TransferIntegrationFixture> CreateAsync(IContactCenterVoiceProvider provider = null)
    {
        var harness = await DialerModeIntegrationHarness.CreateAsync();
        var fixture = new TransferIntegrationFixture(harness, provider);

        await harness.SignInAgentAsync(AgentA, UserA);
        await harness.SeedQueuedActivityAsync(ActivityId, "+15557000001");
        await harness.RunPacingCycleAsync(DialerModeIntegrationHarness.CreateProfile(DialerMode.Power));
        await harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Connected, "connected");

        fixture.CallId = (await harness.FindInteractionByActivityAsync(ActivityId)).ProviderInteractionId;

        Assert.True(await harness.Services.GetRequiredService<IContactCenterAgentLegFailureService>().RecordAnsweredAsync(
            DialerModeIntegrationHarness.ProviderName,
            fixture.CallId,
            AgentLegA,
            TestContext.Current.CancellationToken));
        await harness.CommitAsync();

        // B signs in only now, so the dial above went to A.
        await harness.SignInAgentAsync(AgentB, UserB);
        harness.Clock.Advance(TimeSpan.FromSeconds(30));

        fixture.Compose();

        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentB));

        return fixture;
    }

    public static ClaimsPrincipal Principal(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    public static ContactCenterTransferRequest BlindRequest(InteractionTransferTargetType targetType, string targetId, string interactionId)
        => new()
        {
            InteractionId = interactionId,
            Type = InteractionTransferType.Blind,
            TargetType = targetType,
            TargetId = targetId,
            InitiatedByUserId = UserA,
            Principal = Principal(UserA),
        };

    public async Task<Interaction> FindInteractionAsync()
        => await Harness.FindInteractionByActivityAsync(ActivityId);

    public async Task<CallSession> FindSessionAsync()
        => await Sessions.FindByInteractionIdAsync((await FindInteractionAsync()).ItemId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Feeds the customer's leg ending through provider truth, then delivers the resulting call-ended event to the
    /// transfer handler and to the offer reconciliation the way the outbox would.
    /// </summary>
    public async Task CallerHangsUpAsync()
    {
        await Harness.RaiseCallStateAsync(ActivityId, VoiceCallState.Ended, "caller-hung-up");

        foreach (var ended in Events.Where(e => e.EventType == ContactCenterConstants.Events.CallEnded).ToArray())
        {
            await CallEndedHandler.HandleAsync(ended, TestContext.Current.CancellationToken);
            await OfferReconciliationHandler.HandleAsync(ended, TestContext.Current.CancellationToken);
        }

        await Harness.CommitAsync();
    }

    public ValueTask DisposeAsync() => Harness.DisposeAsync();

    private void Compose()
    {
        var services = Harness.Services;
        var clock = services.GetRequiredService<IClock>();

        var queueManager = new Mock<IActivityQueueManager>();
        var queues = new Dictionary<string, ActivityQueue>(StringComparer.Ordinal)
        {
            [DialerModeIntegrationHarness.QueueId] = new ActivityQueue
            {
                ItemId = DialerModeIntegrationHarness.QueueId,
                Name = "Sales",
                Enabled = true,
                Treatment = new QueueTreatmentSettings { HoldMusicMediaId = "https://media.example.test/hold.mp3" },
            },
            [SupportQueueId] = new ActivityQueue
            {
                ItemId = SupportQueueId,
                Name = "Support",
                Enabled = true,
                DefaultPriority = InteractionPriority.Normal,
                Treatment = new QueueTreatmentSettings { HoldMusicMediaId = "https://media.example.test/support.mp3" },
            },
        };
        queueManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(id is not null && queues.TryGetValue(id, out var queue) ? queue : null));
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues.Values.ToArray());

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours
            .Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var providerResolver = new Mock<IContactCenterVoiceProviderResolver>();
        var provider = _providerOverride ?? Provider;
        providerResolver.Setup(resolver => resolver.Get(It.IsAny<string>())).Returns(provider);
        providerResolver.Setup(resolver => resolver.Get()).Returns(provider);

        var availability = new HarnessAvailabilityService(Harness.AgentManager);
        var treatment = new QueueTreatmentService(
            services.GetRequiredService<IQueueItemManager>(),
            services.GetRequiredService<IInteractionManager>(),
            Treatment,
            availability,
            clock,
            NullLogger<QueueTreatmentService>.Instance);

        var queueService = ActivatorUtilities.CreateInstance<ActivityQueueService>(services, queueManager.Object, businessHours.Object, (IQueueTreatmentProvider)Treatment);
        var reservationService = ActivatorUtilities.CreateInstance<ActivityReservationService>(services, queueManager.Object, (IActivityQueueService)queueService, (IAgentAvailabilityService)availability);
        var withdrawalService = ActivatorUtilities.CreateInstance<QueuedWorkWithdrawalService>(services, (IActivityQueueService)queueService, (IActivityReservationService)reservationService);
        var routingService = new ActivityRoutingService([new LongestIdleRoutingStrategy()], clock);
        var assignmentService = ActivatorUtilities.CreateInstance<ActivityAssignmentService>(
            services,
            (IActivityQueueManager)queueManager.Object,
            (IActivityReservationService)reservationService,
            (IQueuedWorkWithdrawalService)withdrawalService,
            (IActivityRoutingService)routingService,
            businessHours.Object,
            new DirectOrQueueAvailability(Harness, availability));
        var offerService = ActivatorUtilities.CreateInstance<VoiceQueueOfferService>(
            services,
            (IActivityAssignmentService)assignmentService,
            (IActivityReservationService)reservationService,
            (IActivityReservationReclaimer)reservationService,
            Mock.Of<IProviderVoiceOfferSynchronizationService>());

        var agentRelease = new TransferAgentReleaseService(
            services.GetRequiredService<IInteractionManager>(),
            services.GetRequiredService<ICallSessionManager>(),
            services.GetRequiredService<IAgentPresenceManager>(),
            providerResolver.Object,
            NullLogger<TransferAgentReleaseService>.Instance);

        var router = ActivatorUtilities.CreateInstance<TransferredCallRouter>(
            services,
            (IActivityQueueManager)queueManager.Object,
            (IAgentAvailabilityService)availability,
            (IActivityReservationService)reservationService,
            (IActivityQueueService)queueService,
            (IVoiceQueueOfferService)offerService,
            (IQueueTreatmentService)treatment,
            (ITransferAgentReleaseService)agentRelease);

        var ownNumbers = new Mock<IContactCenterOwnNumberSource>();
        ownNumbers.Setup(source => source.GetOwnNumbersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([OwnNumber]);

        var destinationResolver = new TransferDestinationResolver(
            new AllowingAuthorizationService(),
            Harness.AgentManager,
            queueManager.Object,
            SiteServiceFactory.Create(ExternalSettings),
            DialDestinationPolicyFactory.Create(),
            [ownNumbers.Object]);

        var supervisors = new Mock<ISupervisorQueueAuthorizationService>();
        var callControl = new CallControlAuthorizationService(
            Harness.AgentManager,
            services.GetRequiredService<ICallSessionManager>(),
            services.GetRequiredService<IInteractionManager>(),
            supervisors.Object);

        TransferService = new ContactCenterTransferService(
            services.GetRequiredService<IInteractionManager>(),
            providerResolver.Object,
            callControl,
            destinationResolver,
            router,
            agentRelease,
            services.GetRequiredService<IProviderVoiceEventService>(),
            services.GetRequiredService<IContactCenterEventPublisher>(),
            new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
            Harness.Session,
            clock);

        var consults = new ConsultTransferService(
            services.GetRequiredService<ICallSessionManager>(),
            providerResolver.Object,
            services.GetRequiredService<IContactCenterAuditRecorder>(),
            clock,
            NullLogger<ConsultTransferService>.Instance);

        WarmTransfers = new WarmTransferService(
            services.GetRequiredService<IInteractionManager>(),
            services.GetRequiredService<ICallSessionManager>(),
            callControl,
            destinationResolver,
            Harness.AgentManager,
            availability,
            queueManager.Object,
            Mock.Of<IVoiceMediaItemManager>(),
            consults,
            agentRelease,
            services.GetRequiredService<IAgentPresenceManager>(),
            services.GetRequiredService<IProviderVoiceEventService>(),
            services.GetRequiredService<IContactCenterEventPublisher>(),
            Harness.Session,
            clock);

        ConsultLegs = new ConsultLegEventSink(
            services.GetRequiredService<ICallSessionManager>(),
            services.GetRequiredService<IInteractionManager>(),
            consults,
            services.GetRequiredService<IAgentPresenceManager>(),
            services.GetRequiredService<IContactCenterAgentLegFailureService>(),
            Harness.Session,
            clock);

        CallEndedHandler = new ContactCenterTransferCallEndedHandler(
            services.GetRequiredService<ICallSessionManager>(),
            services.GetRequiredService<IInteractionManager>(),
            new Lazy<IConsultTransferService>(() => consults),
            new Lazy<IAgentPresenceManager>(services.GetRequiredService<IAgentPresenceManager>),
            clock);

        OfferReconciliationHandler = new ContactCenterVoiceOfferReconciliationHandler(
            ActivatorUtilities.CreateInstance<ProviderVoiceOfferSynchronizationService>(
                services,
                new Lazy<IContactCenterAuditRecorder>(services.GetRequiredService<IContactCenterAuditRecorder>)));

        var preDial = new Mock<IAgentPreDialCoordinator>();

        CallCommands = ActivatorUtilities.CreateInstance<ContactCenterCallCommandService>(
            services,
            (IActivityReservationService)reservationService,
            Mock.Of<IDialerProfileReader>(),
            Enumerable.Empty<IDialerAttemptService>(),
            (IContactCenterVoiceProviderResolver)providerResolver.Object,
            (IActivityQueueService)queueService,
            Enumerable.Empty<IContactCenterOfferAnsweredNotifier>(),
            preDial.Object);
    }

    private sealed class AllowingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    /// <summary>
    /// Every signed-in agent who is Available with no offer ringing, for a queue or for a direct offer.
    /// </summary>
    private sealed class DirectOrQueueAvailability : IAgentAvailabilityService
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly HarnessAvailabilityService _inner;

        public DirectOrQueueAvailability(DialerModeIntegrationHarness harness, HarnessAvailabilityService inner)
        {
            _harness = harness;
            _inner = inner;
        }

        public Task<AgentAvailability> GetAsync(string agentId, string queueId, CancellationToken cancellationToken = default)
            => _inner.GetAsync(agentId, queueId, cancellationToken);

        public Task<AgentAvailability> GetForDirectAsync(string agentId, CancellationToken cancellationToken = default)
            => _inner.GetForDirectAsync(agentId, cancellationToken);

        public async Task<IReadOnlyCollection<AgentAvailability>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
        {
            var available = new List<AgentAvailability>();

            foreach (var agentId in _harness.AgentIds)
            {
                var availability = await _inner.GetAsync(agentId, queueId, cancellationToken);

                if (availability is not null)
                {
                    available.Add(availability);
                }
            }

            return available;
        }
    }
}

/// <summary>
/// The provider seam: a Contact Center voice provider that can transfer, consult, park the caller and release agent
/// legs, recording each command it was given.
/// </summary>
internal sealed class FakeTransferVoiceProvider :
    IContactCenterVoiceProvider,
    IContactCenterVoiceTransferProvider,
    IContactCenterVoiceAttendedTransferProvider,
    IContactCenterVoiceAgentLegReleaseProvider,
    IContactCenterVoiceCallerParkProvider
{
    public string TechnicalName => DialerModeIntegrationHarness.ProviderName;

    public Microsoft.Extensions.Localization.LocalizedString Name => new(TechnicalName, TechnicalName);

    public ContactCenterVoiceProviderCapabilities Capabilities => ContactCenterVoiceProviderCapabilities.CallTransfer | ContactCenterVoiceProviderCapabilities.AgentConnect;

    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

    public List<ContactCenterVoiceTransferRequest> Transfers { get; } = [];

    public List<ContactCenterVoiceAttendedTransferRequest> ConsultsBegun { get; } = [];

    public List<ContactCenterVoiceAttendedTransferRequest> ConsultsCompleted { get; } = [];

    public List<ContactCenterVoiceAttendedTransferRequest> ConsultsCancelled { get; } = [];

    public List<string> ReleasedAgentLegs { get; } = [];

    public List<string> ParkedCallers { get; } = [];

    /// <summary>
    /// Gets every leg command in the order it was given: <c>park:{caller}</c> and <c>release:{agent leg}</c>.
    /// </summary>
    public List<string> LegCommands { get; } = [];

    public bool ParkSucceeds { get; set; } = true;

    public string ConsultLegId { get; set; } = "consult-leg-1";

    public Task<bool> ParkCallerAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        ParkedCallers.Add(providerCallId);
        LegCommands.Add($"park:{providerCallId}");

        return Task.FromResult(ParkSucceeds);
    }

    public Task<ContactCenterVoiceProviderResult> TransferAsync(ContactCenterVoiceTransferRequest request, CancellationToken cancellationToken = default)
    {
        Transfers.Add(request);

        return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName, ProviderCallId = request.ProviderCallId });
    }

    public Task<ContactCenterVoiceProviderResult> BeginConsultAsync(ContactCenterVoiceAttendedTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultsBegun.Add(request);

        return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName, ProviderCallId = request.ProviderCallId, ProviderLegId = ConsultLegId });
    }

    public Task<ContactCenterVoiceProviderResult> CompleteConsultAsync(ContactCenterVoiceAttendedTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultsCompleted.Add(request);

        return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName });
    }

    public Task<ContactCenterVoiceProviderResult> CancelConsultAsync(ContactCenterVoiceAttendedTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultsCancelled.Add(request);

        return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName });
    }

    public Task ReleaseAgentLegAsync(string agentLegId, CancellationToken cancellationToken = default)
    {
        ReleasedAgentLegs.Add(agentLegId);
        LegCommands.Add($"release:{agentLegId}");

        return Task.CompletedTask;
    }
}

/// <summary>
/// The queue-treatment provider seam, recording what the caller was played.
/// </summary>
internal sealed class RecordingQueueTreatmentProvider : IQueueTreatmentProvider
{
    public List<(string CallId, string MediaId)> HoldMusicStarted { get; } = [];

    public List<string> HoldMusicStopped { get; } = [];

    public Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default)
    {
        HoldMusicStarted.Add((providerCallId, mediaId));

        return Task.CompletedTask;
    }

    public Task StopHoldMusicAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        HoldMusicStopped.Add(providerCallId);

        return Task.CompletedTask;
    }

    public Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
