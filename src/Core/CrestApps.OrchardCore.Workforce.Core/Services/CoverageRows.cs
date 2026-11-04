using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads coverage requirement rows the same way for coverage and for auto-scheduling.
/// </summary>
internal static class CoverageRows
{
    /// <summary>
    /// Returns whether a requirement applies on a day.
    /// </summary>
    public static bool AppliesOn(CoverageRequirement requirement, DateOnly date)
        => (requirement.EffectiveFrom is null || requirement.EffectiveFrom <= date) && (requirement.EffectiveTo is null || requirement.EffectiveTo >= date);

    /// <summary>
    /// Returns a requirement's rows for a day: its rows for the date, which replace the weekday's, or else the
    /// weekday's rows.
    /// </summary>
    public static IEnumerable<CoverageRow> RowsOn(CoverageRequirement requirement, DateOnly date)
    {
        var dated = requirement.Rows.Where(row => row.Date == date).ToArray();

        return dated.Length > 0
            ? dated
            : requirement.Rows.Where(row => row.Date is null && row.Day == date.DayOfWeek);
    }

    /// <summary>
    /// Returns the minute of the day a row starts.
    /// </summary>
    public static int StartMinute(CoverageRow row)
        => (row.Start.Hour * 60) + row.Start.Minute;

    /// <summary>
    /// Returns the minute of the day a row ends; an end of 00:00 is midnight at the end of the day.
    /// </summary>
    public static int EndMinute(CoverageRow row)
        => row.End == TimeOnly.MinValue ? 24 * 60 : (row.End.Hour * 60) + row.End.Minute;
}
