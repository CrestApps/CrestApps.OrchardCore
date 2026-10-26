using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products;

/// <summary>
/// Exposes the products as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the products, their variants and their named prices. The bundles and product listing features register the
/// data sets of their own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<ProductReportDataSource>()
            .AddScoped<ProductReportAccess>()
            .AddScoped<IProductReportDataSet, ProductsReportDataSet>()
            .AddScoped<IProductReportDataSet, ProductVariantsReportDataSet>()
            .AddScoped<IProductReportDataSet, ProductPricesReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the product bundles feature.
/// </summary>
[Feature(ProductConstants.Feature.Bundles)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class BundlesDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IProductReportDataSet, ProductBundleItemsReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the product listing feature.
/// </summary>
[Feature(ProductConstants.Feature.Listing)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class ListingDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IProductReportDataSet, ProductAttributesReportDataSet>();
    }
}
