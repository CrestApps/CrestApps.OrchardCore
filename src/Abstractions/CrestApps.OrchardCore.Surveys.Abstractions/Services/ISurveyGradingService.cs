using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Grades, regrades and changes the scores of responses. Every change is kept in the score history.
/// </summary>
public interface ISurveyGradingService
{
    /// <summary>
    /// Scores questions of a response by hand.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="scores">The points by question id.</param>
    /// <param name="comments">The comments by question id, or <see langword="null"/>.</param>
    /// <param name="feedback">The overall feedback, or <see langword="null"/>.</param>
    /// <param name="actorId">The grader.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> GradeAsync(string responseId, IReadOnlyDictionary<string, decimal> scores, IReadOnlyDictionary<string, string> comments, string feedback, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks that a grader used AI help on a question.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> RecordScoringHelpAsync(string responseId, string questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores a response again with its version's key.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="actorId">Who regrades.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> RegradeAsync(string responseId, string actorId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores the completed responses of older versions again with the key of another version, matched by question id
    /// (only questions whose type did not change).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="fromVersionIds">The versions whose responses are scored again.</param>
    /// <param name="keyVersionId">The version whose key is used.</param>
    /// <param name="actorId">Who regrades.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many responses were scored again.</returns>
    Task<int> RegradeVersionsAsync(string surveyId, IReadOnlyCollection<string> fromVersionIds, string keyVersionId, string actorId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the total score of a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="score">The new score.</param>
    /// <param name="actorId">Who changes it.</param>
    /// <param name="reason">Why (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyResponseResult> ChangeScoreAsync(string responseId, decimal score, string actorId, string reason, CancellationToken cancellationToken = default);
}
