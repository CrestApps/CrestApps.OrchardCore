using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Carts;

/// <summary>
/// Exposes the carts as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the carts and their lines. The wishlists feature registers the data sets of its own records, so they are
/// listed only while it is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<CartReportDataSource>()
            .AddScoped<ICartReportDataSet, CartsReportDataSet>()
            .AddScoped<ICartReportDataSet, CartLinesReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the wishlists feature.
/// </summary>
[Feature(CartConstants.Features.Wishlists)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class WishlistsDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<ICartReportDataSet, WishlistsReportDataSet>()
            .AddScoped<ICartReportDataSet, WishlistItemsReportDataSet>();
    }
}
