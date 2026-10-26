using CrestApps.OrchardCore.Marketplace.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketplace;

/// <summary>
/// Exposes the vendors, their earnings and their payouts as a data source of Orchard Core, for reports and data
/// pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<MarketplaceReportDataSource>()
            .AddScoped<IMarketplaceReportDataSet, VendorsReportDataSet>()
            .AddScoped<IMarketplaceReportDataSet, VendorEarningsReportDataSet>()
            .AddScoped<IMarketplaceReportDataSet, VendorPayoutsReportDataSet>();
    }
}
