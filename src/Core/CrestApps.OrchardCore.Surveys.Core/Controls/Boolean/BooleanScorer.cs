using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;

/// <summary>
/// Scores a yes or no question against the key's right value, <c>true</c> or <c>false</c>, read from the right choice
/// ids, then the accepted answers, then <c>Key["answer"]</c> (the importers write the last two).
/// </summary>
public sealed class BooleanScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Boolean;

    /// <summary>
    /// Gets the right value of a yes or no question, <c>true</c> or <c>false</c>, or <see langword="null"/>.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The right value.</returns>
    public static string RightValue(SurveyQuestion question)
    {
        var scoring = question?.Scoring;
        var value = scoring?.CorrectChoiceIds?.FirstOrDefault()
            ?? scoring?.AcceptedAnswers?.FirstOrDefault()
            ?? (scoring?.Key?["answer"] is JsonValue answer ? answer.ToString() : null);

        return value?.Trim().ToLowerInvariant() switch
        {
            "true" => BooleanValues.True,
            "false" => BooleanValues.False,
            _ => null,
        };
    }

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var max = MaxPoints(question, context);
        var key = RightValue(question);
        var value = answer?.Values?.FirstOrDefault();

        if (key is not null && value is not null && string.Equals(key, value, StringComparison.OrdinalIgnoreCase))
        {
            return SurveyScoreResult.Of(max, max);
        }

        return new SurveyScoreResult { Points = value is null ? 0 : -Penalty(question, context), MaxPoints = max, Correct = false };
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => RightValue(question);
}
