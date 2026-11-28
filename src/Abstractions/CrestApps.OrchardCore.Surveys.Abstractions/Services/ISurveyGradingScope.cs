using System.Security.Claims;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Widens who may grade which responses, such as course staff grading the quizzes of their course. Register an
/// implementation with <c>services.AddSurveyGradingScope&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyGradingScope
{
    /// <summary>
    /// Gets the ids of the surveys a user may grade through this scope, beyond the global permission.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey ids.</returns>
    Task<IReadOnlyCollection<string>> GetGradableSurveyIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
