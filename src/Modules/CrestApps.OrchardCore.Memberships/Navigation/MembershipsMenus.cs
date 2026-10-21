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
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Memberships"], S["Memberships"].PrefixPosition("2.6"), memberships => memberships
                    .Id("commerceMemberships")
                    .Action(_memberships)
                    .Permission(MembershipsPermissions.ManageMemberships)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
