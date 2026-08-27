using System.Collections.Concurrent;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// An advanceable clock so tests can drive retry-delay and expiry windows deterministically.
/// </summary>
internal sealed class TestClock : IClock
{
    private DateTime _utcNow = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;

    public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

    public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

    public ITimeZone[] GetTimeZones() => [];
}

/// <summary>
/// Records outbound dial requests and returns a synthetic provider call id, standing in for the media provider.
/// </summary>
internal sealed class FakeVoiceContactCenterCallRouter : IVoiceContactCenterCallRouter
{
    private int _counter;

    public List<ContactCenterDialRequest> PlacedCalls { get; } = [];

    public bool CanRouteOutbound(string providerName = null) => true;

    public string GetOutboundProviderName(string providerName = null) => DialerModeIntegrationHarness.ProviderName;

    public Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The dialer integration harness does not route inbound calls.");

    public Task<ContactCenterVoiceProviderResult> RouteOutboundAsync(ContactCenterDialRequest request, string providerName = null, CancellationToken cancellationToken = default)
    {
        PlacedCalls.Add(request);
        var callId = $"fake-call-{Interlocked.Increment(ref _counter)}";

        return Task.FromResult(new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = callId,
            ProviderName = DialerModeIntegrationHarness.ProviderName,
        });
    }
}

/// <summary>
/// Captures the durable-command registrations the dialing pipeline creates, so the harness dispatcher can drive
/// the real <see cref="DialProviderCommandTypeExecutor"/> without the production claim/lease store.
/// </summary>
internal sealed class InMemoryProviderCommandStateService : IProviderCommandStateService
{
    private readonly ConcurrentDictionary<string, ProviderCommand> _commands = new(StringComparer.Ordinal);

    public ProviderCommand Find(string commandId)
        => _commands.TryGetValue(commandId, out var command) ? command : null;

    public Task<ProviderCommand> RegisterAsync(ProviderCommandRegistration registration, CancellationToken cancellationToken = default)
    {
        var command = _commands.GetOrAdd(registration.CommandId, _ => new ProviderCommand
        {
            CommandId = registration.CommandId,
            ProviderName = registration.ProviderName,
            CommandType = registration.CommandType,
            ActivityItemId = registration.ActivityItemId,
            InteractionId = registration.InteractionId,
            ReservationId = registration.ReservationId,
            DialerProfileId = registration.DialerProfileId,
            RequestPayload = registration.RequestPayload,
            Status = ProviderCommandStatus.Pending,
        });

        return Task.FromResult(command);
    }

    // The remaining transitions belong to the durable command subsystem, which has its own tests. The dialer
    // happy path only registers commands; anything else here would signal the harness drifted from that path.
    public Task<ProviderCommandClaim> TryClaimAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> MarkSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> DeferSentAsync(string commandId, ProviderCommandClaim claim, string reason, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> ConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> StageConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> MarkOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> StageOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> EscalateExpiredLeaseAsync(string commandId, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommandClaim> TryClaimReconciliationAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> ConfirmFromReconciliationAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> StageConfirmFromReconciliationAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> BeginPendingCompensationAsync(string commandId, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> BeginCompensationAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommandClaim> TryClaimCompensationAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> CompleteCompensationAsync(string commandId, ProviderCommandClaim claim, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> PauseAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<ProviderCommand> FailAsync(string commandId, string reason = null, CancellationToken cancellationToken = default) => throw NotSupported();

    private static NotSupportedException NotSupported([System.Runtime.CompilerServices.CallerMemberName] string member = null)
        => new($"The dialer integration harness does not model '{member}'.");
}

/// <summary>
/// Runs deferred after-commit work synchronously on drain instead of on a real shell scope commit.
/// </summary>
internal sealed class HarnessScopeExecutor : IContactCenterScopeExecutor
{
    private readonly ConcurrentQueue<Func<Task>> _pending = new();
    private IServiceProvider _provider;

    public void Bind(IServiceProvider provider) => _provider = provider;

    public bool TryDequeue(out Func<Task> work) => _pending.TryDequeue(out work);

    public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
        => operation(_provider.GetRequiredService<TContext>());

    public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        _pending.Enqueue(() => operation(_provider.GetRequiredService<TContext>()));

        return true;
    }

    public bool ScheduleAfterCommit(Func<Task> operation)
    {
        _pending.Enqueue(operation);

        return true;
    }
}

/// <summary>
/// Dispatches a registered Dial command by running the real <see cref="DialProviderCommandTypeExecutor"/> against
/// the fake router, which places the call and stamps the interaction's provider call id.
/// </summary>
internal sealed class HarnessProviderCommandProcessor : IProviderCommandProcessor
{
    private readonly InMemoryProviderCommandStateService _stateService;
    private readonly DialProviderCommandTypeExecutor _executor;
    private readonly IClock _clock;

    public HarnessProviderCommandProcessor(
        InMemoryProviderCommandStateService stateService,
        DialProviderCommandTypeExecutor executor,
        IClock clock)
    {
        _stateService = stateService;
        _executor = executor;
        _clock = clock;
    }

    public async Task<ProviderCommand> DispatchAsync(string commandId, CancellationToken cancellationToken = default)
    {
        var command = _stateService.Find(commandId);

        if (command is null || command.CommandType != ProviderCommandType.Dial)
        {
            return command;
        }

        var claim = new ProviderCommandClaim
        {
            CommandId = commandId,
            FenceToken = 1,
            OwnerToken = "harness",
            LeaseExpiresUtc = _clock.UtcNow.AddMinutes(5),
        };

        var result = await _executor.ExecuteAsync(command, claim, cancellationToken);

        if (result.Succeeded)
        {
            await _executor.ProjectSuccessAsync(command, result, cancellationToken);
        }
        else
        {
            await _executor.ProjectFailureAsync(command, cancellationToken);
        }

        return command;
    }

    public Task<ProviderCommand> SettleDispatchAsync(string commandId, ProviderCommandClaim claim, ContactCenterVoiceProviderResult result, string providerReference, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ProviderCommand> SettleReconciliationAsync(string commandId, ProviderCommandClaim claim, ContactCenterVoiceCommandReconciliationResult result, string providerReference, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> RecoverDueAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

/// <summary>
/// Picks the next waiting queue item and an Available agent, then reserves through the real reservation service.
/// This stands in for the routing-selection policy while keeping every agent-state transition real.
/// </summary>
internal sealed class HarnessAssignmentService : IActivityAssignmentService
{
    private const int ReservationTimeoutSeconds = 30;

    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityReservationService _reservationService;
    private readonly TestClock _clock;
    private readonly List<string> _agentIds = [];

    public HarnessAssignmentService(
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IActivityReservationService reservationService,
        TestClock clock)
    {
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _reservationService = reservationService;
        _clock = clock;
    }

    public void RegisterAgent(string agentId) => _agentIds.Add(agentId);

    public async Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var waiting = await _queueItemManager.GetWaitingAsync(queueId, cancellationToken);

        foreach (var queueItem in waiting)
        {
            var candidates = new List<AgentProfile>();

            foreach (var agentId in _agentIds)
            {
                var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

                if (agent is not null &&
                    agent.PresenceStatus == AgentPresenceStatus.Available &&
                    string.IsNullOrEmpty(agent.ActiveReservationId))
                {
                    candidates.Add(agent);
                }
            }

            // Least-recently-assigned first, so work spreads fairly across every agent even when a paced mode
            // only reserves a few per cycle. This mirrors the real routing service's last-assigned ordering.
            foreach (var agent in candidates.OrderBy(a => a.LastAssignedUtc ?? DateTime.MinValue))
            {
                var reservation = await _reservationService.ReserveAsync(queueItem, agent, ReservationTimeoutSeconds, cancellationToken);

                if (reservation is not null)
                {
                    // Advance time a tick so each reservation gets a strictly later assigned time, giving the
                    // next pick a deterministic least-recently-assigned order.
                    _clock.Advance(TimeSpan.FromSeconds(1));

                    return reservation;
                }
            }
        }

        return null;
    }

    public async Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var assigned = 0;

        while (await AssignNextAsync(queueId, cancellationToken) is not null)
        {
            assigned++;
        }

        return assigned;
    }

    public Task<ActivityReservation> AssignSpecificAsync(string activityItemId, string queueId, string agentId, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The dialer integration harness does not model specific assignment.");
}

/// <summary>
/// Answers availability directly from the agent profile: Available with no active reservation.
/// </summary>
internal sealed class HarnessAvailabilityService : IAgentAvailabilityService
{
    private readonly IAgentProfileManager _agentManager;

    public HarnessAvailabilityService(IAgentProfileManager agentManager)
    {
        _agentManager = agentManager;
    }

    public async Task<AgentAvailability> GetAsync(string agentId, string queueId, CancellationToken cancellationToken = default)
        => await ResolveAsync(agentId, cancellationToken);

    public async Task<AgentAvailability> GetForDirectAsync(string agentId, CancellationToken cancellationToken = default)
        => await ResolveAsync(agentId, cancellationToken);

    public Task<IReadOnlyCollection<AgentAvailability>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<AgentAvailability>>([]);

    private async Task<AgentAvailability> ResolveAsync(string agentId, CancellationToken cancellationToken)
    {
        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available ||
            !string.IsNullOrEmpty(agent.ActiveReservationId))
        {
            return null;
        }

        return new AgentAvailability
        {
            Agent = agent,
            ActiveInteractionCount = 0,
            LastHeartbeatUtc = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// An in-memory CRM activity store, exposed as an <see cref="IOmnichannelActivityManager"/> supporting the
/// lookups and status writes the dialing pipeline performs.
/// </summary>
internal sealed class InMemoryOmnichannelActivities
{
    private readonly ConcurrentDictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);

    public void Add(OmnichannelActivity activity) => _activities[activity.ItemId] = activity;

    public OmnichannelActivity Get(string activityId) => _activities.TryGetValue(activityId, out var activity) ? activity : null;

    public IOmnichannelActivityManager BuildManager()
    {
        var mock = new Mock<IOmnichannelActivityManager>();

        mock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((id, _) => new ValueTask<OmnichannelActivity>(Get(id)));

        mock.Setup(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns<OmnichannelActivity, System.Text.Json.Nodes.JsonNode, CancellationToken>((activity, _, __) =>
            {
                _activities[activity.ItemId] = activity;

                return ValueTask.CompletedTask;
            });

        return mock.Object;
    }
}

/// <summary>
/// Captures published domain events for optional assertion.
/// </summary>
internal sealed class RecordingContactCenterEventPublisher : IContactCenterEventPublisher
{
    public List<InteractionEvent> Events { get; } = [];

    public Task PublishAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(interactionEvent);

        return Task.CompletedTask;
    }
}
