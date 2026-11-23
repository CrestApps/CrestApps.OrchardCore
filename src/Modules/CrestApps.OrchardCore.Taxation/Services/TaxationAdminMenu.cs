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
            .AddTaxation(S, taxation => taxation
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
                .Add(S["Sales Tax Rules"], S["Sales Tax Rules"].PrefixPosition(), rules => rules
                    .Id("taxationRules")
                    .Action("Index", "TaxRules", "CrestApps.OrchardCore.Taxation")
                    .Permission(TaxationPermissions.ManageTaxation)
                    .LocalNav())
                .Add(S["Tax Registrations"], S["Tax Registrations"].PrefixPosition(), registrations => registrations
                    .Id("taxationRegistrations")
                    .Action("Index", "TaxRegistrations", "CrestApps.OrchardCore.Taxation")
                    .Permission(TaxationPermissions.ManageTaxation)
                    .LocalNav()))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Tax Calculation"], S["Tax Calculation"].PrefixPosition(), calculation => calculation
                        .Id("taxationCalculation")
                        .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", TaxCalculationSettingsDisplayDriver.GroupId } })
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())
                    .Add(S["Tax Origin"], S["Tax Origin"].PrefixPosition(), origin => origin
                        .Id("taxationOrigin")
                        .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", TaxOriginSettingsDisplayDriver.GroupId } })
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
