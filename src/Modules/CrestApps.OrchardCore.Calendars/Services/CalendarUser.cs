using System.Security.Claims;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Reads the signed-in user's id.
/// </summary>
internal static class CalendarUser
{
    /// <summary>
    /// Gets the user id of a principal.
    /// </summary>
    /// <param name="user">The principal.</param>
    /// <returns>The user id, or <see langword="null"/>.</returns>
    public static string GetId(ClaimsPrincipal user)
    {
        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
