using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Shift Schedules, Open Shifts, Shift Trades, Rotations, Coverage and Shift Templates items to the Calendars
/// menu: shift planning extends the calendars, like Scheduling, and publishes to each team's Workforce calendar.
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
        // After the calendars' own items and Scheduling's; positions compare as text.
        builder.Add(S["Calendars"], calendars => calendars
            .Add(S["Shift Schedules"], S["Shift Schedules"].PrefixPosition("8"), schedules => schedules
                .Id("workforceSchedules")
                .Action("Index", "Schedules", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Open Shifts"], S["Open Shifts"].PrefixPosition("81"), openShifts => openShifts
                .Id("workforceOpenShifts")
                .Action("Index", "OpenShifts", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageOpenShifts)
                .LocalNav())
            .Add(S["Shift Trades"], S["Shift Trades"].PrefixPosition("82"), trades => trades
                .Id("workforceShiftTrades")
                .Action("Index", "ShiftTrades", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApproveShiftTrades)
                .LocalNav())
            .Add(S["Rotations"], S["Rotations"].PrefixPosition("83"), rotations => rotations
                .Id("workforceRotations")
                .Action("Index", "Rotations", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Coverage"], S["Coverage"].PrefixPosition("84"), coverage => coverage
                .Id("workforceCoverage")
                .Action("Index", "Coverage", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageSchedules)
                .LocalNav())
            .Add(S["Shift Templates"], S["Shift Templates"].PrefixPosition("85"), templates => templates
                .Id("shiftTemplates")
                .Action("Index", "ShiftTemplates", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageShiftTemplates)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
