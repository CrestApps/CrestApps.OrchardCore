using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class ContactCenterEndpointAntiforgery
{
    public static async Task<bool> ValidateRequestAsync(IAntiforgery antiforgery, HttpContext httpContext)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);

            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
