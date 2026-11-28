using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;

/// <summary>
/// Scores a ranking against the right order (the key's right choice ids, or the designed order of the items), with
/// partial credit for items in their right place. With "Items to rank" set, the first that many items of the right
/// order are the key.
/// </summary>
public sealed class RankingScorer : SurveyControlScorerBase
{
    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Ranking;

    /// <inheritdoc />
    public override bool AnswerKeyIsOrder => true;

    /// <inheritdoc />
    public override SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(question);

        var max = MaxPoints(question, context);
        var key = RightOrder(question);
        var given = answer?.Values ?? [];

        if (key.Count == 0)
        {
            return SurveyScoreResult.Of(0, max);
        }

        var right = key.Where((id, index) => index < given.Count && string.Equals(given[index], id, StringComparison.Ordinal)).Count();
        var allRight = right == key.Count && given.Count == key.Count;

        if (question.Scoring?.PartialCredit != true)
        {
            return SurveyScoreResult.Of(allRight ? max : 0, max);
        }

        return new SurveyScoreResult { Points = SurveyScoringMath.Points(max * right / key.Count, max), MaxPoints = max, Correct = allRight };
    }

    /// <inheritdoc />
    protected override string RightAnswer(SurveyQuestion question)
        => ChoiceTexts(question, RightOrder(question));

    // The right order of the items the respondent ranks: with "Items to rank" set, only its first items, since the
    // respondent ranks no more than that many.
    private static List<string> RightOrder(SurveyQuestion question)
    {
        var order = question.Scoring?.CorrectChoiceIds is { Count: > 0 } key
            ? key.ToList()
            : question.Choices.Where(choice => choice is not null && !choice.Retired).Select(choice => choice.Id).ToList();

        return question.GetSettings<RankingSettings>().MaxRanked is int maxRanked and > 0 && order.Count > maxRanked
            ? order.Take(maxRanked).ToList()
            : order;
    }
}
