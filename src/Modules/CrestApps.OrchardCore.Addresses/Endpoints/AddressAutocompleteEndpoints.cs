using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Addresses.Endpoints;

/// <summary>
/// Address suggestions for the address editor: addresses for what was typed, and the parts of the one picked. Each one is
/// a call the store pays its provider for, so both endpoints answer only a page that shows the editor (the editor token
/// in the address), hold each network to <see cref="RequestsPerMinute"/> requests a minute whether or not Orchard Core's
/// Rate Limits feature is on, and stop for the day at <see cref="AddressAutocompleteOptions.DailyRequestLimit"/>. With
/// Rate Limits on, the <see cref="AddressConstants.AutocompleteRateLimitGroup"/> group limit applies as well.
/// </summary>
internal static class AddressAutocompleteEndpoints
{
    public const string SuggestPath = "addresses/suggest";

    public const string ResolvePath = "addresses/resolve";

    /// <summary>
    /// The attempt policy that holds each network to <see cref="RequestsPerMinute"/> calls a minute.
    /// </summary>
    public const string NetworkAttemptPolicy = "address-suggestions-network";

    /// <summary>
    /// The attempt policy that holds the whole site to <see cref="AddressAutocompleteOptions.DailyRequestLimit"/>.
    /// </summary>
    public const string DailyAttemptPolicy = "address-suggestions-daily";

    // Requests a visitor may make in a minute; typing an address takes a dozen or so.
    internal const int RequestsPerMinute = 60;

    // The token is for this purpose only, and only the editor's token opens the endpoints.
    private const string TokenPurpose = "CrestApps.OrchardCore.Addresses.Autocomplete";

    private const string TokenValue = "address-editor";

    private const string DailyKey = "site";

    private const int MinimumText = 3;

    private const int MaximumText = 120;

    // Long enough to fill in a checkout left open; a page shown again gets a new one.
    private static readonly TimeSpan _tokenLifetime = TimeSpan.FromHours(2);

    /// <summary>
    /// Returns the token the address editor puts in the endpoint addresses it renders.
    /// </summary>
    /// <param name="tokens">The signed token service.</param>
    public static string CreateEditorToken(SignedTokenService tokens)
        => tokens.Protect(TokenPurpose, TokenValue, _tokenLifetime);

    public static async Task<IResult> SuggestAsync(
        string token,
        string q,
        string country,
        string session,
        HttpContext httpContext,
        IEnumerable<IAddressAutocompleteProvider> providers,
        SignedTokenService tokens,
        IAttemptLimiter limiter,
        IOptions<AddressAutocompleteOptions> options)
    {
        if (!IsEditorToken(tokens, token))
        {
            return Results.NotFound();
        }

        var text = q?.Trim();

        if (string.IsNullOrEmpty(text) || text.Length < MinimumText || text.Length > MaximumText)
        {
            return Results.Json(Array.Empty<AddressSuggestion>());
        }

        if (await providers.GetAvailableAsync(httpContext.RequestAborted) is not { } provider)
        {
            return Results.NotFound();
        }

        if (await RefuseAsync(httpContext, limiter, options.Value) is { } refused)
        {
            return refused;
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

    public static async Task<IResult> ResolveAsync(
        string token,
        string id,
        string session,
        HttpContext httpContext,
        IEnumerable<IAddressAutocompleteProvider> providers,
        SignedTokenService tokens,
        IAttemptLimiter limiter,
        IOptions<AddressAutocompleteOptions> options)
    {
        if (!IsEditorToken(tokens, token) || string.IsNullOrWhiteSpace(id) || id.Length > 512)
        {
            return Results.NotFound();
        }

        if (await providers.GetAvailableAsync(httpContext.RequestAborted) is not { } provider)
        {
            return Results.NotFound();
        }

        if (await RefuseAsync(httpContext, limiter, options.Value) is { } refused)
        {
            return refused;
        }

        if (await provider.ResolveAsync(id.Trim(), Clean(session), httpContext.RequestAborted) is not { } address)
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

    private static bool IsEditorToken(SignedTokenService tokens, string token)
        => token is { Length: > 0 and <= 1024 } && tokens.Unprotect(TokenPurpose, token) == TokenValue;

    // The network's minute first, so one script can't spend the site's day; then the site's day. A daily limit of zero or
    // less refuses: the limit fails closed, as the limiter does when its counter can't be reached.
    private static async Task<IResult> RefuseAsync(HttpContext httpContext, IAttemptLimiter limiter, AddressAutocompleteOptions options)
    {
        var network = await limiter.AcquireAsync(NetworkAttemptPolicy, ClientNetworkKey.NetworkOnly(httpContext), httpContext.RequestAborted);

        if (!network.Allowed)
        {
            return new TooManyRequestsResult(network.RetryAfter);
        }

        if (options.DailyRequestLimit <= 0)
        {
            return new TooManyRequestsResult(TimeSpan.FromHours(1));
        }

        var daily = await limiter.AcquireAsync(DailyAttemptPolicy, DailyKey, httpContext.RequestAborted);

        return daily.Allowed ? null : new TooManyRequestsResult(daily.RetryAfter);
    }

    private static bool IsCountryCode(string value)
        => value is { Length: 2 } && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]);

    private static string Clean(string session)
        => string.IsNullOrWhiteSpace(session) || session.Length > 64 || !session.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? null
            : session;

    // The platform's one 429 body, with its Retry-After header.
    private sealed class TooManyRequestsResult : IResult
    {
        private readonly TimeSpan _retryAfter;

        public TooManyRequestsResult(TimeSpan retryAfter)
        {
            _retryAfter = retryAfter;
        }

        public Task ExecuteAsync(HttpContext httpContext)
            => ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(httpContext, _retryAfter, httpContext.RequestAborted);
    }
}
