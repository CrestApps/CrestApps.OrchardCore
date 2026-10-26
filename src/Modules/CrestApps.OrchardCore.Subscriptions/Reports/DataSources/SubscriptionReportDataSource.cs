using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Reports.DataSources;

/// <summary>
/// Exposes the store's subscriptions (with the lines they bill) and its installment plans (with their payments) as a
/// data source of Orchard Core, for reports and data pipelines. The Subscriptions feature and the Installment Plans
/// feature each register the data sets of the records they store as <see cref="ISubscriptionReportDataSet"/>s, so a
/// data set whose feature is off is never listed, and each data set checks that the principal may read it.
/// </summary>
public sealed class SubscriptionReportDataSource : RecordDataSource, IDataSource
{
    private readonly ISubscriptionReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled subscription features register.</param>
    public SubscriptionReportDataSource(IEnumerable<ISubscriptionReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.SubscriptionSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SubscriptionReportDataSource>("Subscriptions");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<SubscriptionReportDataSource>("The store's subscriptions, with the lines they bill each cycle, and its installment plans, with their payments.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
