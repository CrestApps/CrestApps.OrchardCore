using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Schedules, Open Shifts, Shift Trades, Rotations, Coverage and Shift Templates items to the Workforce menu.
/// </summary>
public sealed class ShiftPlanningAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftPlanningAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftPlanningAdminMenu(IStringLocalizer<ShiftPlanningAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], workforce => workforce
            .Add(S["Schedules"], S["Schedules"].PrefixPosition("20"), schedules => schedules
                .Id("workforceSchedules")
                .Action("Index", "Schedules", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Open Shifts"], S["Open Shifts"].PrefixPosition("21"), openShifts => openShifts
                .Id("workforceOpenShifts")
                .Action("Index", "OpenShifts", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageOpenShifts)
                .LocalNav())
            .Add(S["Shift Trades"], S["Shift Trades"].PrefixPosition("22"), trades => trades
                .Id("workforceShiftTrades")
                .Action("Index", "ShiftTrades", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApproveShiftTrades)
                .LocalNav())
            .Add(S["Rotations"], S["Rotations"].PrefixPosition("30"), rotations => rotations
                .Id("workforceRotations")
                .Action("Index", "Rotations", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Coverage"], S["Coverage"].PrefixPosition("31"), coverage => coverage
                .Id("workforceCoverage")
                .Action("Index", "Coverage", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Shift Templates"], S["Shift Templates"].PrefixPosition("80"), templates => templates
                .Id("shiftTemplates")
                .Action("Index", "ShiftTemplates", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageShiftTemplates)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
