using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Decides whether a user may do something with a survey: global permissions, own surveys, then access providers.
/// </summary>
public interface ISurveyAuthorizationService
{
    /// <summary>
    /// Decides whether a user may do an operation on a survey.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    Task<bool> AuthorizeAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default);
}
