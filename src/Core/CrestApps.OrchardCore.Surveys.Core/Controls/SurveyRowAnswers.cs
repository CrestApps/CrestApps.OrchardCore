using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Reads and scores answers kept per row (<c>SurveyAnswer.Rows</c>): matrices and matching questions post one input per
/// row, named <c>q.{questionId}.{rowId}</c>.
/// </summary>
public static class SurveyRowAnswers
{
    /// <summary>
    /// Reads the choices posted for each active row.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <param name="allowedChoiceIds">The choice ids a row may hold.</param>
    /// <param name="multiple">Whether a row may hold several choices.</param>
    /// <returns>The rows that hold a value, by row id.</returns>
    public static Dictionary<string, List<string>> Read(SurveyAnswerReadContext context, IReadOnlySet<string> allowedChoiceIds, bool multiple)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in context.Question.Rows.Where(row => row is not null && !row.Retired && !string.IsNullOrEmpty(row.Id)))
        {
            var values = ReadRaw(context, $"{context.Prefix}.{row.Id}")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Where(value => allowedChoiceIds is null || allowedChoiceIds.Contains(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (!multiple && values.Count > 1)
            {
                values = [values[0]];
            }

            if (values.Count > 0)
            {
                rows[row.Id] = values;
            }
        }

        return rows;
    }

    /// <summary>
    /// Reads the raw values posted under a name, blanks included.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <param name="name">The input name.</param>
    /// <returns>The values.</returns>
    public static IReadOnlyList<string> ReadRaw(SurveyAnswerReadContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Form is not null)
        {
            return context.Form.TryGetValue(name, out var posted) ? posted.Select(value => value ?? string.Empty).ToList() : [];
        }

        if (context.Json is System.Text.Json.Nodes.JsonObject json && name.StartsWith(context.Prefix + ".", StringComparison.Ordinal))
        {
            var node = json[name.Substring(context.Prefix.Length + 1)];

            return node switch
            {
                System.Text.Json.Nodes.JsonArray array => array.Select(item => item?.ToString() ?? string.Empty).ToList(),
                null => [],
                _ => [node.ToString()],
            };
        }

        return [];
    }

    /// <summary>
    /// Scores row answers against the right choice of each row.
    /// </summary>
    /// <param name="pairs">The right choice of each row, by row id.</param>
    /// <param name="answer">The answer.</param>
    /// <param name="max">The points possible.</param>
    /// <param name="partialCredit">Whether partly right answers earn part of the points.</param>
    /// <returns>The result.</returns>
    public static SurveyScoreResult Score(IReadOnlyDictionary<string, string> pairs, SurveyAnswer answer, decimal max, bool partialCredit)
    {
        if (pairs is null || pairs.Count == 0)
        {
            return SurveyScoreResult.Of(0, max);
        }

        var right = pairs.Count(pair => answer?.Rows is not null
            && answer.Rows.TryGetValue(pair.Key, out var values)
            && values is { Count: 1 }
            && string.Equals(values[0], pair.Value, StringComparison.Ordinal));

        if (!partialCredit)
        {
            return SurveyScoreResult.Of(right == pairs.Count ? max : 0, max);
        }

        return new SurveyScoreResult { Points = SurveyScoringMath.Points(max * right / pairs.Count, max), MaxPoints = max, Correct = right == pairs.Count };
    }
}
