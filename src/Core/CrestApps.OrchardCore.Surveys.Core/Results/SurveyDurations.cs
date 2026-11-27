using System.Globalization;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Writes the durations the staff pages show. They count from the whole duration (its total hours or minutes), so a
/// response resumed days later or a page left open for hours shows its true length: a <see cref="TimeSpan"/> custom
/// format such as <c>h\:mm</c> drops the whole days, and <c>m\:ss</c> the whole hours.
/// </summary>
public static class SurveyDurations
{
    /// <summary>
    /// Writes a duration in hours and minutes, with the seconds when asked: <c>50:00:00</c> for fifty hours.
    /// </summary>
    /// <param name="duration">The duration; a negative one shows as zero.</param>
    /// <param name="withSeconds">Whether to write the seconds.</param>
    /// <returns>The text.</returns>
    public static string Hours(TimeSpan duration, bool withSeconds)
    {
        var value = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var hours = (long)value.TotalHours;

        return withSeconds
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, value.Minutes, value.Seconds)
            : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", hours, value.Minutes);
    }

    /// <summary>
    /// Writes a duration in minutes and seconds: <c>75:00</c> for seventy-five minutes.
    /// </summary>
    /// <param name="duration">The duration; a negative one shows as zero.</param>
    /// <returns>The text.</returns>
    public static string Minutes(TimeSpan duration)
    {
        var value = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (long)value.TotalMinutes, value.Seconds);
    }
}
