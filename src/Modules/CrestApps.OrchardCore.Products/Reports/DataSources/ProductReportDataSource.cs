using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// Exposes the store's products (with their variants and named prices), the products bundles are made of, and the
/// attributes listed products are filtered by, as a data source of Orchard Core, for reports and data pipelines. Each
/// enabled products feature registers the data sets of the records it keeps as <see cref="IProductReportDataSet"/>s, so
/// a data set whose feature is off is never listed, and each data set reads only the product types the principal may
/// view.
/// </summary>
public sealed class ProductReportDataSource : RecordDataSource, IDataSource
{
    private readonly IProductReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled products features register.</param>
    public ProductReportDataSource(IEnumerable<IProductReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.ProductSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ProductReportDataSource>("Products");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ProductReportDataSource>("The store's products, with their variants and prices, the products their bundles are made of, and the attributes listings filter them by.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
