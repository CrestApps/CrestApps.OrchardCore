using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Signs a request in with the one API key a host is configured with, read from the <c>Authorization</c> header as
/// <c>Bearer {key}</c>, <c>ApiKey {key}</c> or the bare key. The A2A host and the MCP server share it; each says
/// whether API-key authentication is on, what the configured key is, and who the signed-in caller is.
/// </summary>
/// <typeparam name="TOptions">The scheme options.</typeparam>
public abstract class ConfiguredApiKeyAuthenticationHandler<TOptions> : AuthenticationHandler<TOptions>
    where TOptions : AuthenticationSchemeOptions, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredApiKeyAuthenticationHandler{TOptions}"/> class.
    /// </summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    protected ConfiguredApiKeyAuthenticationHandler(
        IOptionsMonitor<TOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
    }

    /// <summary>
    /// Gets whether the host is configured to authenticate callers with its API key. When it is not, the handler
    /// does not take part.
    /// </summary>
    protected abstract bool IsApiKeyAuthenticationEnabled { get; }

    /// <summary>
    /// Gets the API key the host is configured with.
    /// </summary>
    protected abstract string ConfiguredApiKey { get; }

    /// <summary>
    /// Gets the identifier given to a caller signed in with the key.
    /// </summary>
    protected abstract string CallerId { get; }

    /// <summary>
    /// Gets the name given to a caller signed in with the key.
    /// </summary>
    protected abstract string CallerName { get; }

    /// <summary>
    /// Reads the key from an <c>Authorization</c> header value: <c>Bearer {key}</c>, <c>ApiKey {key}</c> or the bare
    /// key.
    /// </summary>
    /// <param name="authorizationHeader">The header value.</param>
    /// <returns>The key, or <see langword="null"/> when the header carries none.</returns>
    public static string ReadApiKey(string authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        string apiKey;

        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authorizationHeader["Bearer ".Length..].Trim();
        }
        else if (authorizationHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authorizationHeader["ApiKey ".Length..].Trim();
        }
        else
        {
            apiKey = authorizationHeader.Trim();
        }

        return apiKey.Length == 0
            ? null
            : apiKey;
    }

    /// <summary>
    /// Compares two keys in constant time, so the time a comparison takes says nothing about how much of a guess was
    /// right.
    /// </summary>
    /// <param name="presented">The key the caller presented.</param>
    /// <param name="configured">The key the host is configured with.</param>
    public static bool KeysMatch(string presented, string configured)
    {
        if (presented is null || configured is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(configured));
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!IsApiKeyAuthenticationEnabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var configuredApiKey = ConfiguredApiKey;

        if (string.IsNullOrEmpty(configuredApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is not configured on the server."));
        }

        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var apiKey = ReadApiKey(authorizationHeader.ToString());

        if (apiKey is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!KeysMatch(apiKey, configuredApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, CallerId),
            new Claim(ClaimTypes.Name, CallerName),
            new Claim(ClaimTypes.AuthenticationMethod, Scheme.Name),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
