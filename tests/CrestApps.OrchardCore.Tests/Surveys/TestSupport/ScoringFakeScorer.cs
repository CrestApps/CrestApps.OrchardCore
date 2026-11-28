using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A scorer for tests: the answer's number is its points, and an answer whose first value is "manual" needs a grader.
/// </summary>
internal sealed class ScoringFakeScorer : ISurveyControlScorer
{
    public const string Type = "ScoringFake";

    public string ControlType => Type;

    public SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        var max = MaxPoints(question, context);

        if (answer?.Values.FirstOrDefault() == "manual")
        {
            return SurveyScoreResult.Manual(max);
        }

        return SurveyScoreResult.Of(Math.Min(answer?.Number ?? 0, max), max);
    }

    public decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context)
        => context.PointsOf(question);

    public SurveyAnswerFeedback GetFeedback(SurveyQuestion question, SurveyAnswer answer)
        => new();
}
