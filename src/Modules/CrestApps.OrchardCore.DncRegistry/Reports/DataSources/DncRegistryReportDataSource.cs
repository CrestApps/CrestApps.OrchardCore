using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DncRegistry.Reports.DataSources;

/// <summary>
/// Exposes the local do-not-call lists and their phone numbers as a data source of Orchard Core, for reports and data
/// pipelines. Each data set checks that the principal may manage the DNC registry settings.
/// </summary>
public sealed class DncRegistryReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="DncRegistryReportDataSource"/> class.
    /// </summary>
    /// <param name="lists">The DNC lists data set.</param>
    /// <param name="entries">The DNC entries data set.</param>
    public DncRegistryReportDataSource(
        DncListsReportDataSet lists,
        DncEntriesReportDataSet entries)
    {
        _dataSets = [lists, entries];
    }

    /// <inheritdoc/>
    public static string Name => DncRegistryReportDataSets.Source;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DncRegistryReportDataSource>("DNC registry");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DncRegistryReportDataSource>("The local do-not-call lists uploaded to the site and the phone numbers imported into them.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
