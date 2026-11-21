namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Computes the difficulty and discrimination of each question from scored responses.
/// </summary>
public static class SurveyItemAnalysisCalculator
{
    /// <summary>
    /// The share of responses in the upper and lower groups of the discrimination index.
    /// </summary>
    public const double GroupShare = 0.27;

    /// <summary>
    /// Analyses the questions of scored responses. Questions without points are left out.
    /// </summary>
    /// <param name="percents">The percent of each scored response, by response id.</param>
    /// <param name="scores">The score of each question in each response.</param>
    /// <returns>One analysis per question.</returns>
    public static IReadOnlyList<SurveyItemAnalysis> Analyze(IReadOnlyDictionary<string, decimal> percents, IEnumerable<SurveyQuestionScore> scores)
    {
        ArgumentNullException.ThrowIfNull(percents);
        ArgumentNullException.ThrowIfNull(scores);

        var ordered = percents.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key).ToList();
        var groupSize = ordered.Count < 2 ? 0 : Math.Max(1, (int)Math.Round(ordered.Count * GroupShare, MidpointRounding.AwayFromZero));
        var upper = ordered.Take(groupSize).ToHashSet(StringComparer.Ordinal);
        var lower = ordered.Skip(ordered.Count - groupSize).ToHashSet(StringComparer.Ordinal);
        var results = new List<SurveyItemAnalysis>();

        var answers = scores
            .Where(score => score.MaxPoints > 0 && percents.ContainsKey(score.ResponseId))
            .Select(score => (score.ResponseId, score.QuestionId, Share: (double)(score.Score / score.MaxPoints)));

        foreach (var question in answers.GroupBy(answer => answer.QuestionId, StringComparer.Ordinal))
        {
            var list = question.ToList();
            var top = list.Where(answer => upper.Contains(answer.ResponseId)).Select(answer => answer.Share).ToList();
            var bottom = list.Where(answer => lower.Contains(answer.ResponseId)).Select(answer => answer.Share).ToList();
            double? discrimination = groupSize > 0 && top.Count > 0 && bottom.Count > 0 ? Math.Round(top.Average() - bottom.Average(), 2) : null;

            results.Add(new SurveyItemAnalysis(question.Key, list.Count, Math.Round(list.Average(answer => answer.Share), 2), discrimination));
        }

        return results;
    }
}
