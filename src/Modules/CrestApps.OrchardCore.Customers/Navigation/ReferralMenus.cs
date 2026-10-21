using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Settings > Commerce > Referrals.
/// </summary>
public sealed class ReferralSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", CustomersConstants.ReferralSettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public ReferralSettingsAdminMenu(IStringLocalizer<ReferralSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Referrals"], S["Referrals"].PrefixPosition(), referrals => referrals
                        .Id("referralsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(CustomerPermissions.ManageCustomers)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
