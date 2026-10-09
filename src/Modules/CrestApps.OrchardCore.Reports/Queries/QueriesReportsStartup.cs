using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Queries;

/// <summary>
/// Registers the saved queries as a report data source. It needs no feature of its own: the queries become a data
/// source as soon as the Report Builder and Orchard Core Queries are both enabled.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures("OrchardCore.Queries")]
public sealed class QueriesReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReportDataSource, QueriesReportDataSource>();
    }
}
