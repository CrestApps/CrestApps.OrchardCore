using System.Globalization;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Formats the facts of a call recording the same way on the list and on the recording's own page.
/// </summary>
public static class CallRecordingFormat
{
    /// <summary>
    /// Formats a call's length as <c>m:ss</c>, or <c>h:mm:ss</c> for a call of an hour or more.
    /// </summary>
    /// <param name="seconds">The length of the call, in seconds.</param>
    /// <returns>The formatted length, or <c>-</c> when the length is not known.</returns>
    public static string Duration(double seconds)
    {
        if (seconds <= 0)
        {
            return "-";
        }

        var span = TimeSpan.FromSeconds(Math.Round(seconds));

        return span.ToString(span.TotalHours >= 1 ? "h\\:mm\\:ss" : "m\\:ss", CultureInfo.InvariantCulture);
    }
}
