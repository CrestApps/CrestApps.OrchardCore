using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Addresses.Endpoints;

/// <summary>
/// Address suggestions for the address editor: addresses for what was typed, and the parts of the one picked. Anyone
/// filling in an address may ask, so each visitor is held to a number of requests a minute: each one is a call the
/// store pays for.
/// </summary>
internal static class AddressAutocompleteEndpoints
{
    public const string SuggestPath = "addresses/suggest";

    public const string ResolvePath = "addresses/resolve";

    // Requests a visitor may make in a minute; typing an address takes a dozen or so.
    internal const int RequestsPerMinute = 60;

    private const int MinimumText = 3;

    private const int MaximumText = 120;

    public static async Task<IResult> SuggestAsync(string q, string country, string session, HttpContext httpContext, IEnumerable<IAddressAutocompleteProvider> providers, IMemoryCache cache, IClock clock)
    {
        var text = q?.Trim();

        if (string.IsNullOrEmpty(text) || text.Length < MinimumText || text.Length > MaximumText)
        {
            return Results.Json(Array.Empty<AddressSuggestion>());
        }

        if (!Allow(cache, httpContext.Connection.RemoteIpAddress, clock.UtcNow))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
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

    public static async Task<IResult> ResolveAsync(string id, string session, HttpContext httpContext, IEnumerable<IAddressAutocompleteProvider> providers, IMemoryCache cache, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 512)
        {
            return Results.NotFound();
        }

        if (!Allow(cache, httpContext.Connection.RemoteIpAddress, clock.UtcNow))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
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

    // A visitor's requests this minute, counted by their address.
    internal static bool Allow(IMemoryCache cache, IPAddress address, DateTime utcNow)
    {
        var key = $"address-autocomplete:{address?.ToString() ?? "unknown"}:{utcNow:yyyyMMddHHmm}";
        var count = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);

            return new StrongBox<int>();
        });

        return Interlocked.Increment(ref count.Value) <= RequestsPerMinute;
    }

    private static bool IsCountryCode(string value)
        => value is { Length: 2 } && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]);

    private static string Clean(string session)
        => string.IsNullOrWhiteSpace(session) || session.Length > 64 || !session.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? null
            : session;
}
