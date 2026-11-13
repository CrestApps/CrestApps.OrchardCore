using CrestApps.OrchardCore.PointOfSale.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PointOfSale.Navigation;

/// <summary>
/// Commerce › Point of sale › Web register, for staff who may sell at a register.
/// </summary>
public sealed class PosWebRegisterMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _register = new()
    {
        { "area", PosConstants.Feature },
        { "controller", typeof(WebRegisterController).ControllerName() },
        { "action", nameof(WebRegisterController.Index) },
    };

    internal readonly IStringLocalizer S;

    public PosWebRegisterMenu(IStringLocalizer<PosWebRegisterMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Point of sale"], S["Point of sale"].PrefixPosition("7"), pos => pos
                    .Id("pointOfSale")
                    .Add(S["Web register"], S["Web register"].PrefixPosition("0"), register => register
                        .Action(_register)
                        .Permission(PosPermissions.SellAtRegister)
                        .LocalNav())
                )
            );

        return ValueTask.CompletedTask;
    }
}
