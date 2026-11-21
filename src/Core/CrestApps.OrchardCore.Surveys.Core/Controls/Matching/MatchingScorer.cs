using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matching;

/// <summary>
/// Scores a matching question against the right pairs, with partial credit when asked.
/// </summary>
public sealed class MatchingScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Matching;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        return SurveyRowAnswers.Score(question.Scoring?.Pairs, answer, MaxPoints(question, context), question.Scoring?.PartialCredit == true);
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => string.Join("; ", (question.Scoring?.Pairs ?? [])
            .Select(pair => $"{question.Rows.FirstOrDefault(row => row?.Id == pair.Key)?.Text ?? pair.Key} = {question.Choices.FirstOrDefault(choice => choice?.Id == pair.Value)?.Text ?? pair.Value}"));
}
