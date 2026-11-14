using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Payroll.Navigation;

/// <summary>
/// Adds Payroll &gt; Setup to the admin menu: pay schedules and pay codes.
/// </summary>
public sealed class PaySetupAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaySetupAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaySetupAdminMenu(IStringLocalizer<PaySetupAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll => payroll
            .Id("payroll")
            .AddClass("payroll")
            .Add(S["Setup"], S["Setup"].PrefixPosition("90"), setup => setup
                .Id("payrollSetup")
                .Add(S["Pay Schedules"], S["Pay Schedules"].PrefixPosition("10"), schedules => schedules
                    .Id("paySchedules")
                    .Action("Index", "PaySchedules", PayrollConstants.Features.Payroll)
                    .Permission(PayrollPermissions.ManagePaySetup)
                    .LocalNav())
                .Add(S["Pay Codes"], S["Pay Codes"].PrefixPosition("20"), codes => codes
                    .Id("payCodes")
                    .Action("Index", "PayCodes", PayrollConstants.Features.Payroll)
                    .Permission(PayrollPermissions.ManagePaySetup)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds the employers to Payroll &gt; Setup and Payroll's settings to Settings.
/// </summary>
public sealed class PayrollAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollAdminMenu(IStringLocalizer<PayrollAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll => payroll
            .Id("payroll")
            .Add(S["Employees"], S["Employees"].PrefixPosition("20"), employees => employees
                .Id("payrollEmployees")
                .Action("Index", "PayProfiles", PayrollConstants.Features.Payroll)
                .LocalNav())
            .Add(S["Setup"], S["Setup"].PrefixPosition("90"), setup => setup
                .Id("payrollSetup")
                .Add(S["Employers"], S["Employers"].PrefixPosition("5"), employers => employers
                    .Id("payrollEmployers")
                    .Action("Index", "Employers", PayrollConstants.Features.Payroll)
                    .LocalNav())));

        builder.Add(S["Settings"], settings => settings
            .Add(S["Payroll"], S["Payroll"].PrefixPosition(), payroll => payroll
                .Id("payrollSettings")
                .Action("Index", "Admin", new Microsoft.AspNetCore.Routing.RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", PayrollConstants.SettingsGroupId } })
                .Permission(PayrollPermissions.ManagePayrollSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
