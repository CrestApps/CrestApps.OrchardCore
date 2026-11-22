using System.Globalization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// Formats the answer cells of the survey reports. Date and time answers are wall-clock values the respondent typed
/// (stored without a time zone), so they are shown as they are and never converted as if they were UTC; a date-only
/// answer shows the date only.
/// </summary>
internal static class SurveyReportCells
{
    /// <summary>
    /// Formats the value of an answer column.
    /// </summary>
    /// <param name="value">The value, or <see langword="null"/> when there is no answer.</param>
    /// <param name="type">The type of the column.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="yes">The text of a true value.</param>
    /// <param name="no">The text of a false value.</param>
    /// <returns>The text, empty when there is no answer.</returns>
    public static string Format(object value, DataFieldType type, CultureInfo culture, string yes, string no)
        => value switch
        {
            null => string.Empty,
            DateTime date => date.ToString(type == DataFieldType.Date ? "d" : "g", culture),
            DateOnly day => day.ToString("d", culture),
            TimeOnly time => time.ToString("t", culture),
            decimal number => number.ToString(culture),
            double number => number.ToString(culture),
            long number => number.ToString(culture),
            int number => number.ToString(culture),
            bool flag => flag ? yes : no,
            _ => Convert.ToString(value, culture),
        };
}
