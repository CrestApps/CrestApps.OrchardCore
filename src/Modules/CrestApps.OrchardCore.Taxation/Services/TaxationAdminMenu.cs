using CrestApps.OrchardCore.Taxation.Core;
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
                .AddClass("commerce")
                .Id("commerce")
                .Add(S["Taxation"], S["Taxation"].PrefixPosition(), taxation => taxation
                    .AddClass("taxation")
                    .Id("taxation")
                    .Add(S["Categories"], S["Categories"].PrefixPosition(), categories => categories
                        .AddClass("taxation-categories")
                        .Id("taxationCategories")
                        .Action("Index", "TaxCategories", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Types"], S["Types"].PrefixPosition(), types => types
                        .AddClass("taxation-types")
                        .Id("taxationTypes")
                        .Action("Index", "TaxTypes", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Jurisdictions"], S["Jurisdictions"].PrefixPosition(), jurisdictions => jurisdictions
                        .AddClass("taxation-jurisdictions")
                        .Id("taxationJurisdictions")
                        .Action("Index", "TaxJurisdictions", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Rules"], S["Rules"].PrefixPosition(), rules => rules
                        .AddClass("taxation-rules")
                        .Id("taxationRules")
                        .Action("Index", "TaxRules", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Tables"], S["Tables"].PrefixPosition(), tables => tables
                        .AddClass("taxation-tables")
                        .Id("taxationTables")
                        .Action("Index", "TaxTables", "CrestApps.OrchardCore.Taxation")
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
