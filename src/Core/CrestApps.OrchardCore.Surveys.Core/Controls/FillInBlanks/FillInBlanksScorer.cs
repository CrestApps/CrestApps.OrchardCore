using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;

/// <summary>
/// Scores each blank against its accepted answers; each right blank earns its share of the points.
/// </summary>
public sealed class FillInBlanksScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.FillInBlanks;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var settings = question.GetSettings<FillInBlanksSettings>();

        return SurveyBlankScoring.Score(SurveyBlankText.Parse(settings.Text).Answers, answer, MaxPoints(question, context), settings.CaseSensitive);
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => string.Join(" / ", SurveyBlankText.Parse(question.GetSettings<FillInBlanksSettings>().Text).Answers.Select(answers => SurveyBlankText.First(answers)));
}
