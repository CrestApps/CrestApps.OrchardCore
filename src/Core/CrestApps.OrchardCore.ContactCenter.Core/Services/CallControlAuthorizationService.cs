using System;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Default implementation of the shared fail-closed call-control authorization boundary.
/// </summary>
public sealed class CallControlAuthorizationService : ICallControlAuthorizationService
{
    private static readonly HashSet<CallControlVerb> _systemInitiatedVerbs =
    [
        CallControlVerb.Decline,
        CallControlVerb.Voicemail,
    ];

    private readonly IAgentProfileManager _agentManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallControlAuthorizationService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager used to resolve the caller.</param>
    /// <param name="callSessionManager">The call session manager used to resolve logical calls.</param>
    /// <param name="interactionManager">The interaction manager used to resolve system-initiated calls.</param>
    /// <param name="supervisorQueueAuthorizationService">The supervisor queue authorization service.</param>
    public CallControlAuthorizationService(
        IAgentProfileManager agentManager,
        ICallSessionManager callSessionManager,
        IInteractionManager interactionManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService)
    {
        _agentManager = agentManager;
        _callSessionManager = callSessionManager;
        _interactionManager = interactionManager;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
    }

    /// <inheritdoc/>
    public async Task<CallControlAuthorizationResult> AuthorizeAsync(
        CallControlAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Initiator == CallControlInitiator.System)
        {
            return await AuthorizeSystemAsync(context, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(context.UserId) ||
            string.IsNullOrWhiteSpace(context.InteractionId))
        {
            return CallControlAuthorizationResult.Denied();
        }

        var agent = await _agentManager.FindByUserIdAsync(context.UserId, cancellationToken);

        if (agent is null)
        {
            return CallControlAuthorizationResult.Denied();
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(context.InteractionId, cancellationToken);

        if (session is null || IsTerminal(session.State) || string.IsNullOrWhiteSpace(session.ProviderCallId))
        {
            return CallControlAuthorizationResult.Denied();
        }

        if (!string.IsNullOrWhiteSpace(context.ProviderName) &&
            !string.Equals(session.ProviderName, context.ProviderName, StringComparison.Ordinal))
        {
            return CallControlAuthorizationResult.Denied();
        }

        if (!string.IsNullOrWhiteSpace(context.ProviderCallId) &&
            !string.Equals(session.ProviderCallId, context.ProviderCallId, StringComparison.Ordinal))
        {
            return CallControlAuthorizationResult.Denied();
        }

        if (context.SupervisorOperation)
        {
            return await _supervisorQueueAuthorizationService.IsAuthorizedAsync(
                context.Principal,
                context.UserId,
                session.QueueId,
                cancellationToken)
                ? CallControlAuthorizationResult.Success(agent.ItemId, session)
                : CallControlAuthorizationResult.Denied();
        }

        if (!string.Equals(session.AgentId, agent.ItemId, StringComparison.Ordinal))
        {
            return CallControlAuthorizationResult.Denied();
        }

        return CallControlAuthorizationResult.Success(agent.ItemId, session);
    }

    private async Task<CallControlAuthorizationResult> AuthorizeSystemAsync(
        CallControlAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        // A system-initiated operation has no requesting principal, so the ownership check that protects an
        // agent request cannot apply. It is restricted to the terminal verbs the platform actually issues so a
        // future caller cannot reach a privileged verb by declaring itself the system, and it must never claim
        // supervisor privilege, which is only ever granted to a resolved principal.
        if (context.SupervisorOperation ||
            !_systemInitiatedVerbs.Contains(context.Verb) ||
            string.IsNullOrWhiteSpace(context.InteractionId))
        {
            return CallControlAuthorizationResult.Denied();
        }

        // The provider call identifier is resolved from the interaction rather than the call session, because a
        // call terminated at a closed entry point or an unroutable queue is rejected before any provider event
        // has been ingested and therefore has no session yet.
        var interaction = await _interactionManager.FindByIdAsync(context.InteractionId, cancellationToken);

        if (interaction is null ||
            IsTerminal(interaction.Status) ||
            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            return CallControlAuthorizationResult.Denied();
        }

        if (!string.IsNullOrWhiteSpace(context.ProviderName) &&
            !string.Equals(interaction.ProviderName, context.ProviderName, StringComparison.Ordinal))
        {
            return CallControlAuthorizationResult.Denied();
        }

        if (!string.IsNullOrWhiteSpace(context.ProviderCallId) &&
            !string.Equals(interaction.ProviderInteractionId, context.ProviderCallId, StringComparison.Ordinal))
        {
            return CallControlAuthorizationResult.Denied();
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(context.InteractionId, cancellationToken);

        if (session is not null)
        {
            if (IsTerminal(session.State) || string.IsNullOrWhiteSpace(session.ProviderCallId))
            {
                return CallControlAuthorizationResult.Denied();
            }

            return CallControlAuthorizationResult.Success(session.AgentId, session);
        }

        return CallControlAuthorizationResult.Success(interaction.AgentId, interaction.ProviderInteractionId);
    }

    private static bool IsTerminal(InteractionStatus status)
    {
        return status is InteractionStatus.Ended or InteractionStatus.Failed;
    }

    private static bool IsTerminal(VoiceCallState state)
    {
        return state is VoiceCallState.Ended or
            VoiceCallState.Failed or
            VoiceCallState.NoAnswer or
            VoiceCallState.Rejected or
            VoiceCallState.Canceled or
            VoiceCallState.Transferred;
    }
}
