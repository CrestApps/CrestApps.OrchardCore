using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Companies.Controllers;
using CrestApps.OrchardCore.Companies.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Companies.Navigation;

/// <summary>
/// Adds Company to the storefront account menu, for those who buy for one.
/// </summary>
public sealed class CompanyAccountMenu : NamedNavigationProvider
{
    private readonly CompanyService _companies;

    internal readonly IStringLocalizer S;

    public CompanyAccountMenu(CompanyService companies, IStringLocalizer<CompanyAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _companies = companies;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if ((await _companies.GetCurrentAsync()).Company is null)
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
