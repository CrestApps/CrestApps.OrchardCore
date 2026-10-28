using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// Exposes the checkouts buyers started, the coupons and their uses, and the payment methods customers kept, as a data
/// source of Orchard Core, for reports and data pipelines. The data sets are registered as
/// <see cref="ICheckoutReportDataSet"/>s, and each data set checks that the principal may read it.
/// </summary>
public sealed class CheckoutReportDataSource : RecordDataSource, IDataSource
{
    private readonly ICheckoutReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled checkout features register.</param>
    public CheckoutReportDataSource(IEnumerable<ICheckoutReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.CheckoutSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CheckoutReportDataSource>("Checkout");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CheckoutReportDataSource>("The checkouts buyers started, abandoned checkouts included, the coupons and their uses, and the payment methods customers kept.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
