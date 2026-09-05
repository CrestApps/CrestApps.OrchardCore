using CrestApps.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routers;

/// <summary>
/// Handles the <see cref="SmsNumberRouteDistributionMode.Routed"/> queue-target mode: a new conversation on a
/// routed queue is push-assigned to a specific eligible agent chosen by the <see cref="ISmsRoutingStrategy"/>.
/// It runs before <see cref="NumberRouteRouter"/> and only claims the message when an agent is available; when
/// nobody is eligible it returns <see langword="false"/> so the next router lands the message in the queue's
/// shared pool instead of dropping it.
/// </summary>
public sealed class RoutedQueueRouter : ISmsInboundRouter
{
    private readonly ISmsRoutingStrategy _routingStrategy;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutedQueueRouter"/> class.
    /// </summary>
    public RoutedQueueRouter(ISmsRoutingStrategy routingStrategy, IClock clock)
    {
        _routingStrategy = routingStrategy;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 250;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(SmsInboundRoutingContext context, CancellationToken cancellationToken = default)
    {
        // Only fresh conversations are routed here; an existing thread keeps its owner (handled earlier at Order 200).
        if (!context.IsNewConversation)
        {
            return false;
        }

        if (!context.Endpoint.TryGet<SmsEndpointRoutingSettings>(out var routing) ||
            routing is null ||
            routing.TargetType != SmsNumberRouteTargetType.Queue ||
            routing.DistributionMode != SmsNumberRouteDistributionMode.Routed ||
            string.IsNullOrEmpty(routing.TargetId))
        {
            return false;
        }

        var agentId = await _routingStrategy.SelectAgentAsync(routing.TargetId, cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(agentId))
        {
            // No eligible agent right now: let NumberRouteRouter place it in the shared pool as a fallback.
            return false;
        }

        var conversation = context.Conversation;

        // The queue still owns the thread (so supervisors see it under the department), but it is push-assigned to
        // a specific agent, who is the only one notified.
        conversation.OwnerType = SmsConversationOwnerType.Queue;
        conversation.OwnerId = routing.TargetId;
        conversation.AssignedAgentId = agentId;
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;
        conversation.AssignedUtc = _clock.UtcNow;

        return true;
    }
}
