using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;

/// <summary>
/// Scores a short text against the accepted answers (white space folded, case as the key says).
/// </summary>
public sealed class ShortTextScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ShortText;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var max = MaxPoints(question, context);
        var value = answer?.Values?.FirstOrDefault();
        var scoring = question.Scoring ?? new SurveyQuestionScoring();

        if (string.IsNullOrWhiteSpace(value))
        {
            return SurveyScoreResult.Of(0, max);
        }

        var right = scoring.AcceptedAnswers.Any(accepted => SurveyScoringMath.Same(accepted, value, scoring.CaseSensitive));

        return right ? SurveyScoreResult.Of(max, max) : new SurveyScoreResult { Points = -Penalty(question, context), MaxPoints = max, Correct = false };
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => string.Join(" / ", question.Scoring?.AcceptedAnswers ?? []);
}
