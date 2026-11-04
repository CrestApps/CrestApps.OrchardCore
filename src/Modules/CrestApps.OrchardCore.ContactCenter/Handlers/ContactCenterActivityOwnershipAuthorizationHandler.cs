using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
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
/// </remarks>
internal sealed class ContactCenterActivityOwnershipAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IContactCenterWorkStateManager _workStateManager;
    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityOwnershipAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="workStateManager">The work state manager.</param>
    /// <param name="authorizationService">
    /// The authorization service, resolved lazily: an authorization handler that constructed it eagerly would
    /// close the cycle between the service and the handlers it runs.
    /// </param>
    public ContactCenterActivityOwnershipAuthorizationHandler(
        IContactCenterWorkStateManager workStateManager,
        Lazy<IAuthorizationService> authorizationService)
    {
        _workStateManager = workStateManager;
        _authorizationService = authorizationService;
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
        if (!await _authorizationService.Value.AuthorizeAsync(context.User, OmnichannelConstants.Permissions.CompleteOwnActivity))
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
        }
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
