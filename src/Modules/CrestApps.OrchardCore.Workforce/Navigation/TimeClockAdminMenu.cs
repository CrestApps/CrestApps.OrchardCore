using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the time clock's screens to Workforce > Time Management.
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
            .Add(S["Time Management"], S["Time Management"].PrefixPosition("20"), time => time
                .Id("workforceTimeManagement")
                .Add(S["Timesheets"], S["Timesheets"].PrefixPosition("10"), timesheets => timesheets
                    .Id("workforceTimesheets")
                    .Action("Index", "Timesheets", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ApproveTimesheets)
                    .LocalNav())
                .Add(S["Attendance"], S["Attendance"].PrefixPosition("11"), attendance => attendance
                    .Id("workforceAttendance")
                    .Action("Index", "Attendance", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ApproveTimesheets)
                    .LocalNav())
                .Add(S["Payroll Export"], S["Payroll Export"].PrefixPosition("12"), export => export
                    .Id("workforcePayrollExport")
                    .Action("Index", "PayrollExport", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ExportPayroll)
                    .LocalNav())
                .Add(S["Time Clock Policies"], S["Time Clock Policies"].PrefixPosition("50"), policies => policies
                    .Id("workforceTimeClockPolicies")
                    .Action("Index", "TimeClockPolicies", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                    .LocalNav())
                .Add(S["Pay Periods"], S["Pay Periods"].PrefixPosition("51"), periods => periods
                    .Id("workforcePayPeriods")
                    .Action("Index", "PayPeriods", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                    .LocalNav())
                .Add(S["Pay Codes"], S["Pay Codes"].PrefixPosition("52"), codes => codes
                    .Id("workforcePayCodes")
                    .Action("Index", "PayCodes", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                    .LocalNav())
                .Add(S["Time Clock Kiosks"], S["Time Clock Kiosks"].PrefixPosition("53"), kiosks => kiosks
                    .Id("workforceTimeClockKiosks")
                    .Action("Index", "TimeClockKiosks", WorkforceConstants.Features.Workforce)
                    .Permission(WorkforcePermissions.ManageTimeClockPolicies)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
