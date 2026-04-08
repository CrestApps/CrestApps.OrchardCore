using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.Core.AI.A2A.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Services;

internal sealed class A2AApiKeyAuthenticationHandler : AuthenticationHandler<A2AApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<A2AHostOptions> _hostOptionsMonitor;

    public A2AApiKeyAuthenticationHandler(
        IOptionsMonitor<A2AApiKeyAuthenticationOptions> options,
        IOptionsMonitor<A2AHostOptions> hostOptionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        _hostOptionsMonitor = hostOptionsMonitor;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var hostOptions = _hostOptionsMonitor.CurrentValue;

        if (hostOptions.AuthenticationType != A2AHostAuthenticationType.ApiKey)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrEmpty(hostOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is not configured on the server."));
        }

        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeaderValue = authHeader.ToString();

        if (string.IsNullOrEmpty(authHeaderValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string apiKey;

        if (authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authHeaderValue["Bearer ".Length..].Trim();
        }
        else if (authHeaderValue.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authHeaderValue["ApiKey ".Length..].Trim();
        }
        else
        {
            apiKey = authHeaderValue.Trim();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!FixedTimeEquals(apiKey, hostOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "a2a-api-key-user"),
            new Claim(ClaimTypes.Name, "A2A API Key User"),
            new Claim(ClaimTypes.AuthenticationMethod, A2AApiKeyAuthenticationDefaults.AuthenticationScheme),
        ];

        var identity = new ClaimsIdentity(claims, A2AApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, A2AApiKeyAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

internal sealed class A2AApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

internal static class A2AApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "A2AApiKey";
}
