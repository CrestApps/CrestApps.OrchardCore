using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;

/// <summary>
/// Scores a multiple choice question: the sum of the chosen choices' points when choices carry points, otherwise all or
/// nothing, or partial credit (right minus wrong over the right choices), with penalties under negative marking.
/// </summary>
public sealed class MultipleChoiceScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.MultipleChoice;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var scoring = question.Scoring ?? new SurveyQuestionScoring();
        var max = MaxPoints(question, context);
        var chosen = (answer?.Values ?? []).ToHashSet(StringComparer.Ordinal);

        if (scoring.ChoicePoints is { Count: > 0 } choicePoints)
        {
            var sum = chosen.Sum(id => choicePoints.GetValueOrDefault(id));
            var floor = context?.Settings?.NegativeMarking == true ? decimal.MinValue : 0;
            var points = Math.Min(Math.Max(sum, floor), max);

            return new SurveyScoreResult { Points = points, MaxPoints = max, Correct = max > 0 && points >= max };
        }

        var key = scoring.CorrectChoiceIds.ToHashSet(StringComparer.Ordinal);

        if (key.Count == 0)
        {
            return SurveyScoreResult.Of(0, max);
        }

        var right = chosen.Count(key.Contains);
        var wrong = chosen.Count - right;
        var penalty = Penalty(question, context) * wrong;

        if (!scoring.PartialCredit)
        {
            var allRight = key.SetEquals(chosen);

            return new SurveyScoreResult { Points = allRight ? max : -penalty, MaxPoints = max, Correct = allRight };
        }

        var earned = SurveyScoringMath.Points(max * (right - wrong) / key.Count, max) - penalty;

        return new SurveyScoreResult { Points = earned, MaxPoints = max, Correct = key.SetEquals(chosen) };
    }

    /// <inheritdoc />
    public override decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context)
        => question?.Scoring?.ChoicePoints is { Count: > 0 } choicePoints
            ? choicePoints.Values.Where(points => points > 0).Sum()
            : base.MaxPoints(question, context);

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => ChoiceTexts(question, question.Scoring?.CorrectChoiceIds);
}
