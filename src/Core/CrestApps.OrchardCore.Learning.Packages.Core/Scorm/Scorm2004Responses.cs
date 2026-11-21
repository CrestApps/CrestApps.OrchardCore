namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The formats of <c>cmi.interactions.n.learner_response</c> and <c>cmi.interactions.n.correct_responses.n.pattern</c>
/// per interaction type (RTE 4.2.9): records separated by <c>[,]</c>, pairs by <c>[.]</c>, numeric ranges by <c>[:]</c>,
/// and the <c>{case_matters=}</c> and <c>{order_matters=}</c> delimiters a pattern may start with.
/// </summary>
/// <remarks>
/// Every type is checked for its structure and its identifiers; the finer rules of performance steps (a numeric range
/// as a step answer) are accepted as text. A numeric pattern is <c>min[:]max</c>, either end optional; a single number
/// is also taken.
/// </remarks>
internal static class Scorm2004Responses
{
    private const string RecordSeparator = "[,]";
    private const string PairSeparator = "[.]";
    private const string RangeSeparator = "[:]";

    /// <summary>
    /// Gets whether a value is a learner response, or a correct response pattern, of an interaction type.
    /// </summary>
    /// <param name="type">The interaction type.</param>
    /// <param name="value">The value.</param>
    /// <param name="pattern">Whether the value is a correct response pattern.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsValid(string type, string value, bool pattern)
    {
        switch (type)
        {
            case "true-false":
                return value is "true" or "false";

            case "choice":
                {
                    if (value.Length == 0)
                    {
                        return true;
                    }

                    var ids = value.Split(RecordSeparator);

                    return ids.All(IsShortIdentifier) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length;
                }

            case "fill-in":
                {
                    var rest = pattern ? StripFlags(value, caseMatters: true, orderMatters: true) : value;

                    return rest is not null && rest.Split(RecordSeparator).All(text => Scorm2004DataModel.IsLocalizedString(text, 250));
                }

            case "long-fill-in":
                {
                    var rest = pattern ? StripFlags(value, caseMatters: true, orderMatters: false) : value;

                    return rest is not null && Scorm2004DataModel.IsLocalizedString(rest, 4000);
                }

            case "likert":
                return IsShortIdentifier(value);

            case "matching":
                return value.Split(RecordSeparator).All(record =>
                {
                    var pair = record.Split(PairSeparator);

                    return pair.Length == 2 && IsShortIdentifier(pair[0]) && IsShortIdentifier(pair[1]);
                });

            case "performance":
                {
                    var rest = pattern ? StripFlags(value, caseMatters: false, orderMatters: true) : value;

                    return rest is not null && rest.Split(RecordSeparator).All(record =>
                    {
                        var pair = record.Split(PairSeparator);

                        return pair.Length == 2 &&
                            (pair[0].Length > 0 || pair[1].Length > 0) &&
                            (pair[0].Length == 0 || IsShortIdentifier(pair[0])) &&
                            pair[1].Length <= 250;
                    });
                }

            case "sequencing":
                return value.Length > 0 && value.Split(RecordSeparator).All(IsShortIdentifier);

            case "numeric":
                return pattern ? IsNumericRange(value) : Scorm2004DataModel.TryParseReal(value, out _);

            case "other":
                return value.Length <= 4000;

            default:
                return false;
        }
    }

    private static bool IsShortIdentifier(string value)
        => Scorm2004DataModel.IsIdentifier(value, 250);

    private static bool IsNumericRange(string value)
    {
        var parts = value.Split(RangeSeparator);

        if (parts.Length == 1)
        {
            return Scorm2004DataModel.TryParseReal(value, out _);
        }

        if (parts.Length != 2 || (parts[0].Length == 0 && parts[1].Length == 0))
        {
            return false;
        }

        decimal min = 0, max = 0;

        if ((parts[0].Length > 0 && !Scorm2004DataModel.TryParseReal(parts[0], out min)) ||
            (parts[1].Length > 0 && !Scorm2004DataModel.TryParseReal(parts[1], out max)))
        {
            return false;
        }

        return parts[0].Length == 0 || parts[1].Length == 0 || min <= max;
    }

    // Takes off the {case_matters=true|false} and {order_matters=true|false} delimiters a pattern may start with, each
    // once; null when one is malformed or not allowed for the type.
    private static string StripFlags(string value, bool caseMatters, bool orderMatters)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rest = value;

        while (true)
        {
            string name = null;

            if (caseMatters && rest.StartsWith("{case_matters=", StringComparison.Ordinal))
            {
                name = "case_matters";
            }
            else if (orderMatters && rest.StartsWith("{order_matters=", StringComparison.Ordinal))
            {
                name = "order_matters";
            }

            if (name is null)
            {
                return rest;
            }

            var end = rest.IndexOf('}', StringComparison.Ordinal);

            if (end < 0 || !seen.Add(name) || rest[(name.Length + 2)..end] is not ("true" or "false"))
            {
                return null;
            }

            rest = rest[(end + 1)..];
        }
    }
}
