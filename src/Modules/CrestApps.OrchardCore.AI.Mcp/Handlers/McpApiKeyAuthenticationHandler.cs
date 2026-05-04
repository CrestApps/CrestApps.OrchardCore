using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpApiKeyAuthenticationHandler : AuthenticationHandler<McpApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<McpServerOptions> _mcpServerOptionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="mcpServerOptionsMonitor">The mcp server options monitor.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The encoder.</param>
    public McpApiKeyAuthenticationHandler(
        IOptionsMonitor<McpApiKeyAuthenticationOptions> options,
        IOptionsMonitor<McpServerOptions> mcpServerOptionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
    : base(options, loggerFactory, encoder)
    {
        _mcpServerOptionsMonitor = mcpServerOptionsMonitor;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var mcpServerOptions = _mcpServerOptionsMonitor.CurrentValue;

        // Only handle authentication if ApiKey authentication type is configured.

        if (mcpServerOptions.AuthenticationType != McpServerAuthenticationType.ApiKey)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrEmpty(mcpServerOptions.ApiKey))
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

        if (!FixedTimeEquals(apiKey, mcpServerOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "mcp-api-key-user"),
            new Claim(ClaimTypes.Name, "MCP API Key User"),
            new Claim(ClaimTypes.AuthenticationMethod, McpApiKeyAuthenticationDefaults.AuthenticationScheme),
        };

        var identity = new ClaimsIdentity(claims, McpApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, McpApiKeyAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

internal sealed class McpApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

internal static class McpApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "McpApiKey";
}
