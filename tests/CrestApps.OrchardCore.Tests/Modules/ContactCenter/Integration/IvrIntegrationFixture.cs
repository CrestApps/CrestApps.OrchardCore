using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// An inbound caller on an entry point with a phone menu, on the dialer harness's real SQLite store. The menu runtime,
/// the digits sink, the IVR router and everything a routed caller passes through — the queue, reservation, offer,
/// presence and treatment services — run for real. Only the provider is faked: the menu provider records what the
/// caller was played, and voicemail and external transfers are recorded at the seam their own tests cover.
/// </summary>
internal sealed class IvrIntegrationFixture : IAsyncDisposable
{
    public const string ActivityId = "activity-ivr";
    public const string InteractionId = "interaction-ivr";
    public const string CallId = "v3:ivr-caller";
    public const string EntryPointId = "entry-main";
    public const string AgentId = "agent-1";
    public const string UserId = "user-1";
    public const string SupportQueueId = DialerModeIntegrationHarness.QueueId;
    public const string SalesQueueId = "queue-sales";

    private IvrIntegrationFixture(DialerModeIntegrationHarness harness)
    {
        Harness = harness;
    }

    public DialerModeIntegrationHarness Harness { get; }

    public RecordingIvrProvider Menus { get; } = new();

    public RecordingQueueTreatmentProvider Treatment { get; } = new();

    public List<(string ActivityId, string Reason)> Voicemails { get; } = [];

    public List<string> ExternalTransfers { get; } = [];

    public List<CallbackRequest> Callbacks { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the tenant has callbacks enabled, so a scheduled callback is kept.
    /// </summary>
    public bool CallbacksEnabled { get; set; } = true;

    public Dictionary<string, ActivityQueue> Queues { get; } = new(StringComparer.Ordinal)
    {
        [SupportQueueId] = new ActivityQueue
        {
            ItemId = SupportQueueId,
            Name = "Support",
            Enabled = true,
            Treatment = new QueueTreatmentSettings { HoldMusicMediaId = "https://media.example.test/support.mp3" },
        },
        [SalesQueueId] = new ActivityQueue
        {
            ItemId = SalesQueueId,
            Name = "Sales",
            Enabled = true,
            Treatment = new QueueTreatmentSettings { HoldMusicMediaId = "https://media.example.test/sales.mp3" },
        },
    };

    public IQueueTreatmentService TreatmentService { get; private set; }

    public IActivityReservationService Reservations { get; private set; }

    public OmnichannelActivity FindActivity()
        => Harness.Services.GetRequiredService<InMemoryOmnichannelActivities>().Get(ActivityId);

    public ContactCenterEntryPoint EntryPoint { get; } = CreateEntryPoint();

    public IIvrCallRouter Router { get; private set; }

    public IInboundVoiceDigitsSink Sink { get; private set; }

    public IExternalTransferOutcomeSink TransferOutcomes { get; private set; }

    public IReadOnlyList<InteractionEvent> Events => Harness.PublishedEvents;

    /// <summary>
    /// Creates the fixture: an agent signed in and Available, and an inbound call that inbound routing has just handed
    /// to the entry point's menu, exactly as it leaves it: activity and interaction written, the entry point recorded,
    /// nothing queued.
    /// </summary>
    public static async Task<IvrIntegrationFixture> CreateAsync()
    {
        var harness = await DialerModeIntegrationHarness.CreateAsync();
        var fixture = new IvrIntegrationFixture(harness);

        await harness.SignInAgentAsync(AgentId, UserId);
        await fixture.SeedInboundCallAsync();
        fixture.Compose();

        return fixture;
    }

    /// <summary>
    /// Runs what inbound routing schedules after its commit: answering the caller and playing the first menu.
    /// </summary>
    public async Task StartAsync()
    {
        await Router.StartAsync(InteractionId, TestContext.Current.CancellationToken);
        await Harness.CommitAsync();
    }

    /// <summary>
    /// Delivers a <c>call.gather.ended</c> the way the Telnyx webhook hands it to the Contact Center.
    /// </summary>
    public async Task<bool> PressAsync(string digits, string deliveryId, InboundVoiceDigitsOutcome outcome = InboundVoiceDigitsOutcome.Collected)
    {
        var handled = await Sink.HandleDigitsAsync(new InboundVoiceDigitsEvent
        {
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderCallId = CallId,
            Digits = digits,
            DeliveryId = deliveryId,
            Outcome = outcome,
        }, TestContext.Current.CancellationToken);

        await Harness.CommitAsync();

        return handled;
    }

    public async Task TakeAgentOfflineAsync()
    {
        var agent = await Harness.AgentManager.FindByIdAsync(AgentId, TestContext.Current.CancellationToken);
        agent.PresenceStatus = AgentPresenceStatus.Offline;
        await Harness.AgentManager.UpdateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await Harness.CommitAsync();
    }

    public async Task<Interaction> FindInteractionAsync()
        => await Harness.InteractionManager.FindByIdAsync(InteractionId, TestContext.Current.CancellationToken);

    public async Task<QueueItem> FindQueueItemAsync()
        => await Harness.Services.GetRequiredService<IQueueItemManager>().FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken);

    public async Task<IReadOnlyCollection<ActivityReservation>> FindReservationsAsync()
        => await Harness.Services.GetRequiredService<IActivityReservationManager>().GetActiveByActivityAsync(ActivityId, TestContext.Current.CancellationToken);

    public IReadOnlyList<string> IvrEventTypes()
        => Events
            .Where(e => e.InteractionId == InteractionId && e.EventType.StartsWith("Ivr", StringComparison.Ordinal))
            .Select(e => e.EventType)
            .ToArray();

    public ValueTask DisposeAsync() => Harness.DisposeAsync();

    private static ContactCenterEntryPoint CreateEntryPoint()
        => new()
        {
            ItemId = EntryPointId,
            Name = "Main line",
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = SupportQueueId,
            Priority = InteractionPriority.Normal,
            RingTimeoutSeconds = 40,
            IvrFlow = new IvrFlow
            {
                RootNodeId = "main",
                MaxRetries = 3,
                Nodes =
                [
                    new IvrNode
                    {
                        NodeId = "main",
                        Prompt = "For sales press 1, for support press 2, for Sam press 3, to leave a message press 9, to hear this again press 0.",
                        Options =
                        [
                            new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = SalesQueueId } },
                            new IvrOption { Digit = "2", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "support" } },
                            new IvrOption { Digit = "3", Action = new IvrAction { Kind = IvrActionKind.RouteToAgent, TargetId = AgentId } },
                            new IvrOption { Digit = "9", Action = new IvrAction { Kind = IvrActionKind.Voicemail } },
                            new IvrOption { Digit = "0", Action = new IvrAction { Kind = IvrActionKind.Repeat } },
                        ],
                    },
                    new IvrNode
                    {
                        NodeId = "support",
                        Prompt = "For billing press 1, for our partner line press 2.",
                        Options =
                        [
                            new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = SupportQueueId } },
                            new IvrOption { Digit = "2", Action = new IvrAction { Kind = IvrActionKind.ExternalTransfer, TargetId = "dest-partner" } },
                        ],
                    },
                ],
            },
        };

    private async Task SeedInboundCallAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Harness.Services.GetRequiredService<InMemoryOmnichannelActivities>().Add(new OmnichannelActivity
        {
            ItemId = ActivityId,
            Kind = ActivityKind.Call,
            Channel = "Phone",
            Status = ActivityStatus.AwaitingAgentResponse,
        });

        await Harness.Services.GetRequiredService<IContactCenterWorkStateService>().MutateAsync(
            ActivityId,
            workState => workState.TransitionTo(ActivityAssignmentStatus.Available),
            cancellationToken);

        var interaction = new Interaction
        {
            ItemId = InteractionId,
            ActivityItemId = ActivityId,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderInteractionId = CallId,
            CustomerAddress = "+17025550100",
            CreatedUtc = Harness.Clock.UtcNow,
        };
        interaction.TransitionTo(InteractionStatus.Ringing);
        interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.ServiceAddress] = "+17025550199";
        interaction.TechnicalMetadata[EntryPointFlowResolver.EntryPointMetadataKey] = EntryPointId;

        await Harness.InteractionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
        await Harness.CommitAsync();
    }

    private void Compose()
    {
        var services = Harness.Services;
        var clock = services.GetRequiredService<IClock>();

        var queues = Queues;

        var queueManager = new Mock<IActivityQueueManager>();
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

        var limits = new Mock<IQueueLimitService>();
        limits
            .Setup(service => service.AdmitAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityQueue queue, CancellationToken _) => QueueAdmissionDecision.Admit(queue.ItemId));

        var availability = new IvrAvailability(Harness);
        var treatment = new QueueTreatmentService(
            services.GetRequiredService<IQueueItemManager>(),
            services.GetRequiredService<IInteractionManager>(),
            Treatment,
            availability,
            clock,
            NullLogger<QueueTreatmentService>.Instance);
        TreatmentService = treatment;

        var queueService = ActivatorUtilities.CreateInstance<ActivityQueueService>(services, queueManager.Object, businessHours.Object, (IQueueTreatmentProvider)Treatment);
        var reservationService = ActivatorUtilities.CreateInstance<ActivityReservationService>(services, queueManager.Object, (IActivityQueueService)queueService, (IAgentAvailabilityService)availability);
        Reservations = reservationService;
        var withdrawalService = ActivatorUtilities.CreateInstance<QueuedWorkWithdrawalService>(services, (IActivityQueueService)queueService, (IActivityReservationService)reservationService);
        var assignmentService = ActivatorUtilities.CreateInstance<ActivityAssignmentService>(
            services,
            (IActivityQueueManager)queueManager.Object,
            (IActivityReservationService)reservationService,
            (IQueuedWorkWithdrawalService)withdrawalService,
            (IActivityRoutingService)new ActivityRoutingService([new LongestIdleRoutingStrategy()]),
            businessHours.Object,
            (IAgentAvailabilityService)availability);
        var offerService = ActivatorUtilities.CreateInstance<VoiceQueueOfferService>(
            services,
            (IActivityAssignmentService)assignmentService,
            (IActivityReservationService)reservationService,
            (IActivityReservationReclaimer)reservationService,
            Mock.Of<IProviderVoiceOfferSynchronizationService>());

        var entryPoints = new Mock<IContactCenterEntryPointManager>();
        entryPoints
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(id == EntryPointId ? EntryPoint : null));
        var flowResolver = new EntryPointFlowResolver(entryPoints.Object);

        var ivr = new IvrExecutionService(
            services.GetRequiredService<IInteractionManager>(),
            Menus,
            services.GetRequiredService<IContactCenterAuditRecorder>(),
            Harness.Session,
            clock,
            NullLogger<IvrExecutionService>.Instance);

        var processor = new Mock<IInboundVoiceCallProcessor>();
        processor
            .Setup(value => value.SendToVoicemailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((activityId, reason, _) => Voicemails.Add((activityId, reason)))
            .ReturnsAsync(true);

        var external = new Mock<IIvrExternalTransferService>();
        external
            .Setup(value => value.TransferAsync(It.IsAny<Interaction>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Interaction, string, CancellationToken>((_, destination, _) => ExternalTransfers.Add(destination))
            .ReturnsAsync(true);
        external
            .Setup(value => value.FailAsync(It.IsAny<Interaction>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ExternalTransfers.LastOrDefault());

        Router = new IvrCallRouter(
            services.GetRequiredService<IInteractionManager>(),
            flowResolver,
            ivr,
            queueManager.Object,
            limits.Object,
            queueService,
            offerService,
            treatment,
            Harness.AgentManager,
            processor.Object,
            external.Object,
            services.GetRequiredService<IContactCenterAuditRecorder>(),
            Harness.Session,
            clock,
            NullLogger<IvrCallRouter>.Instance);

        TransferOutcomes = new IvrExternalTransferOutcomeSink(
            services.GetRequiredService<IInteractionManager>(),
            external.Object,
            Router,
            NullLogger<IvrExternalTransferOutcomeSink>.Instance);

        var callbackService = new Mock<ICallbackService>();
        callbackService
            .Setup(value => value.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallbackRequest request, CancellationToken _) =>
            {
                if (!CallbacksEnabled)
                {
                    return null;
                }

                Callbacks.Add(request);

                return request;
            });

        // The confirmation is said after the commit; the harness runs it inline, on the recording provider.
        var afterCommit = new Mock<IContactCenterScopeExecutor>();
        afterCommit.Setup(value => value.ScheduleAfterCommit(It.IsAny<Func<IQueueTreatmentProvider, Task>>())).Returns(false);

        var callbackOffers = new QueueCallbackOfferResponder(
            services.GetRequiredService<IQueueItemManager>(),
            queueManager.Object,
            new QueuedCallbackService(
                callbackService.Object,
                services.GetRequiredService<IQueueItemManager>(),
                queueService,
                clock,
                NullLogger<QueuedCallbackService>.Instance),
            treatment,
            services.GetRequiredService<IInteractionManager>(),
            services.GetRequiredService<IContactCenterWorkStateService>(),
            services.GetRequiredService<IContactCenterActivityWriter>(),
            afterCommit.Object,
            Treatment,
            clock,
            NullLogger<QueueCallbackOfferResponder>.Instance);

        Sink = new InboundVoiceDigitsSink(
            services.GetRequiredService<IInteractionManager>(),
            flowResolver,
            ivr,
            Router,
            callbackOffers,
            services.GetRequiredService<IContactCenterAuditRecorder>(),
            clock,
            NullLogger<InboundVoiceDigitsSink>.Instance);
    }

    /// <summary>
    /// Every signed-in agent who is Available with no offer ringing, for a queue or for a direct offer.
    /// </summary>
    private sealed class IvrAvailability : IAgentAvailabilityService
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly HarnessAvailabilityService _inner;

        public IvrAvailability(DialerModeIntegrationHarness harness)
        {
            _harness = harness;
            _inner = new HarnessAvailabilityService(harness.AgentManager);
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
/// The menu provider seam: records every answer and every menu played, in order.
/// </summary>
internal sealed class RecordingIvrProvider : IIvrProvider
{
    public List<string> Commands { get; } = [];

    public List<(string CallId, string Text, string ValidDigits)> Prompts { get; } = [];

    public Task<bool> AnswerAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        Commands.Add($"answer:{providerCallId}");

        return Task.FromResult(true);
    }

    public Task<bool> PromptAsync(string providerCallId, string text, string mediaId, string validDigits, CancellationToken cancellationToken = default)
    {
        Commands.Add($"gather:{validDigits}");
        Prompts.Add((providerCallId, text, validDigits));

        return Task.FromResult(true);
    }
}
