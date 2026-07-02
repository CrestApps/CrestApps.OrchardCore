using CrestApps.OrchardCore.Reports.Drivers;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Registers the reusable Reports framework: the report and export registries, the built-in date-range
/// filter, the CSV export format, and the admin Reports navigation.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IReportManager, ReportManager>()
            .AddScoped<IReportExportManager, ReportExportManager>()
            .AddScoped<IReportExportFormat, CsvReportExportFormat>();

        services.AddDisplayDriver<ReportFilter, ReportDateRangeFilterDisplayDriver>();

        services.AddNavigationProvider<ReportsAdminMenu>();
    }
}
