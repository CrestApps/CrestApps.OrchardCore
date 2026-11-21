using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Number;

/// <summary>
/// Scores a number against the right number, within the tolerance.
/// </summary>
public sealed class NumberScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Number;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
        => SurveyNumberScoring.Score(question, answer, MaxPoints(question, context), Penalty(question, context));

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => question.Scoring?.NumberAnswer?.ToString(CultureInfo.CurrentCulture);
}
