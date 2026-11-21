using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;

/// <summary>
/// Scores a single choice question: the points of the chosen choice when choices carry points, otherwise all points for
/// a right choice (and the penalty for a wrong one under negative marking).
/// </summary>
public sealed class SingleChoiceScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.SingleChoice;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var scoring = question.Scoring ?? new SurveyQuestionScoring();
        var max = MaxPoints(question, context);
        var chosen = answer?.Values ?? [];

        if (scoring.ChoicePoints is { Count: > 0 } choicePoints)
        {
            var points = chosen.Count == 1 && choicePoints.TryGetValue(chosen[0], out var value) ? value : 0;

            return new SurveyScoreResult { Points = Math.Min(points, max), MaxPoints = max, Correct = max > 0 && points >= max };
        }

        if (chosen.Count == 1 && scoring.CorrectChoiceIds.Contains(chosen[0], StringComparer.Ordinal))
        {
            return SurveyScoreResult.Of(max, max);
        }

        var penalty = chosen.Count > 0 ? Penalty(question, context) : 0;

        return new SurveyScoreResult { Points = -penalty, MaxPoints = max, Correct = false };
    }

    /// <inheritdoc />
    public override decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context)
        => question?.Scoring?.ChoicePoints is { Count: > 0 } choicePoints
            ? Math.Max(choicePoints.Values.Max(), 0)
            : base.MaxPoints(question, context);

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => ChoiceTexts(question, question.Scoring?.CorrectChoiceIds);
}
