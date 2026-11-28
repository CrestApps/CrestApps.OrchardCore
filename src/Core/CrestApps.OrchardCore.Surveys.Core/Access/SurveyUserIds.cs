using System.Security.Claims;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Reads the user id of a caller the same way everywhere: the <see cref="ClaimTypes.NameIdentifier"/> claim, or the
/// <c>sub</c> claim of an API token that has none. The API's caller id and its ownership checks both use it, so a
/// caller who owns a survey is always its owner.
/// </summary>
public static class SurveyUserIds
{
    /// <summary>
    /// The claim type of the subject in an OpenID Connect token.
    /// </summary>
    public const string SubjectClaimType = "sub";

    /// <summary>
    /// Gets the user id of a caller, or <see langword="null"/>.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <returns>The user id.</returns>
    public static string Of(ClaimsPrincipal user)
    {
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(id) ? user?.FindFirstValue(SubjectClaimType) : id;
    }
}
