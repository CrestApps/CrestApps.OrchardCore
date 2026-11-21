using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Lists the responses that need a grader, across the surveys a user may grade.
/// </summary>
public interface ISurveyGradingQueue
{
    /// <summary>
    /// Reads a page of the responses a user may grade that need a grader, newest first.
    /// </summary>
    /// <param name="user">The grader.</param>
    /// <param name="surveyId">A survey to narrow to, or <see langword="null"/> for all.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(ClaimsPrincipal user, string surveyId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may grade the responses of a survey.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the user may grade them.</returns>
    Task<bool> CanGradeAsync(ClaimsPrincipal user, string surveyId, CancellationToken cancellationToken = default);
}
