using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Refer a friend to the storefront account menu.
/// </summary>
public sealed class ReferralAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public ReferralAccountMenu(IStringLocalizer<ReferralAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Refer a friend"], "70", refer => refer
                .Id("account-referrals")
                .AddClass("fa-solid fa-user-plus")
                .Action("Index", "ReferralAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}

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
                        .AddClass("referrals-settings")
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
