using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Accounts.B2B.Controllers;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Accounts.B2B.Navigation;

/// <summary>
/// Adds Business account to the storefront account menu, for those who buy for one.
/// </summary>
public sealed class BusinessAccountMenu : NamedNavigationProvider
{
    private readonly B2BAccountService _accounts;

    internal readonly IStringLocalizer S;

    public BusinessAccountMenu(B2BAccountService accounts, IStringLocalizer<BusinessAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _accounts = accounts;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if ((await _accounts.GetCurrentAsync()).Account is null)
        {
            return;
        }

        builder
            .Add(S["Business account"], "15", account => account
                .Id("account-business")
                .AddClass("fa-solid fa-building")
                .Action("Index", "BusinessAccount", new { area = B2BAccountsConstants.Feature }));
    }
}
