using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

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
/// Describes a learner's quiz attempt (a response to the quiz's survey) for the AI to explain, showing only what the
/// survey's review settings let the learner see.
/// </summary>
internal static partial class QuizResultExplainer
{
    /// <summary>
    /// Describes an attempt.
    /// </summary>
    /// <param name="response">The attempt.</param>
    /// <param name="definition">The definition of the version it ran on.</param>
    /// <param name="quizTitle">The title of the quiz.</param>
    /// <param name="shows">What the review settings let the learner see now.</param>
    /// <param name="registry">The question types.</param>
    /// <returns>The description.</returns>
    public static QuizResultExplanation Describe(SurveyResponse response, SurveyDefinition definition, string quizTitle, ReviewShows shows, ISurveyControlRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(response);

        var canReview = (shows & ReviewShows.Responses) != 0;
        var canSeeScore = (shows & ReviewShows.Score) != 0;
        var explanation = new QuizResultExplanation
        {
            Quiz = quizTitle,
            Attempt = response.AttemptNumber,
            Status = response.Status.ToString(),
            ReviewAvailable = canReview,
        };

        if (response.Status == SurveyResponseStatus.InProgress)
        {
            explanation.Message = "The attempt is still in progress, so there is no result yet.";

            return explanation;
        }

        if (response.Status is SurveyResponseStatus.Voided or SurveyResponseStatus.Abandoned)
        {
            explanation.Message = "The attempt was cancelled and does not count.";

            return explanation;
        }

        if (response.Grading == GradingStatus.Pending)
        {
            explanation.Message = "The attempt is waiting for a grader, so there is no result yet.";

            return explanation;
        }

        // The score, percent and pass/fail follow the score setting alone: a review that shows the answers but hides the
        // score must not give the score away through the model.
        if (canSeeScore)
        {
            explanation.Score = response.Score;
            explanation.MaxScore = response.MaxScore;
            explanation.Percent = response.Percent;
            explanation.Passed = response.Passed;
            explanation.PassMark = definition?.Scoring?.PassMarkPercent;
        }

        if (!canReview)
        {
            explanation.Message = canSeeScore
                ? "The quiz's review settings do not let the learner see the questions, answers or feedback now. Explain the score only, and do not guess what the right answers were."
                : "The quiz's review settings hide the result from the learner. Say that the result is not available, and do not guess it.";

            return explanation;
        }

        explanation.Feedback = response.Feedback;

        if (!canSeeScore)
        {
            explanation.Message = "The quiz's review settings hide the score from the learner. Explain the answers and feedback only, and do not work out or guess the score, the percent or whether the attempt passed.";
        }

        var format = new SurveyFormatContext { Definition = definition, Culture = CultureInfo.InvariantCulture };
        var shown = response.Plan?.QuestionOrder?.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal) ?? [];
        var number = 0;

        foreach (var question in definition?.Pages.SelectMany(page => page.Questions) ?? [])
        {
            if (shown.Count > 0 && !shown.Contains(question.Id))
            {
                continue;
            }

            var handler = registry?.GetHandler(question.ControlType);
            var descriptor = registry?.GetControl(question.ControlType);

            if (handler is null || descriptor is null || !descriptor.Has(SurveyControlCapabilities.Answerable))
            {
                continue;
            }

            response.Answers.TryGetValue(question.Id, out var answer);

            var feedback = new List<string>();
            var rightAnswer = (string)null;

            if (registry.GetScorer(question.ControlType) is { } scorer && scorer.GetFeedback(question, answer) is { } given)
            {
                if ((shows & ReviewShows.RightAnswers) != 0)
                {
                    rightAnswer = given.RightAnswer;
                }

                if ((shows & ReviewShows.Feedback) != 0)
                {
                    if (!string.IsNullOrWhiteSpace(given.Feedback))
                    {
                        feedback.Add(Text(given.Feedback));
                    }

                    feedback.AddRange(given.ChoiceFeedback.Values.Where(text => !string.IsNullOrWhiteSpace(text)).Select(Text));
                }

                if ((shows & ReviewShows.GeneralFeedback) != 0 && !string.IsNullOrWhiteSpace(given.GeneralFeedback))
                {
                    feedback.Add(Text(given.GeneralFeedback));
                }
            }

            if (!string.IsNullOrWhiteSpace(answer?.GraderComment))
            {
                feedback.Add(answer.GraderComment.Trim());
            }

            explanation.Questions.Add(new QuizQuestionExplanation
            {
                Number = ++number,
                Prompt = Text(question.Title),
                YourAnswer = answer is null ? null : handler.FormatAnswer(question, answer, format),
                RightAnswer = rightAnswer,
                Points = answer?.MaxPoints ?? 0,
                Score = canSeeScore ? answer?.Score : null,
                Feedback = feedback.Count == 0 ? null : string.Join(" ", feedback),
            });
        }

        return explanation;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    internal static string Text(string html)
        => string.IsNullOrWhiteSpace(html) ? null : WebUtility.HtmlDecode(TagPattern().Replace(html, " ")).Trim();

    internal static string Number(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
