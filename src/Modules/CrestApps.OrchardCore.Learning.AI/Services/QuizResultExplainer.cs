using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// A learner's quiz attempt as the explain-my-result tool returns it.
/// </summary>
public sealed class QuizResultExplanation
{
    /// <summary>
    /// Gets or sets the quiz title.
    /// </summary>
    public string Quiz { get; set; }

    /// <summary>
    /// Gets or sets which attempt of the learner it is.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the attempt's state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the score, when the learner may see it.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the most points, when the learner may see the score.
    /// </summary>
    public decimal? MaxScore { get; set; }

    /// <summary>
    /// Gets or sets the score in percent, when the learner may see it.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets whether the attempt passed, when the learner may see the score.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets the pass mark in percent, when the learner may see the score.
    /// </summary>
    public decimal? PassMark { get; set; }

    /// <summary>
    /// Gets or sets whether the learner may review the answers now.
    /// </summary>
    public bool ReviewAvailable { get; set; }

    /// <summary>
    /// Gets or sets what the model should tell the learner about what is not shown.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the grader's overall feedback, when the learner may review the attempt.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the questions with the learner's answers, only when the learner may review the attempt.
    /// </summary>
    public IList<QuizQuestionExplanation> Questions { get; set; } = [];
}

/// <summary>
/// One question of a reviewed attempt.
/// </summary>
public sealed class QuizQuestionExplanation
{
    /// <summary>
    /// Gets or sets the question's number in the attempt.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the question's prompt, as text.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the learner's answer, as text.
    /// </summary>
    public string YourAnswer { get; set; }

    /// <summary>
    /// Gets or sets the right answer, as text.
    /// </summary>
    public string RightAnswer { get; set; }

    /// <summary>
    /// Gets or sets the points the question was worth.
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// Gets or sets the points the learner got.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the feedback: the question's, the chosen options' and the grader's comment.
    /// </summary>
    public string Feedback { get; set; }
}

/// <summary>
/// Explains a learner's quiz attempt only as far as the quiz's review policy allows: the score when the policy shows
/// it, and the questions, answers, right answers and feedback only when the learner may review the attempt. An answer
/// key never leaves an attempt the learner may not review.
/// </summary>
internal static partial class QuizResultExplainer
{
    /// <summary>
    /// Describes an attempt.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="canReview">Whether the review policy lets the learner see the answers now.</param>
    /// <param name="canSeeScore">Whether the review policy lets the learner see the score.</param>
    /// <returns>The explanation.</returns>
    public static QuizResultExplanation Describe(AssessmentAttempt attempt, bool canReview, bool canSeeScore)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var explanation = new QuizResultExplanation
        {
            Quiz = attempt.QuizTitle,
            Attempt = attempt.Sequence,
            Status = attempt.Status.ToString(),
            ReviewAvailable = canReview,
        };

        if (attempt.Status is AttemptStatus.InProgress)
        {
            explanation.Message = "The attempt is still in progress, so there is no result yet.";

            return explanation;
        }

        if (attempt.Status is AttemptStatus.Submitted or AttemptStatus.NeedsGrading)
        {
            explanation.Message = "The attempt is waiting for a grader, so there is no result yet.";

            return explanation;
        }

        if (attempt.Status is AttemptStatus.Voided or AttemptStatus.Abandoned)
        {
            explanation.Message = "The attempt was cancelled and does not count.";

            return explanation;
        }

        if (canSeeScore || canReview)
        {
            explanation.Score = attempt.Score;
            explanation.MaxScore = attempt.MaxScore;
            explanation.Percent = attempt.Percent;
            explanation.Passed = attempt.Passed;
            explanation.PassMark = attempt.PassMark;
        }

        if (!canReview)
        {
            explanation.Message = canSeeScore
                ? "The quiz's review policy does not let the learner see the questions, answers or feedback now. Explain the score only, and do not guess what the right answers were."
                : "The quiz's review policy hides the result from the learner. Say that the result is not available, and do not guess it.";

            return explanation;
        }

        explanation.Feedback = attempt.Feedback;

        foreach (var item in attempt.Items.OrderBy(item => item.Index))
        {
            var chosen = item.Response?.ChoiceIds ?? [];
            var feedback = new List<string>();

            if (!string.IsNullOrWhiteSpace(item.Feedback))
            {
                feedback.Add(Text(item.Feedback));
            }

            feedback.AddRange(item.Choices
                .Where(choice => chosen.Contains(choice.Id) && !string.IsNullOrWhiteSpace(choice.Feedback))
                .Select(choice => Text(choice.Feedback)));

            if (!string.IsNullOrWhiteSpace(item.GraderComment))
            {
                feedback.Add(item.GraderComment.Trim());
            }

            explanation.Questions.Add(new QuizQuestionExplanation
            {
                Number = item.Index + 1,
                Prompt = Text(item.Prompt) ?? item.Title,
                YourAnswer = DescribeResponse(item),
                RightAnswer = DescribeKey(item),
                Points = item.Points,
                Score = item.Score,
                Feedback = feedback.Count == 0 ? null : string.Join(" ", feedback),
            });
        }

        return explanation;
    }

    private static string DescribeResponse(AttemptItem item)
    {
        var response = item.Response;

        if (response is null)
        {
            return null;
        }

        if (response.ChoiceIds.Count > 0)
        {
            return string.Join("; ", response.ChoiceIds.Select(id => item.Choices.FirstOrDefault(choice => choice.Id == id)?.Text ?? id));
        }

        if (response.Pairs.Count > 0)
        {
            return string.Join("; ", response.Pairs.Select(pair => (item.Choices.FirstOrDefault(choice => choice.Id == pair.Key)?.Text ?? pair.Key) + " = " + (item.Answers.FirstOrDefault(answer => answer.Id == pair.Value)?.Text ?? pair.Value)));
        }

        if (response.Number is { } number)
        {
            return Number(number);
        }

        return response.Texts.Count == 0 ? null : string.Join("; ", response.Texts);
    }

    private static string DescribeKey(AttemptItem item)
    {
        var key = item.Key;

        if (key is null)
        {
            return null;
        }

        if (key.ChoiceIds.Count > 0)
        {
            return string.Join("; ", key.ChoiceIds.Select(id => item.Choices.FirstOrDefault(choice => choice.Id == id)?.Text ?? id));
        }

        if (key.Pairs.Count > 0)
        {
            return string.Join("; ", key.Pairs.Select(pair => (item.Choices.FirstOrDefault(choice => choice.Id == pair.Key)?.Text ?? pair.Key) + " = " + (item.Answers.FirstOrDefault(answer => answer.Id == pair.Value)?.Text ?? pair.Value)));
        }

        if (key.Number is { } number)
        {
            return key.Tolerance > 0 ? $"{Number(number)} (within {Number(key.Tolerance)})" : Number(number);
        }

        return key.Texts.Count == 0 ? null : string.Join("; ", key.Texts);
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    internal static string Text(string html)
        => string.IsNullOrWhiteSpace(html) ? null : WebUtility.HtmlDecode(TagPattern().Replace(html, " ")).Trim();

    internal static string Number(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
