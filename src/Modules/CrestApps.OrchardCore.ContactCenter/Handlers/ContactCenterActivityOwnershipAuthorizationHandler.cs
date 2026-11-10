using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Lets an agent complete an activity that routing has given them, deciding who holds it from the routing-owned
/// work state rather than from the CRM activity's copy of it.
/// </summary>
/// <remarks>
/// The activity's <c>ReservedById</c> and <c>AssignedToId</c> are a read model, reconciled after the routing
/// transaction commits. A preview record opens the activity screen the moment the offer reaches the agent, which can
/// be before that reconciliation, and the Omnichannel ownership check then reads an activity with no owner and
/// refuses the agent the record they were just handed. The work state is authoritative for who holds work, so this
/// handler grants the same permission from it; the Omnichannel handler keeps granting it from the activity.
/// Routing also lets go of the work once its call ends, clearing the owner while the disposition for that call is
/// still owed, so the agent who handled the activity's last call may complete it while nobody else holds it.
/// </remarks>
internal sealed class ContactCenterActivityOwnershipAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IContactCenterWorkStateManager _workStateManager;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityOwnershipAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="workStateManager">The work state manager.</param>
    /// <param name="serviceProvider">
    /// The service provider the authorization service is resolved from when it is needed: an authorization handler
    /// that constructed it eagerly would close the cycle between the service and the handlers it runs.
    /// </param>
    public ContactCenterActivityOwnershipAuthorizationHandler(
        IContactCenterWorkStateManager workStateManager,
        IServiceProvider serviceProvider)
    {
        _workStateManager = workStateManager;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded ||
            requirement.Permission != OmnichannelConstants.Permissions.CompleteActivity)
        {
            return;
        }

        var activity = GetActivity(context.Resource);

        if (activity is null || string.IsNullOrEmpty(activity.ItemId))
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // A role check is cheap and a work state lookup is not, so ask the permission first: most callers of this
        // check are rows in a list, and a user who cannot complete their own work never needs the lookup.
        var authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(context.User, OmnichannelConstants.Permissions.CompleteOwnActivity))
        {
            return;
        }

        var workState = await _workStateManager.FindByActivityIdAsync(activity.ItemId);

        if (workState is null)
        {
            return;
        }

        if (string.Equals(workState.AssignedToId, userId, StringComparison.Ordinal) ||
            string.Equals(workState.ReservedById, userId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);

            return;
        }

        if (await HandledLastCallAsync(activity, workState, userId))
        {
            context.Succeed(requirement);
        }
    }

    // The agent took the activity's call, and nobody has the activity now. Routing lets go of the work once the call
    // ends -- a release, a reconciliation that read the call as never reaching an agent -- and clears its owner, while
    // the disposition for the call the agent just had is still owed. The call itself says who handled it, as it does
    // for the bar and the workspace that offer the agent this completion.
    private async Task<bool> HandledLastCallAsync(OmnichannelActivity activity, ContactCenterWorkState workState, string userId)
    {
        // Work someone else holds now is theirs, and a finished activity takes no disposition. Both are read without a
        // query, so a row in someone else's list never pays for the lookups below.
        if (!string.IsNullOrEmpty(workState.AssignedToId) ||
            !string.IsNullOrEmpty(workState.ReservedById) ||
            activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged)
        {
            return false;
        }

        // Resolved only on this path, for the same reason as the authorization service above.
        var interactionManager = _serviceProvider.GetService<IInteractionManager>();
        var agentManager = _serviceProvider.GetService<IAgentProfileManager>();

        if (interactionManager is null || agentManager is null)
        {
            return false;
        }

        var interaction = await interactionManager.FindByActivityIdAsync(activity.ItemId);

        if (!HandledCallRule.IsHandledCall(interaction) || string.IsNullOrEmpty(interaction.AgentId))
        {
            return false;
        }

        var agent = await agentManager.FindByUserIdAsync(userId);

        return HandledCallRule.IsHandledBy(interaction, agent?.ItemId);
    }

    private static OmnichannelActivity GetActivity(object resource)
    {
        if (resource is OmnichannelActivity activity)
        {
            return activity;
        }

        if (resource is OmnichannelActivityContainer container)
        {
            return container.Activity;
        }

        return null;
    }
}
