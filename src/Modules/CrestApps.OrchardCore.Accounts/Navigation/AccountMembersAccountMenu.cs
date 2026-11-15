using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Accounts.Navigation;

/// <summary>
/// Adds Account members to the storefront account menu.
/// </summary>
public sealed class AccountMembersAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AccountMembersAccountMenu(IStringLocalizer<AccountMembersAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Account members"], "31", members => members
            .Id("account-members")
            .AddClass("fa-solid fa-users")
            .Url("~/account/members"));

        return ValueTask.CompletedTask;
    }
}
