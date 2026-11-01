using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// Exposes the calendars and their events as a data source of Orchard Core, for reports and data pipelines. Only the
/// calendars the principal may read, and the events on them, are exposed, by the same rules as the calendar view.
/// </summary>
internal sealed class CalendarReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReportDataSource"/> class.
    /// </summary>
    /// <param name="calendars">The calendars data set.</param>
    /// <param name="events">The calendar events data set.</param>
    public CalendarReportDataSource(
        CalendarsReportDataSet calendars,
        CalendarEventsReportDataSet events)
    {
        _dataSets = [calendars, events];
    }

    /// <inheritdoc/>
    public static string Name => CalendarReportDataSets.Source;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CalendarReportDataSource>("Calendars");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CalendarReportDataSource>("The calendars you can read and the events on them: when they are, how long, their type, status and source.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
