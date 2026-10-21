using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Addresses.Endpoints;

/// <summary>
/// Address suggestions for the address editor: addresses for what was typed, and the parts of the one picked. Anyone
/// filling in an address may ask, and each one is a call the store pays for, so both endpoints are in the
/// <see cref="AddressConstants.AutocompleteRateLimitGroup"/> rate-limit group, which holds each visitor to
/// <see cref="RequestsPerMinute"/> requests a minute when Orchard Core's Rate Limits feature is on.
/// </summary>
internal static class AddressAutocompleteEndpoints
{
    public const string SuggestPath = "addresses/suggest";

    public const string ResolvePath = "addresses/resolve";

    // Requests a visitor may make in a minute; typing an address takes a dozen or so.
    internal const int RequestsPerMinute = 60;

    private const int MinimumText = 3;

    private const int MaximumText = 120;

    public static async Task<IResult> SuggestAsync(string q, string country, string session, HttpContext httpContext, IEnumerable<IAddressAutocompleteProvider> providers)
    {
        var text = q?.Trim();

        if (string.IsNullOrEmpty(text) || text.Length < MinimumText || text.Length > MaximumText)
        {
            return Results.Json(Array.Empty<AddressSuggestion>());
        }

        if (await providers.GetAvailableAsync(httpContext.RequestAborted) is not { } provider)
        {
            return Results.NotFound();
        }

        var suggestions = await provider.SuggestAsync(new AddressSuggestionRequest
        {
            Text = text,
            Country = IsCountryCode(country) ? country.ToUpperInvariant() : null,
            SessionToken = Clean(session),
            Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
        }, httpContext.RequestAborted);

        return Results.Json(suggestions.Select(suggestion => new { id = suggestion.Id, text = suggestion.Text }));
    }

    public static async Task<IResult> ResolveAsync(string id, string session, HttpContext httpContext, IEnumerable<IAddressAutocompleteProvider> providers)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 512)
        {
            return Results.NotFound();
        }

        if (await providers.GetAvailableAsync(httpContext.RequestAborted) is not { } provider ||
            await provider.ResolveAsync(id.Trim(), Clean(session), httpContext.RequestAborted) is not { } address)
        {
            return Results.NotFound();
        }

        return Results.Json(new
        {
            addressLine1 = address.AddressLine1,
            addressLine2 = address.AddressLine2,
            dependentLocality = address.DependentLocality,
            city = address.City,
            region = address.Region,
            postalCode = address.PostalCode,
            country = address.Country,
        });
    }

    private static bool IsCountryCode(string value)
        => value is { Length: 2 } && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]);

    private static string Clean(string session)
        => string.IsNullOrWhiteSpace(session) || session.Length > 64 || !session.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? null
            : session;
}
