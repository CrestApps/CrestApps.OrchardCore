using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Addresses.Endpoints;

/// <summary>
/// Answers the address editor with a country's layout: its lines, labels, required parts and regions.
/// </summary>
internal static class AddressFormatsEndpoint
{
    public static async Task<IResult> HandleAsync(string countryCode, IAddressEditorService editor, HttpContext httpContext)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2 || !char.IsAsciiLetter(countryCode[0]) || !char.IsAsciiLetter(countryCode[1]))
        {
            return Results.NotFound();
        }

        // The labels are in the visitor's language, so the answer is cached by the browser only.
        httpContext.Response.Headers.CacheControl = "private, max-age=3600";

        return Results.Json(await editor.GetLayoutAsync(countryCode.ToUpperInvariant()));
    }
}
