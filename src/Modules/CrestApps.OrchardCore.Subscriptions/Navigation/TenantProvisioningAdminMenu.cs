using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Navigation;

/// <summary>
/// Adds the site-provisioning screens to the admin navigation.
/// </summary>
/// <remarks>
/// The operator entry matters more than it looks: a provisioning job that has been abandoned is a customer
/// who paid and has no site, and it is the one state in the module a person has to resolve. Without a menu
/// entry it would sit in the database unseen.
/// </remarks>
public sealed class TenantProvisioningAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _adminRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(TenantProvisioningAdminController).ControllerName() },
        { "action", nameof(TenantProvisioningAdminController.Index) },
    };

    private static readonly RouteValueDictionary _mySitesRouteValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", typeof(MySitesController).ControllerName() },
        { "action", nameof(MySitesController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantProvisioningAdminMenu(IStringLocalizer<TenantProvisioningAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Subscriptions"], S["Subscriptions"].PrefixPosition(), subscriptions => subscriptions
                .Add(S["Site provisioning"], S["Site provisioning"].PrefixPosition("5"), provisioning => provisioning
                    .Action(_adminRouteValues)
                    .Permission(SubscriptionPermissions.ManageSubscriptions)
                    .LocalNav()
                )
                .Add(S["My Sites"], S["My Sites"].PrefixPosition("6"), sites => sites
                    .Action(_mySitesRouteValues)
                    .Permission(SubscriptionPermissions.ManageOwnSubscriptions)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
