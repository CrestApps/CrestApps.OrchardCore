using CrestApps.OrchardCore.Scheduling.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Exposes the appointments, appointment types, resources and waitlist as a data source of Orchard Core, for reports
/// and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<SchedulingReportDataSource>()
            .AddScoped<AppointmentsReportDataSet>()
            .AddScoped<AppointmentResourcesReportDataSet>()
            .AddScoped<AppointmentTypesReportDataSet>()
            .AddScoped<SchedulingResourcesReportDataSet>()
            .AddScoped<WaitlistReportDataSet>();
    }
}
