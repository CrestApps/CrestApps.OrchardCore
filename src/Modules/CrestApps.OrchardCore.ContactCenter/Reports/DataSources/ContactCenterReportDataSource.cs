using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// Exposes the records the Contact Center stores (interactions, their events, calls, recordings, queues, agents and
/// more) to the report builder. Each enabled Contact Center feature registers the data sets of the records it stores
/// as <see cref="IContactCenterReportDataSet"/>s, so a data set whose feature is off is never listed, and each data set
/// checks that the principal may read it.
/// </summary>
public sealed class ContactCenterReportDataSource : ReportRecordDataSource
{
    private readonly IContactCenterReportDataSet[] _dataSets;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled Contact Center features register.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterReportDataSource(
        IEnumerable<IContactCenterReportDataSet> dataSets,
        IStringLocalizer<ContactCenterReportDataSource> stringLocalizer)
    {
        _dataSets = dataSets.ToArray();
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => ContactCenterReportDataSets.Source;

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Contact Center"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["The interactions, calls, queues, agents and other records of the Contact Center."];

    /// <inheritdoc/>
    protected override IEnumerable<IReportRecordDataSet> DataSets => _dataSets;
}
