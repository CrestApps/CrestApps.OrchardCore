using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Taxation.Services;

internal sealed class TaxationAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TaxationAdminMenu(IStringLocalizer<TaxationAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Taxation"], S["Taxation"].PrefixPosition(), taxation => taxation
                    .Id("taxation")
                    .Add(S["Categories"], S["Categories"].PrefixPosition(), categories => categories
                        .Id("taxationCategories")
                        .Action("Index", "TaxCategories", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Types"], S["Types"].PrefixPosition(), types => types
                        .Id("taxationTypes")
                        .Action("Index", "TaxTypes", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Jurisdictions"], S["Jurisdictions"].PrefixPosition(), jurisdictions => jurisdictions
                        .Id("taxationJurisdictions")
                        .Action("Index", "TaxJurisdictions", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Rules"], S["Rules"].PrefixPosition(), rules => rules
                        .Id("taxationRules")
                        .Action("Index", "TaxRules", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Tables"], S["Tables"].PrefixPosition(), tables => tables
                        .Id("taxationTables")
                        .Action("Index", "TaxTables", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Tax origin"], S["Tax origin"].PrefixPosition(), origin => origin
                        .Id("taxationOrigin")
                        .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", TaxOriginSettingsDisplayDriver.GroupId } })
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
