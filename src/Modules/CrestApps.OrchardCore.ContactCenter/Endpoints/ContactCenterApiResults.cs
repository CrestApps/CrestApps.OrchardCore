using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// Refusals for the endpoints that scripts call (the soft phone, the agent workspace, the desktop and extension
/// clients).
/// </summary>
/// <remarks>
/// <c>TypedResults.Forbid()</c> and <c>TypedResults.Challenge()</c> hand the refusal to the site's cookie
/// authentication, which answers a browser with a redirect to the sign-in or access-denied page. A script's
/// <c>fetch</c> follows that redirect and receives the page with a 200, so a refused request looked like a success:
/// the soft phone deleted nothing and said nothing. These results write the status and a problem body directly, so
/// the caller always sees the 401, 403 or 404 and can tell the user why.
/// </remarks>
internal static class ContactCenterApiResults
{
    public static ProblemHttpResult Unauthorized(string detail = null)
    {
        return TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized);
    }

    public static ProblemHttpResult Forbidden(string detail = null)
    {
        return TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden);
    }

    public static ProblemHttpResult NotFound(string detail = null)
    {
        return TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);
    }
}
