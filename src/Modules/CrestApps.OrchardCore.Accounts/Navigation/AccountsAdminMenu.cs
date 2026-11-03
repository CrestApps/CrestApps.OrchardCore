using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Accounts.Navigation;

/// <summary>
/// Adds "Accounts" to the admin menu: Orchard Core's content list scoped to the account types.
/// </summary>
internal sealed class AccountsAdminMenu : AdminNavigationProvider
{
    private readonly IAccountContentTypeProvider _accountContentTypeProvider;
    private readonly AccountsAdminMenuOptions _options;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountsAdminMenu"/> class.
    /// </summary>
    /// <param name="accountContentTypeProvider">The provider of the account types.</param>
    /// <param name="options">Whether the menu item is shown.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AccountsAdminMenu(
        IAccountContentTypeProvider accountContentTypeProvider,
        IOptions<AccountsAdminMenuOptions> options,
        IStringLocalizer<AccountsAdminMenu> stringLocalizer)
    {
        _accountContentTypeProvider = accountContentTypeProvider;
        _options = options.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (!_options.ShowAccountsMenu)
        {
            return;
        }

        var accountTypes = await _accountContentTypeProvider.GetAccountTypesAsync();

        if (accountTypes.Count == 0)
        {
            return;
        }

        builder.Add(S["Accounts"], "after.5", accounts => accounts
            .AddClass("accounts")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-building")
            .Id("accounts")
            .Action("List", "Admin", new RouteValueDictionary
            {
                { "area", "OrchardCore.Contents" },
                { "contentTypeId", string.Join(',', accountTypes.Select(type => type.Name)) },
            })
            .Permission(CommonPermissions.ListContent)
            .LocalNav());
    }
}
