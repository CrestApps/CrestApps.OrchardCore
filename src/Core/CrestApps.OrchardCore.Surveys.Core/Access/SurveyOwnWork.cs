using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Nobody grades, rescores or regrades their own work: the one check every grading path (the grading queue, the Grade
/// page and its posts, regrading versions, scoring help) makes, so course staff who also take a quiz can't change
/// their own result.
/// </summary>
public static class SurveyOwnWork
{
    /// <summary>
    /// Gets the id of a signed-in user, or <see langword="null"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The user id.</returns>
    public static string UserIdOf(ClaimsPrincipal user)
        => user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    /// <summary>
    /// Gets whether a response is the user's own.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when the user gave it.</returns>
    public static bool IsOwn(ClaimsPrincipal user, SurveyResponse response)
        => response is not null
            && !string.IsNullOrEmpty(response.UserId)
            && string.Equals(response.UserId, UserIdOf(user), StringComparison.Ordinal);
}
