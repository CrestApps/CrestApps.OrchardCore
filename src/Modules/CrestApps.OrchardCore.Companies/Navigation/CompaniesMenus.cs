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
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Companies"], S["Companies"].PrefixPosition("3"), companies => companies
                    .AddClass("commerce-companies")
                    .Id("commerceCompanies")
                    .Action(_companies)
                    .Permission(CompaniesPermissions.ManageCompanies)
                    .LocalNav())
                .Add(S["Price lists"], S["Price lists"].PrefixPosition("3.1"), priceLists => priceLists
                    .AddClass("commerce-price-lists")
                    .Id("commercePriceLists")
                    .Action(_priceLists)
                    .Permission(CompaniesPermissions.ManageCompanies)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds Company to the storefront account menu, for those who buy for one.
/// </summary>
public sealed class CompanyAccountMenu : INavigationProvider
{
    private readonly CompanyService _companies;

    internal readonly IStringLocalizer S;

    public CompanyAccountMenu(CompanyService companies, IStringLocalizer<CompanyAccountMenu> stringLocalizer)
    {
        _companies = companies;
        S = stringLocalizer;
    }

    public async ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal) ||
            (await _companies.GetCurrentAsync()).Company is null)
        {
            return;
        }

        builder
            .Add(S["Company"], "15", company => company
                .Id("account-company")
                .AddClass("fa-solid fa-building")
                .Action("Index", "CompanyAccount", new { area = CompaniesConstants.Feature }));
    }
}
