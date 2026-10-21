using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Memberships.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Memberships.Navigation;

/// <summary>
/// Adds the customer's memberships to the storefront account.
/// </summary>
public sealed class MembershipsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public MembershipsAccountMenu(IStringLocalizer<MembershipsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Memberships"], "21", memberships => memberships
            .Id("account-memberships")
            .AddClass("fa-solid fa-id-card")
            .Action("Index", "AccountMemberships", new { area = MembershipsConstants.Feature }));

        return ValueTask.CompletedTask;
    }
}
