using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Payroll.Reports;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Payroll's reports under Workforce &amp; Payroll: they export to the Reports formats and are e-mailed by Report
/// Subscriptions.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(ReportsConstants.Feature)]
public sealed class PayrollReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<PayrollRegisterReport>();
        services.AddReport<PayrollSummaryReport>();
        services.AddReport<CashRequirementReport>();
        services.AddReport<DeductionsAndContributionsReport>();
        services.AddReport<LaborDistributionReport>();
        services.AddReport<YearToDateEarningsReport>();
    }
}

/// <summary>
/// Payroll's dashboard widgets: the cash requirement and the payroll summary.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class PayrollReportWidgetsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<PayrollReportWidgetMigrations>();
}

/// <summary>
/// Pay dates on the calendar: the runs of the employers a person may see, and a staff member's own pay dates.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(CrestApps.OrchardCore.Calendars.CalendarsConstants.Features.Calendars)]
public sealed class PayrollCalendarStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => CrestApps.OrchardCore.Calendars.CalendarServiceCollectionExtensions.AddCalendarEventSource<Services.PayDateCalendarEventSource>(services);
}
