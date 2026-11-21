using System.Globalization;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Reads the points a grader typed on a grading page, the same way in every language. A number field posts "7.5" in
/// every browser language, while a grader who types into a plain field may write "7,5"; either means seven and a half.
/// One comma or point is the decimal separator, and thousands separators are not taken (points are small numbers), so
/// "1.000,5" is refused rather than read as a thousand or as a hundred. The request culture is never used: MVC's own
/// binder read "7.5" as 75 in German and refused it in French.
/// </summary>
public static class GradePoints
{
    /// <summary>
    /// Reads points.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="points">The points, or <see langword="null"/> for empty text.</param>
    /// <returns>Whether the text is empty or a number.</returns>
    public static bool TryParse(string text, out decimal? points)
    {
        points = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();

        if (trimmed.Count(character => character is ',' or '.') > 1)
        {
            return false;
        }

        if (!decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        points = value;

        return true;
    }
}
