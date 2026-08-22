using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routers;

/// <summary>
/// The terminal router: a message on a DID that no <c>SmsNumberRoute</c> claims lands in the unassigned inbox
/// rather than being silently dropped, so a supervisor can triage it. This is what replaces the old
/// "Unable to link incoming SMS message… to an Activity" drop.
/// </summary>
public sealed class FallbackRouter : ISmsInboundRouter
{
    /// <inheritdoc/>
    public int Order => 1000;

    /// <inheritdoc/>
    public Task<bool> TryRouteAsync(SmsInboundRoutingContext context, CancellationToken cancellationToken = default)
    {
        var conversation = context.Conversation;

        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;
        conversation.OwnerType = SmsConversationOwnerType.Personal;
        conversation.OwnerId = null;
        conversation.AssignedAgentId = null;

        return Task.FromResult(true);
    }
}
