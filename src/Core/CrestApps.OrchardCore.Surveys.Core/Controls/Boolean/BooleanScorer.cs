using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;

/// <summary>
/// Scores a yes or no question against the key's right value (<c>true</c> or <c>false</c> in the right choice ids).
/// </summary>
public sealed class BooleanScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Boolean;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var max = MaxPoints(question, context);
        var key = question.Scoring?.CorrectChoiceIds?.FirstOrDefault();
        var value = answer?.Values?.FirstOrDefault();

        if (key is not null && value is not null && string.Equals(key, value, StringComparison.OrdinalIgnoreCase))
        {
            return SurveyScoreResult.Of(max, max);
        }

        return new SurveyScoreResult { Points = value is null ? 0 : -Penalty(question, context), MaxPoints = max, Correct = false };
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => question.Scoring?.CorrectChoiceIds?.FirstOrDefault();
}
