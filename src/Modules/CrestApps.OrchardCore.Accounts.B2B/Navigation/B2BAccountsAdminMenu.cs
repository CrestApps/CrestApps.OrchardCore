using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Accounts.B2B.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Accounts.B2B.Navigation;

/// <summary>
/// Commerce › B2B Accounts, the accounts that buy as a business, and Commerce › Price lists.
/// </summary>
public sealed class B2BAccountsAdminMenu : AdminNavigationProvider
{
    // Orchard Core's content list of the account type, narrowed by the b2b term to the accounts that buy as a business.
    private static readonly RouteValueDictionary _accounts = new()
    {
        { "area", "OrchardCore.Contents" },
        { "controller", "Admin" },
        { "action", "List" },
        { "contentTypeId", AccountsConstants.ContentTypes.Account },
        { "q", $"{B2BAccountsContentsAdminListFilterProvider.TermName}:true" },
    };

    private static readonly RouteValueDictionary _priceLists = new()
    {
        { "area", B2BAccountsConstants.Feature },
        { "controller", typeof(PriceListsAdminController).ControllerName() },
        { "action", nameof(PriceListsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public B2BAccountsAdminMenu(IStringLocalizer<B2BAccountsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["B2B Accounts"], S["B2B Accounts"].PrefixPosition("3"), accounts => accounts
                    .Id("commerceB2BAccounts")
                    .Action(_accounts)
                    .Permission(B2BAccountsPermissions.ManageB2BAccounts)
                    .LocalNav())
                .Add(S["Price lists"], S["Price lists"].PrefixPosition("3.1"), priceLists => priceLists
                    .Id("commercePriceLists")
                    .Action(_priceLists)
                    .Permission(B2BAccountsPermissions.ManageB2BAccounts)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
