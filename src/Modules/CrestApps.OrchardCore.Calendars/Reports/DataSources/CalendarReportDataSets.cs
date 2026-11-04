namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// The stable names of the calendars report data source, its data sets, and the fields other data sets join on.
/// Report designs store them, so they must never change.
/// </summary>
internal static class CalendarReportDataSets
{
    /// <summary>
    /// The technical name of the calendars data source.
    /// </summary>
    public const string Source = "Calendars";

    /// <summary>
    /// The calendars data set: one row per calendar.
    /// </summary>
    public const string Calendars = "Calendars";

    /// <summary>
    /// The calendar events data set: one row per stored event.
    /// </summary>
    public const string Events = "CalendarEvents";

    /// <summary>
    /// The name of the field that identifies a record.
    /// </summary>
    public const string ItemIdField = "ItemId";

    /// <summary>
    /// The name of the field that holds the calendar an event is on.
    /// </summary>
    public const string CalendarIdField = "CalendarId";
}
