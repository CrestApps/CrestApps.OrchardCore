using System.Text.RegularExpressions;

namespace CrestApps.Support;

public static class StringExtensions
{
    /// <summary>
    /// Sanitizes a string value for safe inclusion in log messages by removing
    /// carriage return and newline characters that could be used for log injection.
    /// </summary>
    public static string SanitizeLogValue(this string value)
        => value?.Replace("\r", "").Replace("\n", "") ?? string.Empty;

    public static bool Like(this string toSearch, string toFind)
    {
        var match = new Regex(@"\.|\$|\^|\{|\[|\(|\||\)|\*|\+|\?|\\").Replace(toFind, ch => @"\" + ch)
                                                                     .Replace('_', '.')
                                                                     .Replace("%", ".*");

        return new Regex(@"\A" + match + @"\z", RegexOptions.Singleline).IsMatch(toSearch);
    }

    public static bool Like(this string toSearch, string toFind, StringComparison comparison)
    {
        if (comparison == StringComparison.CurrentCultureIgnoreCase
            || comparison == StringComparison.OrdinalIgnoreCase
            || comparison == StringComparison.InvariantCultureIgnoreCase)
        {
            return Like(toSearch.ToLower(), toFind.ToLower());
        }

        return Like(toSearch, toFind);
    }

    public static string GetControllerName(this string name)
    {
        return Str.TrimEnd(name, "Controller", StringComparison.OrdinalIgnoreCase);
    }
}
