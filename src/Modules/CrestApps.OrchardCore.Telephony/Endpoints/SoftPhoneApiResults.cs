using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrestApps.OrchardCore.Telephony.Endpoints;

/// <summary>
/// Refusals for the endpoints the soft phone and its desktop and extension clients call.
/// </summary>
/// <remarks>
/// <c>TypedResults.Forbid()</c> hands the refusal to the site's cookie authentication, which answers with a redirect
/// to the access-denied page. A script's <c>fetch</c> follows that redirect and receives the page with a 200, so a
/// refused request looked like a success. These results write the status and a problem body directly.
/// </remarks>
internal static class SoftPhoneApiResults
{
    public static ProblemHttpResult Forbidden(string detail = null)
    {
        return TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden);
    }
}
