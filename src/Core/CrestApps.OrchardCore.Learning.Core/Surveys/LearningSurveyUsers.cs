using System.Security.Claims;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Reads the user id of a learner the way the Surveys API identifies its caller: the name identifier claim, or the
/// <c>sub</c> claim that tokens carry.
/// </summary>
internal static class LearningSurveyUsers
{
    /// <summary>
    /// Gets the id of a signed-in user.
    /// </summary>
    /// <param name="user">The user, or <see langword="null"/>.</param>
    /// <returns>The user id, or <see langword="null"/> when nobody is signed in.</returns>
    public static string GetUserId(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        return string.IsNullOrEmpty(userId) ? null : userId;
    }
}
