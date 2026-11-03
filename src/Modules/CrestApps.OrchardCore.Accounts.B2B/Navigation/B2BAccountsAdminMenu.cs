using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Accounts.B2B.Controllers;
using CrestApps.OrchardCore.Accounts.Services;
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
    private static readonly RouteValueDictionary _priceLists = new()
    {
        { "area", B2BAccountsConstants.Feature },
        { "controller", typeof(PriceListsAdminController).ControllerName() },
        { "action", nameof(PriceListsAdminController.Index) },
    };

    private readonly IAccountContentTypeProvider _accountContentTypeProvider;

    internal readonly IStringLocalizer S;

    public B2BAccountsAdminMenu(
        IAccountContentTypeProvider accountContentTypeProvider,
        IStringLocalizer<B2BAccountsAdminMenu> stringLocalizer)
    {
        _accountContentTypeProvider = accountContentTypeProvider;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Orchard Core's content list of the account types that have the B2B part, narrowed by the b2b term to the
        // accounts that buy as a business.
        var accountTypes = (await _accountContentTypeProvider.GetAccountTypesAsync())
            .Where(type => AccountRecordKinds.HasPart(type, B2BAccountsConstants.ContentParts.B2BAccount))
            .Select(type => type.Name)
            .ToArray();

        var accountsRoute = new RouteValueDictionary
        {
            { "area", "OrchardCore.Contents" },
            { "controller", "Admin" },
            { "action", "List" },
            { "contentTypeId", accountTypes.Length > 0 ? string.Join(',', accountTypes) : AccountsConstants.ContentTypes.Account },
            { "q", $"{B2BAccountsContentsAdminListFilterProvider.TermName}:true" },
        };

        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["B2B Accounts"], S["B2B Accounts"].PrefixPosition("3"), accounts => accounts
                    .Id("commerceB2BAccounts")
                    .Action(accountsRoute)
                    .Permission(B2BAccountsPermissions.ManageB2BAccounts)
                    .LocalNav())
                .Add(S["Price lists"], S["Price lists"].PrefixPosition("3.1"), priceLists => priceLists
                    .Id("commercePriceLists")
                    .Action(_priceLists)
                    .Permission(B2BAccountsPermissions.ManageB2BAccounts)
                    .LocalNav()));
    }
}
