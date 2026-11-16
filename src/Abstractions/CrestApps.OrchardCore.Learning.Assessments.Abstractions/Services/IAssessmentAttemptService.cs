using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Runs attempts on the server: it draws the questions, fixes the deadline, saves each answer, accepts the submit once,
/// submits at the deadline, grades, regrades and voids.
/// </summary>
public interface IAssessmentAttemptService
{
    /// <summary>
    /// Starts an attempt, or returns the one in progress.
    /// </summary>
    /// <param name="request">What to start.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> StartAsync(StartAttemptRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the answer of one item of an attempt in progress.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="userId">The learner, who must own the attempt.</param>
    /// <param name="index">The item position.</param>
    /// <param name="response">The answer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> SaveResponseAsync(string attemptId, string userId, int index, AttemptResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an attempt. A second submit changes nothing.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="userId">The learner, who must own the attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> SubmitAsync(string attemptId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an attempt whose deadline passed.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> SubmitAtDeadlineAsync(string attemptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the scores a grader gave.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="scores">The score and comment of each item, by position.</param>
    /// <param name="feedback">The overall feedback.</param>
    /// <param name="actor">The grader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> GradeAsync(string attemptId, IDictionary<int, ManualScore> scores, string feedback, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores the attempt again from its own items, such as after a key was corrected on it, keeping the old score.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="actor">Who.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> RegradeAsync(string attemptId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids an attempt; it no longer counts.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="actor">Who.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttemptResult> VoidAsync(string attemptId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Says whether the learner may see the answers and feedback of an attempt now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> when the review policy allows it.</returns>
    bool CanReview(AssessmentAttempt attempt);

    /// <summary>
    /// Moves the close date of a quiz on its attempts, so the review that waits for the quiz to close follows the date
    /// the quiz closes now. Only attempts that kept the quiz's previous close date move; an attempt that took its close
    /// date from the learner's override keeps it.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="previousClosesUtc">The close date the quiz had, in UTC, or <see langword="null"/>.</param>
    /// <param name="closesUtc">The close date the quiz has now, in UTC, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many attempts moved.</returns>
    Task<int> MoveQuizCloseAsync(string quizId, DateTime? previousClosesUtc, DateTime? closesUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to start an attempt.
/// </summary>
public sealed class StartAttemptRequest
{
    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the signed-in person, for the Learning access decision.
    /// </summary>
    public ClaimsPrincipal User { get; set; }

    /// <summary>
    /// Gets or sets the quiz.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the access code typed by the learner.
    /// </summary>
    public string AccessCode { get; set; }

    /// <summary>
    /// Gets or sets whether the learner confirmed the honor statement.
    /// </summary>
    public bool HonorConfirmed { get; set; }
}

/// <summary>
/// A score a grader gives to one item.
/// </summary>
public sealed class ManualScore
{
    /// <summary>
    /// Gets or sets whether the grader used scoring help (a suggestion from AI) for this score.
    /// </summary>
    public bool AIAssisted { get; set; }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// Gets or sets the comment.
    /// </summary>
    public string Comment { get; set; }
}

/// <summary>
/// The result of an attempt operation.
/// </summary>
public sealed class AttemptResult
{
    /// <summary>
    /// Gets or sets the attempt.
    /// </summary>
    public AssessmentAttempt Attempt { get; set; }

    /// <summary>
    /// Gets or sets the error, one of <see cref="AttemptErrors"/>, or <see langword="null"/>.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets when the learner may start again, for <see cref="AttemptErrors.TooSoon"/>.
    /// </summary>
    public DateTime? AvailableFromUtc { get; set; }

    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(ErrorCode);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">The error.</param>
    /// <returns>The result.</returns>
    public static AttemptResult Fail(string errorCode)
        => new() { ErrorCode = errorCode };
}

/// <summary>
/// Why an attempt operation failed.
/// </summary>
public static class AttemptErrors
{
    /// <summary>The attempt or quiz was not found, or is not the learner own.</summary>
    public const string NotFound = "NotFound";

    /// <summary>Someone else was changing the attempt at that moment; try again.</summary>
    public const string Busy = "Busy";

    /// <summary>Nobody grades, regrades or voids their own attempt.</summary>
    public const string OwnAttempt = "OwnAttempt";

    /// <summary>The learner may not open the quiz.</summary>
    public const string NoAccess = "NoAccess";

    /// <summary>The quiz is not open now.</summary>
    public const string Closed = "Closed";

    /// <summary>The learner used every attempt.</summary>
    public const string NoAttemptsLeft = "NoAttemptsLeft";

    /// <summary>The learner must wait before the next attempt.</summary>
    public const string TooSoon = "TooSoon";

    /// <summary>The access code is wrong.</summary>
    public const string WrongAccessCode = "WrongAccessCode";

    /// <summary>Too many wrong access codes; the learner waits before trying again.</summary>
    public const string TooManyAttempts = "TooManyAttempts";

    /// <summary>The honor statement was not confirmed.</summary>
    public const string HonorRequired = "HonorRequired";

    /// <summary>The attempt is no longer in progress.</summary>
    public const string NotInProgress = "NotInProgress";

    /// <summary>The deadline passed.</summary>
    public const string DeadlinePassed = "DeadlinePassed";

    /// <summary>The item cannot be answered now, such as going back in a sequential quiz.</summary>
    public const string ItemLocked = "ItemLocked";

    /// <summary>A reason is required.</summary>
    public const string ReasonRequired = "ReasonRequired";

    /// <summary>A score is out of range.</summary>
    public const string InvalidScore = "InvalidScore";

    /// <summary>The quiz has no questions.</summary>
    public const string NoQuestions = "NoQuestions";

    /// <summary>
    /// The learner's enrollment is not active (suspended, ended or withdrawn), so a grade cannot reach the learner's
    /// progress; nothing was changed.
    /// </summary>
    public const string EnrollmentNotActive = "EnrollmentNotActive";

    /// <summary>Learning refused the result for another reason, such as an item no longer in the course; nothing was changed.</summary>
    public const string NotRecorded = "NotRecorded";
}
