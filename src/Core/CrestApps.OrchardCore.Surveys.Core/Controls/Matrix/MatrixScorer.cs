using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;

/// <summary>
/// Scores a matrix against the right column of each row (the key's pairs), with partial credit when asked.
/// </summary>
public sealed class MatrixScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Matrix;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        return SurveyRowAnswers.Score(question.Scoring?.Pairs, answer, MaxPoints(question, context), question.Scoring?.PartialCredit == true);
    }
}
