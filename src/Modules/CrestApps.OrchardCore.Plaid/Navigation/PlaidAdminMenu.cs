using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Plaid.Navigation;

/// <summary>
/// Adds Settings > Plaid.
/// </summary>
internal sealed class PlaidAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public PlaidAdminMenu(IStringLocalizer<PlaidAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Plaid"], S["Plaid"].PrefixPosition(), plaid => plaid
                    .Id("plaidSettings")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", PlaidConstants.SettingsGroupId } })
                    .Permission(PlaidPermissions.ManagePlaidSettings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
