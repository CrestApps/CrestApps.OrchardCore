using System;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Default implementation of the shared fail-closed call-control authorization boundary.
/// </summary>
public sealed class CallControlAuthorizationService : ICallControlAuthorizationService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallControlAuthorizationService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager used to resolve the caller.</param>
    /// <param name="callSessionManager">The call session manager used to resolve logical calls.</param>
    /// <param name="supervisorQueueAuthorizationService">The supervisor queue authorization service.</param>
    public CallControlAuthorizationService(
        IAgentProfileManager agentManager,
        ICallSessionManager callSessionManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService)
    {
        _agentManager = agentManager;
        _callSessionManager = callSessionManager;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
    }

    /// <inheritdoc/>
    public async Task<CallControlAuthorizationResult> AuthorizeAsync(
        CallControlAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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

    private static bool IsTerminal(ContactCenterCallState state)
    {
        return state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred;
    }
}
