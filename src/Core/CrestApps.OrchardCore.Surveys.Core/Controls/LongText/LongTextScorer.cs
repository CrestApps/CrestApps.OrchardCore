using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.LongText;

/// <summary>
/// Leaves a written answer to a grader; an empty answer scores 0.
/// </summary>
public sealed class LongTextScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.LongText;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        var max = MaxPoints(question, context);

        return string.IsNullOrWhiteSpace(answer?.Values?.FirstOrDefault())
            ? SurveyScoreResult.Of(0, max)
            : SurveyScoreResult.Manual(max);
    }
}
