using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Navigation;

/// <summary>
/// Puts the account's home page first in the account menu.
/// </summary>
public sealed class CommerceAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public CommerceAccountMenu(IStringLocalizer<CommerceAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Overview"], "0", overview => overview
            .Id(Controllers.AccountController.HomeItemId)
            .AddClass("fa-solid fa-house")
            .Action("Index", "Account", new { area = CommerceConstants.Features.Area }));

        return ValueTask.CompletedTask;
    }
}
