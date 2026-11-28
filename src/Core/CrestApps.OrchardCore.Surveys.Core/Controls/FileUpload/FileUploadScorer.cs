using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// Leaves uploaded files to a grader; no file scores 0.
/// </summary>
public sealed class FileUploadScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.FileUpload;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        var max = MaxPoints(question, context);

        return answer?.AttachmentIds?.Count > 0 ? SurveyScoreResult.Manual(max) : SurveyScoreResult.Of(0, max);
    }
}
