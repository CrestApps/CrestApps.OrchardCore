using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Adds a requirement to changing scores that are given: changing a response's total score, scoring it again, grading
/// a graded response again, and scoring older versions again with another version's key. Every check must allow the
/// change; it comes on top of being allowed to grade the survey. Register an implementation with
/// <c>services.AddSurveyScoreChangeCheck&lt;T&gt;()</c>, such as Learning requiring Override grades on course quizzes.
/// </summary>
public interface ISurveyScoreChangeCheck
{
    /// <summary>
    /// Decides whether a user may change scores of a survey.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="response">The response whose score changes, or <see langword="null"/> for many responses at once.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when this check allows the change.</returns>
    Task<bool> AllowsAsync(ClaimsPrincipal user, Survey survey, SurveyResponse response, CancellationToken cancellationToken = default);
}
