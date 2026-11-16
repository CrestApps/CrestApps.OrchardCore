using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Taxation.Services;

internal sealed class TaxTablesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TaxTablesAdminMenu(IStringLocalizer<TaxTablesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Taxation"], S["Taxation"].PrefixPosition(), taxation => taxation
                .Id("taxation")
                .Add(S["Jurisdictions"], S["Jurisdictions"].PrefixPosition(), jurisdictions => jurisdictions
                    .Id("taxationJurisdictions")
                    .Action("Index", "TaxJurisdictions", "CrestApps.OrchardCore.Taxation")
                    .Permission(TaxationPermissions.ManageTaxation)
                    .LocalNav())
                .Add(S["Tables"], S["Tables"].PrefixPosition(), tables => tables
                    .Id("taxationTables")
                    .Action("Index", "TaxTables", "CrestApps.OrchardCore.Taxation")
                    .Permission(TaxationPermissions.ManageTaxation)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
