using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// The analysis of one question across the attempts that drew it.
/// </summary>
/// <param name="QuizId">The quiz.</param>
/// <param name="QuestionId">The question.</param>
/// <param name="Title">The question title, as the latest attempt copied it.</param>
/// <param name="QuestionType">The question type.</param>
/// <param name="Answers">How many scored answers the analysis rests on.</param>
/// <param name="Difficulty">The mean share of the points learners got, from 0 (nobody) to 1 (everybody); higher is easier.</param>
/// <param name="Discrimination">The mean share of the points in the top 27% of attempts minus the bottom 27%, from -1 to 1; or
/// <see langword="null"/> with fewer than two attempts. Below 0.2 the question tells strong and weak learners apart poorly.</param>
public sealed record ItemAnalysis(string QuizId, string QuestionId, string Title, string QuestionType, int Answers, double Difficulty, double? Discrimination);

/// <summary>
/// Computes the difficulty and discrimination of each question from graded attempts.
/// </summary>
public static class ItemAnalysisCalculator
{
    /// <summary>
    /// The share of attempts in the upper and lower groups of the discrimination index.
    /// </summary>
    public const double GroupShare = 0.27;

    /// <summary>
    /// Analyses the questions of graded attempts. Voided and unsubmitted attempts, and answers without a score, are left out.
    /// </summary>
    /// <param name="attempts">The attempts.</param>
    /// <returns>One analysis per quiz and question.</returns>
    public static IReadOnlyList<ItemAnalysis> Analyze(IEnumerable<AssessmentAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        var graded = attempts
            .Where(attempt => attempt.Status == AttemptStatus.Graded && attempt.Percent is not null)
            .ToList();
        var results = new List<ItemAnalysis>();

        foreach (var quiz in graded.GroupBy(attempt => attempt.QuizId, StringComparer.Ordinal))
        {
            var ordered = quiz.OrderByDescending(attempt => attempt.Percent).ToList();
            var groupSize = ordered.Count < 2 ? 0 : Math.Max(1, (int)Math.Round(ordered.Count * GroupShare, MidpointRounding.AwayFromZero));
            var upper = ordered.Take(groupSize).Select(attempt => attempt.ItemId).ToHashSet(StringComparer.Ordinal);
            var lower = ordered.Skip(ordered.Count - groupSize).Select(attempt => attempt.ItemId).ToHashSet(StringComparer.Ordinal);

            var answers = ordered
                .SelectMany(attempt => attempt.Items
                    .Where(item => item.Score is not null && item.Points > 0)
                    .Select(item => (Attempt: attempt, Item: item, Share: (double)(item.Score.Value / item.Points))));

            foreach (var question in answers.GroupBy(answer => answer.Item.QuestionId, StringComparer.Ordinal))
            {
                var list = question.ToList();
                var top = list.Where(answer => upper.Contains(answer.Attempt.ItemId)).Select(answer => answer.Share).ToList();
                var bottom = list.Where(answer => lower.Contains(answer.Attempt.ItemId)).Select(answer => answer.Share).ToList();
                double? discrimination = groupSize > 0 && top.Count > 0 && bottom.Count > 0 ? Math.Round(top.Average() - bottom.Average(), 2) : null;

                results.Add(new ItemAnalysis(
                    quiz.Key,
                    question.Key,
                    list[0].Item.Title,
                    list[0].Item.QuestionType,
                    list.Count,
                    Math.Round(list.Average(answer => answer.Share), 2),
                    discrimination));
            }
        }

        return results;
    }
}
