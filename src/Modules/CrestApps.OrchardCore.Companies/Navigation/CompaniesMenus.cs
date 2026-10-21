using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Companies.Controllers;
using CrestApps.OrchardCore.Companies.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Companies.Navigation;

/// <summary>
/// Commerce › Companies, and Commerce › Price lists.
/// </summary>
public sealed class CompaniesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _companies = new()
    {
        { "area", CompaniesConstants.Feature },
        { "controller", typeof(CompaniesAdminController).ControllerName() },
        { "action", nameof(CompaniesAdminController.Index) },
    };

    private static readonly RouteValueDictionary _priceLists = new()
    {
        { "area", CompaniesConstants.Feature },
        { "controller", typeof(PriceListsAdminController).ControllerName() },
        { "action", nameof(PriceListsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public CompaniesAdminMenu(IStringLocalizer<CompaniesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Companies"], S["Companies"].PrefixPosition("3"), companies => companies
                    .Id("commerceCompanies")
                    .Action(_companies)
                    .Permission(CompaniesPermissions.ManageCompanies)
                    .LocalNav())
                .Add(S["Price lists"], S["Price lists"].PrefixPosition("3.1"), priceLists => priceLists
                    .Id("commercePriceLists")
                    .Action(_priceLists)
                    .Permission(CompaniesPermissions.ManageCompanies)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
