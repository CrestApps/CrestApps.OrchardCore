using CrestApps.OrchardCore.Calendars.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Exposes the calendars and their events as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<CalendarReportDataSource>()
            .AddScoped<CalendarReportAccess>()
            .AddScoped<CalendarsReportDataSet>()
            .AddScoped<CalendarEventsReportDataSet>();
    }
}
