using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Ends a transferred call on the soft phone of the agent who transferred it.
/// </summary>
/// <remarks>
/// The soft phone learns about a Contact Center call from the call's own events, and those follow whoever has the
/// call. Once it has been handed on they go to the next agent, so the agent who handed it over was never told it had
/// left them and kept a call on screen that was no longer theirs.
/// </remarks>
public sealed class ContactCenterSoftPhoneTransferEventHandler : IContactCenterEventHandler
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IClock _clock;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSoftPhoneTransferEventHandler"/> class.
    /// </summary>
    public ContactCenterSoftPhoneTransferEventHandler(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IAgentProfileManager agentProfileManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IClock clock,
        ShellSettings shellSettings)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentProfileManager = agentProfileManager;
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _clock = clock;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/SoftPhoneTransferProjection/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.InteractionTransferred)
        {
            return;
        }

        var transferringAgentId = interactionEvent.GetData<CallLifecycleEventData>()?.AgentId;
        var interactionId = string.IsNullOrEmpty(interactionEvent.InteractionId) ? interactionEvent.AggregateId : interactionEvent.InteractionId;

        if (string.IsNullOrEmpty(transferringAgentId) || string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
        var agent = await _agentProfileManager.FindByIdAsync(transferringAgentId, cancellationToken);

        if (interaction is null || string.IsNullOrEmpty(agent?.UserId))
        {
            return;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var callId = session?.ProviderCallId ?? interaction.ProviderInteractionId;

        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        var endedUtc = interactionEvent.OccurredUtc == default ? _clock.UtcNow : interactionEvent.OccurredUtc;
        var projection = await _telephonyInteractionStore.FindByCallIdAsync(agent.UserId, callId, cancellationToken);

        if (projection is not null && !projection.EndedUtc.HasValue)
        {
            projection.EndedUtc = endedUtc;
            projection.DurationSeconds = Math.Max(0, (endedUtc - projection.StartedUtc).TotalSeconds);
            projection.Outcome = CallOutcome.Completed;
            await _telephonyInteractionStore.UpdateAsync(projection, cancellationToken);
        }

        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, agent.UserId))
            .CallStateChanged(new TelephonyCall
            {
                CallId = callId,
                State = CallState.Disconnected,
                ProviderName = session?.ProviderName ?? interaction.ProviderName,
                Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["interactionId"] = interaction.ItemId,
                    ["transferred"] = true,
                },
            });
    }
}
