using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Scheduling.Reports.DataSources;

/// <summary>
/// The stable names of the scheduling report data source, its data sets, and the fields other data sets join on.
/// Report designs store them, so they must never change.
/// </summary>
internal static class SchedulingReportDataSets
{
    /// <summary>
    /// The technical name of the scheduling data source.
    /// </summary>
    public const string Source = "Scheduling";

    /// <summary>
    /// The appointments data set: one row per appointment.
    /// </summary>
    public const string Appointments = "Appointments";

    /// <summary>
    /// The appointment resources data set: one row per resource an appointment books.
    /// </summary>
    public const string AppointmentResources = "AppointmentResources";

    /// <summary>
    /// The appointment types data set: one row per appointment type.
    /// </summary>
    public const string AppointmentTypes = "AppointmentTypes";

    /// <summary>
    /// The resources data set: one row per staff member, room or piece of equipment that can be booked.
    /// </summary>
    public const string Resources = "SchedulingResources";

    /// <summary>
    /// The waitlist data set: one row per waitlist entry.
    /// </summary>
    public const string Waitlist = "WaitlistEntries";

    /// <summary>
    /// The name of the field that identifies a record.
    /// </summary>
    public const string ItemIdField = "ItemId";

    /// <summary>
    /// The name of the field that holds an appointment type.
    /// </summary>
    public const string AppointmentTypeIdField = "AppointmentTypeId";

    /// <summary>
    /// The name of the field that holds an appointment.
    /// </summary>
    public const string AppointmentIdField = "AppointmentId";

    /// <summary>
    /// The name of the field that holds a resource.
    /// </summary>
    public const string ResourceIdField = "ResourceId";

    /// <summary>
    /// Gets a reference to the record of one of the scheduling data sets.
    /// </summary>
    /// <param name="dataSet">The data set's name.</param>
    /// <returns>The reference.</returns>
    public static DataFieldReference Reference(string dataSet)
    {
        return new DataFieldReference(Source, dataSet, ItemIdField);
    }

    /// <summary>
    /// Gets a reference to a calendar or a calendar event of the calendars data source.
    /// </summary>
    /// <param name="dataSet">The calendars data set's name, <c>Calendars</c> or <c>CalendarEvents</c>.</param>
    /// <returns>The reference.</returns>
    public static DataFieldReference CalendarReference(string dataSet)
    {
        return new DataFieldReference("Calendars", dataSet, ItemIdField);
    }

    /// <summary>
    /// Joins values into one text, separated by commas, or <see langword="null"/> when there are none.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The text.</returns>
    public static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Gets the minutes between two instants, rounded, or <see langword="null"/> when either is missing.
    /// </summary>
    /// <param name="fromUtc">The first instant.</param>
    /// <param name="toUtc">The second instant.</param>
    /// <returns>The minutes.</returns>
    public static long? Minutes(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc is null || toUtc is null || fromUtc.Value == default || toUtc.Value == default)
        {
            return null;
        }

        return (long)Math.Round((toUtc.Value - fromUtc.Value).TotalMinutes, MidpointRounding.AwayFromZero);
    }
}
