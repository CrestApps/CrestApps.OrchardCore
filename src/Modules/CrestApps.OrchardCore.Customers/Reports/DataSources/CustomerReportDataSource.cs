using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Customers.Reports.DataSources;

/// <summary>
/// Exposes the store's customers, with their loyalty points, store credit and referrals, as a data source of Orchard
/// Core, for reports and data pipelines. Each enabled customers feature registers the data sets of the records it
/// stores as <see cref="ICustomerReportDataSet"/>s, so a data set whose feature is off is never listed, and each data
/// set checks that the principal may read it.
/// </summary>
public sealed class CustomerReportDataSource : RecordDataSource, IDataSource
{
    private readonly ICustomerReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled customers features register.</param>
    public CustomerReportDataSource(IEnumerable<ICustomerReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.CustomerSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CustomerReportDataSource>("Customers");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CustomerReportDataSource>("The store's customers, with their loyalty points, store credit and referrals.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
