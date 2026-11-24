using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Asks every <see cref="ISurveyScoreChangeCheck"/> whether a user may change given scores: a change of the total score,
/// scoring a response again, grading a graded response again or replacing a given score while grading, and scoring older
/// versions again with another key. Every
/// check must allow it; with no check registered, being allowed to grade the survey is enough.
/// </summary>
public sealed class SurveyScoreChangeChecks
{
    private readonly IEnumerable<ISurveyScoreChangeCheck> _checks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyScoreChangeChecks"/> class.
    /// </summary>
    /// <param name="checks">The registered checks.</param>
    public SurveyScoreChangeChecks(IEnumerable<ISurveyScoreChangeCheck> checks)
    {
        _checks = checks;
    }

    /// <summary>
    /// Gets whether a user may change scores of a survey.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="response">The response whose score changes, or <see langword="null"/> for many responses at once.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when every check allows the change.</returns>
    public async Task<bool> AllowAsync(ClaimsPrincipal user, Survey survey, SurveyResponse response, CancellationToken cancellationToken = default)
    {
        if (survey is null)
        {
            return false;
        }

        foreach (var check in _checks)
        {
            if (!await check.AllowsAsync(user, survey, response, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets whether voiding or deleting a response changes a grade that is given, so it needs the score change checks: a
    /// completed response of a scored survey, graded or waiting for a grader (the grade of an attempt, or the attempt
    /// given back). A response in progress, abandoned, screened out, voided or of a survey that isn't scored carries no
    /// grade, so voiding or deleting it needs only the right to delete responses.
    /// </summary>
    /// <param name="response">The response, as stored.</param>
    /// <returns><see langword="true"/> when the void or delete needs the score change checks.</returns>
    public static bool RemovingChangesGivenScore(SurveyResponse response)
        => response is { Status: SurveyResponseStatus.Completed, Grading: GradingStatus.Pending or GradingStatus.Graded };

    /// <summary>
    /// Gets whether grading a response changes a score that is given, so it needs the score change checks: the response is
    /// graded (new grades replace given ones), or a posted score differs from a question's given score, automatic or
    /// manual (such as full points for a wrong or blank choice question, or a second grader replacing the first one's
    /// grade while the response still waits for grading). Points for a question that needs grading (an answer with no
    /// score yet) and a score posted unchanged change nothing given.
    /// </summary>
    /// <param name="response">The response, as stored.</param>
    /// <param name="scores">The posted points, by question id.</param>
    /// <returns><see langword="true"/> when the grading needs the score change checks.</returns>
    public static bool GradingChangesGivenScores(SurveyResponse response, IReadOnlyDictionary<string, decimal> scores)
    {
        if (response is null)
        {
            return false;
        }

        if (response.Grading == GradingStatus.Graded)
        {
            return true;
        }

        foreach (var (questionId, points) in scores ?? new Dictionary<string, decimal>())
        {
            SurveyAnswer answer = null;

            if (!string.IsNullOrEmpty(questionId))
            {
                response.Answers?.TryGetValue(questionId, out answer);
            }

            if (answer is not null && answer.Score is null)
            {
                // The question needs grading: nothing is given yet.
                continue;
            }

            // An unanswered question scores 0.
            if (points != (answer?.Score ?? 0m))
            {
                return true;
            }
        }

        return false;
    }
}
