using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Shows the source kind of a check or disbursement, a technical name such as <c>PayStatement</c>, as words people read,
/// such as "Pay statement".
/// </summary>
internal static partial class SourceKindText
{
    /// <summary>
    /// Splits a source kind into words: the first keeps its capital, acronyms such as ACH stay capitals, the rest are
    /// lower case.
    /// </summary>
    /// <param name="sourceKind">The source kind.</param>
    /// <returns>The words, or the value as it is when it isn't a single technical name.</returns>
    public static string Describe(string sourceKind)
    {
        if (string.IsNullOrWhiteSpace(sourceKind) || sourceKind.Any(char.IsWhiteSpace))
        {
            return sourceKind;
        }

        var words = Words().Matches(sourceKind).Select(match => match.Value).ToList();

        if (words.Count == 0)
        {
            return sourceKind;
        }

        return string.Join(' ', words.Select((word, index) =>
            index == 0 || (word.Length > 1 && word.All(char.IsUpper)) ? word : word.ToLowerInvariant()));
    }

    [GeneratedRegex("[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")]
    private static partial Regex Words();
}
