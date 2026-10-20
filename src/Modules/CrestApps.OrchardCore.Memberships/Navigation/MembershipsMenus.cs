using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Memberships.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Memberships.Navigation;

/// <summary>
/// Commerce › Memberships.
/// </summary>
public sealed class MembershipsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _memberships = new()
    {
        { "area", MembershipsConstants.Feature },
        { "controller", typeof(MembershipsAdminController).ControllerName() },
        { "action", nameof(MembershipsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public MembershipsAdminMenu(IStringLocalizer<MembershipsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Memberships"], S["Memberships"].PrefixPosition("2.6"), memberships => memberships
                    .AddClass("commerce-memberships")
                    .Id("commerceMemberships")
                    .Action(_memberships)
                    .Permission(MembershipsPermissions.ManageMemberships)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds the customer's memberships to the storefront account.
/// </summary>
public sealed class MembershipsAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public MembershipsAccountMenu(IStringLocalizer<MembershipsAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Memberships"], "21", memberships => memberships
            .Id("account-memberships")
            .AddClass("fa-solid fa-id-card")
            .Action("Index", "AccountMemberships", new { area = MembershipsConstants.Feature }));

        return ValueTask.CompletedTask;
    }
}
