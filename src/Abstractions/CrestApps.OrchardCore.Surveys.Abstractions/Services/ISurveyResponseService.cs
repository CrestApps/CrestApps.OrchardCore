using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Runs responses: start, pages, autosave, live evaluation, immediate feedback, completion, deadlines, reopening,
/// voiding and deleting. The server decides visibility, branching, limits, deadlines and scores.
/// </summary>
public interface ISurveyResponseService
{
    /// <summary>
    /// Starts a response, or returns the respondent's response in progress for the same survey and context.
    /// </summary>
    /// <param name="request">What to start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyStartResult> StartAsync(StartResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a response the caller owns: the signed-in user, or the holder of its resume token.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="identity">Who asks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, or <see langword="null"/> when it does not exist or is not the caller's.</returns>
    Task<SurveyResponse> FindForRespondentAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trades a response's resume token for a new one: the presented token must be the response's current token, and
    /// it stops working once the new one is stored. A resume link uses this so its token resumes only once.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="presentedToken">The resume token presented, such as the one on a resume link.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new token, or <see langword="null"/> when the presented token is not the response's current one.</returns>
    Task<string> RotateResumeTokenAsync(string responseId, string presentedToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current page of a response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<SurveyPageResult> GetPageAsync(SurveyResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the current page: next, back or finish.
    /// </summary>
    /// <param name="request">The post.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The next page, the errors, or the end.</returns>
    Task<SurveyPageResult> SubmitPageAsync(SubmitPageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current page without validation.
    /// </summary>
    /// <param name="request">The save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveySaveResult> SaveAsync(SavePageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the current page with answers not yet posted.
    /// </summary>
    /// <param name="request">The answers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Visibility and piped texts.</returns>
    Task<SurveyEvaluateResult> EvaluateAsync(EvaluatePageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks one answer with immediate feedback and locks it.
    /// </summary>
    /// <param name="request">The answer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The feedback.</returns>
    Task<SurveyCheckResult> CheckAnswerAsync(CheckAnswerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a response whose deadline passed.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> SubmitAtDeadlineAsync(string responseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a completed response when the survey allows changing answers after submitting.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="identity">Who asks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> ReopenAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="actorId">Who voids it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> VoidAsync(string responseId, string actorId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a response with its index rows and files.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="actorId">Who deletes it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> DeleteAsync(string responseId, string actorId, CancellationToken cancellationToken = default);
}
