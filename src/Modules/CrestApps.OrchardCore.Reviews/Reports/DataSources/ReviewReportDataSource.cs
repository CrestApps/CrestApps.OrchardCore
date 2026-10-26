using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reviews.Reports.DataSources;

/// <summary>
/// Exposes the store's product reviews as a data source of Orchard Core, for reports and data pipelines. The data sets
/// are registered as <see cref="IReviewReportDataSet"/>s, and each data set checks that the principal may read it.
/// </summary>
public sealed class ReviewReportDataSource : RecordDataSource, IDataSource
{
    private readonly IReviewReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled reviews features register.</param>
    public ReviewReportDataSource(IEnumerable<IReviewReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.ReviewSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReviewReportDataSource>("Reviews");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ReviewReportDataSource>("The reviews buyers wrote of the store's products: their stars, whether the store approved them, and whether the reviewer bought the product.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
