using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the time clock's screens to the Workforce menu.
/// </summary>
internal sealed class TimeClockAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TimeClockAdminMenu(IStringLocalizer<TimeClockAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], workforce => workforce
            .Add(S["Timesheets"], S["Timesheets"].PrefixPosition("24"), timesheets => timesheets
                .Id("workforceTimesheets")
                .Action("Index", "Timesheets", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApproveTimesheets)
                .LocalNav())
            .Add(S["Attendance"], S["Attendance"].PrefixPosition("25"), attendance => attendance
                .Id("workforceAttendance")
                .Action("Index", "Attendance", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApproveTimesheets)
                .LocalNav())
            .Add(S["Payroll Export"], S["Payroll Export"].PrefixPosition("26"), export => export
                .Id("workforcePayrollExport")
                .Action("Index", "PayrollExport", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ExportPayroll)
                .LocalNav())
            .Add(S["Time Clock Policies"], S["Time Clock Policies"].PrefixPosition("86"), policies => policies
                .Id("workforceTimeClockPolicies")
                .Action("Index", "TimeClockPolicies", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                .LocalNav())
            .Add(S["Pay Periods"], S["Pay Periods"].PrefixPosition("87"), periods => periods
                .Id("workforcePayPeriods")
                .Action("Index", "PayPeriods", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                .LocalNav())
            .Add(S["Pay Codes"], S["Pay Codes"].PrefixPosition("88"), codes => codes
                .Id("workforcePayCodes")
                .Action("Index", "PayCodes", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                .LocalNav())
            .Add(S["Time Clock Kiosks"], S["Time Clock Kiosks"].PrefixPosition("89"), kiosks => kiosks
                .Id("workforceTimeClockKiosks")
                .Action("Index", "TimeClockKiosks", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
