using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Navigation;

/// <summary>
/// Adds <b>Subscriptions → Installment Plans</b> and <b>Settings → Installment Plans</b> to the admin menu.
/// </summary>
public sealed class InstallmentPlansAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", SubscriptionConstants.Features.Area },
        { "controller", "InstallmentPlans" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", SubscriptionConstants.InstallmentPlans.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlansAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentPlansAdminMenu(IStringLocalizer<InstallmentPlansAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Subscriptions"], subscriptions => subscriptions
                .Add(S["Installment Plans"], S["Installment Plans"].PrefixPosition("3"), plans => plans
                    .Id("installmentPlans")
                    .Action(_routeValues)
                    .Permission(SubscriptionPermissions.ManageInstallmentPlans)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Installment Plans"], S["Installment Plans"].PrefixPosition(), plans => plans
                    .Id("installmentPlansSettings")
                    .Action("Index", "Admin", _settingsRouteValues)
                    .Permission(SubscriptionPermissions.ManageInstallmentPlans)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
