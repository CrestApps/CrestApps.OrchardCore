using CrestApps.OrchardCore.Users.Controllers;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users.Navigation;

/// <summary>
/// Puts the account's home page first in the account menu.
/// </summary>
public sealed class AccountHomeMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AccountHomeMenu(IStringLocalizer<AccountHomeMenu> stringLocalizer)
        : base(UsersConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Overview"], "0", overview => overview
            .Id(AccountController.HomeItemId)
            .AddClass("fa-solid fa-house")
            .Action("Index", "Account", new { area = UsersConstants.Feature.Area }));

        return ValueTask.CompletedTask;
    }
}
