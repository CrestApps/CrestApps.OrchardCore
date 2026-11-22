using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Users.Core.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

/// <summary>
/// The roles a subscription plan grants are chosen only by someone who may assign roles to users, as in its editor,
/// also when the plan is saved through the content API or an import.
/// </summary>
public sealed class SubscriptionEntitlementPartRolesHandler : GrantedRolesPartHandler<SubscriptionEntitlementPart>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionEntitlementPartRolesHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the signed-in user saving the plan.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionEntitlementPartRolesHandler(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<SubscriptionEntitlementPartRolesHandler> stringLocalizer)
        : base(httpContextAccessor, authorizationService, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<string> GetRoleNames(SubscriptionEntitlementPart part)
        => part.RoleNames;
}
