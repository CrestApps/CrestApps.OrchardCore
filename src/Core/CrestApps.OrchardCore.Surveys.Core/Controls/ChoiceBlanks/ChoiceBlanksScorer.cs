using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;

/// <summary>
/// Scores each blank against its right word; each right blank earns its share of the points.
/// </summary>
public sealed class ChoiceBlanksScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ChoiceBlanks;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var settings = question.GetSettings<ChoiceBlanksSettings>();

        return SurveyBlankScoring.Score(SurveyBlankText.Parse(settings.Text).Answers, answer, MaxPoints(question, context), settings.CaseSensitive);
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => string.Join(" / ", SurveyBlankText.Parse(question.GetSettings<ChoiceBlanksSettings>().Text).Answers.Select(answers => SurveyBlankText.First(answers)));
}
