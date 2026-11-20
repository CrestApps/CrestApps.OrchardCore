using CrestApps.OrchardCore.Learning.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.Services;

/// <summary>
/// Takes a learner's answers to a survey and gives staff the totals. Every write runs under one lock per enrollment
/// and survey, and reports the answer to Learning, so an item that completes on the answer completes.
/// </summary>
public interface ISurveyResponseService
{
    /// <summary>
    /// Takes a learner's answers under their own active enrollment in the course of the survey, within the
    /// enrollment's access dates and once the item's release rules open it, before the survey closes. A learner answers
    /// once, unless the survey lets them change their answers, which then replace the earlier ones.
    /// </summary>
    /// <param name="request">The learner, the survey and the answers.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response, or why it was refused.</returns>
    Task<SurveyResponseResult> AnswerAsync(SurveyAnswerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a learner's response to a survey.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    Task<SurveyResponse> FindAsync(string enrollmentId, string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the answers of a survey: each choice, the average rating and the written answers.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The totals.</returns>
    Task<SurveySummary> SummarizeAsync(string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides whether a person may see how everyone answered a survey, when the survey shows its totals to learners:
    /// course staff, or a learner with an open enrollment in the survey's course, within its access dates, to whom the
    /// item is released. Nobody signed out, not enrolled or not yet given the item sees them.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The person, or <see langword="null"/> when signed out.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the person may see the totals.</returns>
    Task<bool> CanSeeResultsAsync(string surveyId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of the responses to a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(string surveyId, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// A learner's answers.
/// </summary>
public sealed class SurveyAnswerRequest
{
    /// <summary>
    /// Gets or sets the learner; their own active enrollment in the course of the survey is used.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the survey course item.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the answers by question id.
    /// </summary>
    public IDictionary<string, IList<string>> Answers { get; set; } = new Dictionary<string, IList<string>>(StringComparer.Ordinal);
}

/// <summary>
/// What an answer did.
/// </summary>
public sealed class SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets why the answers were refused: one of <see cref="SurveyErrors"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the question the error is about, when there is one.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets whether the answers were taken.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Creates a refusal.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <param name="questionId">The question, when there is one.</param>
    /// <returns>The result.</returns>
    public static SurveyResponseResult Fail(string error, string questionId = null)
        => new() { Error = error, QuestionId = questionId };
}

/// <summary>
/// Why answers were refused.
/// </summary>
public static class SurveyErrors
{
    /// <summary>The item is not a survey of a course.</summary>
    public const string NotSurvey = "NotSurvey";

    /// <summary>The learner has no active enrollment in the course.</summary>
    public const string NotEnrolled = "NotEnrolled";

    /// <summary>The learner's access to the course ended.</summary>
    public const string AccessEnded = "AccessEnded";

    /// <summary>A release rule keeps the survey locked.</summary>
    public const string NotAvailableYet = "NotAvailableYet";

    /// <summary>The survey stopped taking answers.</summary>
    public const string Closed = "Closed";

    /// <summary>The learner answered, and the survey does not let them change their answers.</summary>
    public const string AlreadyAnswered = "AlreadyAnswered";

    /// <summary>A required question has no answer; the result names it.</summary>
    public const string Required = "Required";

    /// <summary>An answer is not one the question takes; the result names the question.</summary>
    public const string InvalidAnswer = "InvalidAnswer";

    /// <summary>Another change is being made.</summary>
    public const string Busy = "Busy";
}

/// <summary>
/// The totals of a survey.
/// </summary>
public sealed class SurveySummary
{
    /// <summary>
    /// Gets or sets how many learners answered.
    /// </summary>
    public int ResponseCount { get; set; }

    /// <summary>
    /// Gets or sets the totals of each question, in the survey's order.
    /// </summary>
    public IList<SurveyQuestionSummary> Questions { get; set; } = [];
}

/// <summary>
/// The totals of one question.
/// </summary>
public sealed class SurveyQuestionSummary
{
    /// <summary>
    /// Gets or sets the question id.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer.
    /// </summary>
    public SurveyQuestionType Type { get; set; }

    /// <summary>
    /// Gets or sets how many learners answered it.
    /// </summary>
    public int AnswerCount { get; set; }

    /// <summary>
    /// Gets or sets how many learners chose each choice, or gave each rating, in the question's order.
    /// </summary>
    public IDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the average rating, or <see langword="null"/> when it is not a rating or nobody rated.
    /// </summary>
    public decimal? Average { get; set; }

    /// <summary>
    /// Gets or sets the written answers.
    /// </summary>
    public IList<string> TextAnswers { get; set; } = [];
}
