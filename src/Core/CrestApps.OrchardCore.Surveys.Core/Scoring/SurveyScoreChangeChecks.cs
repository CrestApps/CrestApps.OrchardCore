using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Asks every <see cref="ISurveyScoreChangeCheck"/> whether a user may change given scores: a change of the total score,
/// scoring a response again, grading a graded response again, and scoring older versions again with another key. Every
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
    /// Gets whether grading a response changes a score that is given: it is graded, so new grades replace given ones.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when the grading needs the score change checks.</returns>
    public static bool GradingChangesGivenScores(SurveyResponse response)
        => response?.Grading == GradingStatus.Graded;
}
