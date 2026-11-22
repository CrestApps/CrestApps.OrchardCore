using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Builds the answer index rows of a completed response.
/// </summary>
public static class SurveyAnswerIndexRows
{
    /// <summary>
    /// Builds the rows of every answer of a response through the handlers of its questions. A scored answer that gives
    /// no value still gets one row with its score, so item analysis counts it.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="definition">The definition of the version the response ran on.</param>
    /// <param name="registry">The control registry.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<SurveyAnswerIndex> Build(SurveyResponse response, SurveyDefinition definition, ISurveyControlRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);

        var questions = SurveyDefinitionQuestions.ById(definition);
        var rows = new List<SurveyAnswerIndex>();
        var completed = DateTime.SpecifyKind(response.CompletedUtc ?? response.ModifiedUtc, DateTimeKind.Utc);

        foreach (var (questionId, answer) in response.Answers)
        {
            if (answer is null || !questions.TryGetValue(questionId, out var question))
            {
                continue;
            }

            var handler = registry.GetHandler(question.ControlType);

            if (handler is null)
            {
                continue;
            }

            var first = true;

            foreach (var value in handler.GetIndexRows(question, answer) ?? [])
            {
                if (value is null)
                {
                    continue;
                }

                rows.Add(Row(response, question, completed, first ? answer : null, value.RowId, value.ChoiceId, value.NumberValue, value.DateValue, value.TextValue));
                first = false;
            }

            if (first && answer.MaxPoints is not null)
            {
                rows.Add(Row(response, question, completed, answer, null, null, null, null, null));
            }
        }

        return rows;
    }

    private static SurveyAnswerIndex Row(
        SurveyResponse response,
        SurveyQuestion question,
        DateTime completed,
        SurveyAnswer scored,
        string rowId,
        string choiceId,
        decimal? number,
        DateTime? date,
        string text)
        => new()
        {
            ResponseId = response.ItemId,
            SurveyId = response.SurveyId,
            VersionNumber = response.VersionNumber,
            QuestionId = Cut(question.Id, SurveyAnswerIndex.KeyLength),
            QuestionName = Cut(question.Name, 100),
            RowId = Cut(rowId, SurveyAnswerIndex.KeyLength),
            ChoiceId = Cut(choiceId, SurveyAnswerIndex.KeyLength),

            // A number too large for the column is left out of the index rather than failing the whole save.
            NumberValue = IndexNumber(number),
            DateValue = date is { } day ? DateTime.SpecifyKind(day, DateTimeKind.Utc) : null,
            TextValue = Cut(text, SurveyAnswerIndex.TextLength),
            Score = scored?.Score,
            MaxPoints = scored?.MaxPoints,
            CompletedUtc = completed,
            CollectorId = response.CollectorId,
            ContextKind = Cut(response.Context?.Kind, 100),
            ContextRef = Cut(response.Context?.ToString(), 255),
        };

    // The column keeps 5 decimals, so the value is rounded first, as the database would: a value just under the limit
    // can round up to it (99,999,999,999,999.999999 becomes 100,000,000,000,000) and overflow the column.
    internal static decimal? IndexNumber(decimal? number)
    {
        if (number is not { } value)
        {
            return null;
        }

        var rounded = Math.Round(value, SurveyAnswerIndex.NumberScale, MidpointRounding.AwayFromZero);

        return Math.Abs(rounded) < SurveyAnswerIndex.NumberLimit ? rounded : null;
    }

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
