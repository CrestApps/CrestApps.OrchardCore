using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Splits a text with <c>[[answer|other answer]]</c> blanks into the segments respondents see and the answers of each
/// blank, which stay on the server.
/// </summary>
public static partial class SurveyBlankText
{
    /// <summary>
    /// Splits a text into segments and blank answers. There is always one more segment than blanks.
    /// </summary>
    /// <param name="text">The text with blanks.</param>
    /// <returns>The segments and the answers of each blank (alternatives split on <c>|</c>).</returns>
    public static (IReadOnlyList<string> Segments, IReadOnlyList<IReadOnlyList<string>> Answers) Parse(string text)
    {
        text ??= string.Empty;

        var segments = new List<string>();
        var answers = new List<IReadOnlyList<string>>();
        var position = 0;

        foreach (Match match in Blank().Matches(text))
        {
            segments.Add(text.Substring(position, match.Index - position));
            answers.Add(match.Groups["answers"].Value
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList());
            position = match.Index + match.Length;
        }

        segments.Add(text.Substring(position));

        return (segments, answers);
    }

    [GeneratedRegex(@"\[\[(?<answers>[^\]]+)\]\]")]
    private static partial Regex Blank();
}
