using CrestApps.OrchardCore.PointOfSale.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PointOfSale.Navigation;

/// <summary>
/// Commerce › Point of sale › Registers and Shifts.
/// </summary>
public sealed class PosAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _registers = new()
    {
        { "area", PosConstants.Feature },
        { "controller", typeof(RegistersAdminController).ControllerName() },
        { "action", nameof(RegistersAdminController.Index) },
    };

    private static readonly RouteValueDictionary _shifts = new()
    {
        { "area", PosConstants.Feature },
        { "controller", typeof(RegistersAdminController).ControllerName() },
        { "action", nameof(RegistersAdminController.Shifts) },
    };

    internal readonly IStringLocalizer S;

    public PosAdminMenu(IStringLocalizer<PosAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Point of sale"], S["Point of sale"].PrefixPosition("7"), pos => pos
                    .AddClass("point-of-sale")
                    .Id("pointOfSale")
                    .Add(S["Registers"], S["Registers"].PrefixPosition("1"), registers => registers
                        .Action(_registers)
                        .Permission(PosPermissions.ManageRegisters)
                        .LocalNav())
                    .Add(S["Shifts"], S["Shifts"].PrefixPosition("2"), shifts => shifts
                        .Action(_shifts)
                        .Permission(PosPermissions.ManageRegisters)
                        .LocalNav())
                )
            );

        return ValueTask.CompletedTask;
    }
}
