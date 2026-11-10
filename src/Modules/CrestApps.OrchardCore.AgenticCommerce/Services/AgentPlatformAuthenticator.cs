using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore;

namespace CrestApps.OrchardCore.AgenticCommerce.Services;

/// <summary>
/// Finds the agent platform a request comes from: by its API key, or by the OAuth client its bearer token was issued to,
/// which also tells a linked buyer's account. The platform must be enabled, use the protocol and call from an allowed
/// address.
/// </summary>
public sealed class AgentPlatformAuthenticator
{
    private readonly IAgentPlatformStore _platforms;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPlatformAuthenticator"/> class.
    /// </summary>
    /// <param name="platforms">The platform store.</param>
    /// <param name="logger">The logger.</param>
    public AgentPlatformAuthenticator(IAgentPlatformStore platforms, ILogger<AgentPlatformAuthenticator> logger)
    {
        _platforms = platforms;
        _logger = logger;
    }

    /// <summary>
    /// Returns the platform that sent a request with credentials, or why it isn't allowed.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="protocol">The protocol the request is made in.</param>
    public async Task<AgentAuthentication> AuthenticateAsync(HttpContext httpContext, string protocol)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var headers = httpContext.Request.Headers;
        var authorization = headers.Authorization.ToString();
        var bearer = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization.Substring(7).Trim() : null;
        var apiKey = headers["X-API-Key"].ToString();

        if (string.IsNullOrEmpty(apiKey) && bearer?.StartsWith(AgentPlatformApiKeys.Prefix, StringComparison.Ordinal) == true)
        {
            apiKey = bearer;
        }

        AgentPlatform platform = null;
        string userId = null;
        IReadOnlyCollection<string> scopes = [];

        if (!string.IsNullOrEmpty(apiKey))
        {
            platform = await _platforms.FindByApiKeyHashAsync(AgentPlatformApiKeys.Hash(apiKey), httpContext.RequestAborted);

            if (platform is null)
            {
                return AgentAuthentication.Fail(StatusCodes.Status401Unauthorized, "The API key isn't valid.");
            }
        }
        else if (!string.IsNullOrEmpty(bearer))
        {
            var principal = await AuthenticateBearerAsync(httpContext);
            var clientId = principal?.FindFirstValue("client_id") ?? principal?.FindFirstValue("azp");

            if (string.IsNullOrEmpty(clientId))
            {
                return AgentAuthentication.Fail(StatusCodes.Status401Unauthorized, "The bearer token isn't valid.");
            }

            platform = await _platforms.FindByClientIdAsync(clientId, httpContext.RequestAborted);

            if (platform is null)
            {
                return AgentAuthentication.Fail(StatusCodes.Status403Forbidden, "The OAuth client isn't an agent platform of this store.");
            }

            // A token issued for a buyer through account linking names them; one issued to the client alone names the
            // client.
            var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(subject) && !string.Equals(subject, clientId, StringComparison.Ordinal))
            {
                userId = subject;
            }

            scopes = principal.FindAll("scope")
                .Concat(principal.FindAll("oi_scp"))
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        if (platform is null)
        {
            return AgentAuthentication.Fail(StatusCodes.Status401Unauthorized, null);
        }

        var result = Check(platform, protocol, httpContext, userId);

        return result.Succeeded
            ? new AgentAuthentication
            {
                Platform = result.Platform,
                UserId = result.UserId,
                Scopes = scopes,
            }
            : result;
    }

    /// <summary>
    /// Checks that a platform found another way, such as by a signed request, may call.
    /// </summary>
    /// <param name="platform">The platform.</param>
    /// <param name="protocol">The protocol the request is made in.</param>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="userId">The linked buyer, or <see langword="null"/>.</param>
    public static AgentAuthentication Check(AgentPlatform platform, string protocol, HttpContext httpContext, string userId = null)
    {
        ArgumentNullException.ThrowIfNull(platform);

        if (!platform.Enabled)
        {
            return AgentAuthentication.Fail(StatusCodes.Status403Forbidden, "The platform may not buy from this store.");
        }

        if (!platform.Uses(protocol))
        {
            return AgentAuthentication.Fail(StatusCodes.Status403Forbidden, $"The platform doesn't use {protocol.ToUpperInvariant()} with this store.");
        }

        if (!IsAllowed(platform, httpContext.Connection.RemoteIpAddress))
        {
            return AgentAuthentication.Fail(StatusCodes.Status403Forbidden, "Requests from this address aren't allowed for the platform.");
        }

        return new AgentAuthentication
        {
            Platform = platform,
            UserId = userId,
        };
    }

    private async Task<ClaimsPrincipal> AuthenticateBearerAsync(HttpContext httpContext)
    {
        try
        {
            var result = await httpContext.AuthenticateAsync(OrchardCoreConstants.AuthenticationSchemes.Api);

            return result.Succeeded ? result.Principal : null;
        }
        catch (InvalidOperationException exception)
        {
            // The OpenID token validation isn't on, so no bearer token but an API key can sign a platform in.
            _logger.LogDebug(exception, "Bearer tokens can't be validated: the API authentication scheme isn't registered.");

            return null;
        }
    }

    internal static bool IsAllowed(AgentPlatform platform, IPAddress address)
    {
        if (platform.AllowedIpRanges is not { Count: > 0 })
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        foreach (var entry in platform.AllowedIpRanges)
        {
            if (IPNetwork.TryParse(entry, out var network) && network.Contains(address))
            {
                return true;
            }

            if (IPAddress.TryParse(entry, out var single) && single.Equals(address))
            {
                return true;
            }
        }

        return false;
    }
}
