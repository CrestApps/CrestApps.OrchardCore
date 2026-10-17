using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// Exposes the records the Contact Center stores (interactions, their events, calls, recordings, queues, agents and
/// more) as a data source of Orchard Core, for reports and data pipelines. Each enabled Contact Center feature registers
/// the data sets of the records it stores as <see cref="IContactCenterReportDataSet"/>s, so a data set whose feature is
/// off is never listed, and each data set checks that the principal may read it.
/// </summary>
public sealed class ContactCenterReportDataSource : RecordDataSource, IDataSource
{
    private readonly IContactCenterReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled Contact Center features register.</param>
    public ContactCenterReportDataSource(IEnumerable<IContactCenterReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => ContactCenterReportDataSets.Source;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ContactCenterReportDataSource>("Contact Center");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ContactCenterReportDataSource>("The interactions, calls, queues, agents and other records of the Contact Center.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
