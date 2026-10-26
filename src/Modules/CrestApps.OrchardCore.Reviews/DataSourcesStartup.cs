using CrestApps.OrchardCore.Reviews.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reviews;

/// <summary>
/// Exposes the product reviews as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<ReviewReportDataSource>()
            .AddScoped<IReviewReportDataSet, ProductReviewsReportDataSet>();
    }
}
