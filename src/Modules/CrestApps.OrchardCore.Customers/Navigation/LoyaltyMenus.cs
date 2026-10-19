using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Rewards to the storefront account menu.
/// </summary>
public sealed class LoyaltyAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public LoyaltyAccountMenu(IStringLocalizer<LoyaltyAccountMenu> stringLocalizer)
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
            .Add(S["Rewards"], "65", rewards => rewards
                .Id("account-rewards")
                .AddClass("fa-solid fa-star")
                .Action("Index", "LoyaltyAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds Settings > Commerce > Rewards.
/// </summary>
public sealed class LoyaltySettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", CustomersConstants.LoyaltySettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public LoyaltySettingsAdminMenu(IStringLocalizer<LoyaltySettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Rewards"], S["Rewards"].PrefixPosition(), rewards => rewards
                        .AddClass("rewards-settings")
                        .Id("rewardsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(CustomerPermissions.ManageCustomers)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
