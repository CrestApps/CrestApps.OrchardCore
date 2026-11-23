using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.MailingLists.Reports;

/// <summary>
/// Exposes the members of the mailing lists as a data source of Orchard Core, for reports, their CSV and Excel exports,
/// and data pipelines. The data set checks that the principal may see the members of mailing lists.
/// </summary>
public sealed class MailingListsReportDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The technical name of the data source, stored in report designs.
    /// </summary>
    public const string SourceName = "MailingLists";

    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListsReportDataSource"/> class.
    /// </summary>
    /// <param name="members">The data set of the members of the mailing lists.</param>
    public MailingListsReportDataSource(MailingListMembersReportDataSet members)
    {
        _dataSets = [members];
    }

    /// <inheritdoc/>
    public static string Name => SourceName;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MailingListsReportDataSource>("Mailing lists");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<MailingListsReportDataSource>("The members of the mailing lists: who signed up to which list, how, and where each membership stands.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
