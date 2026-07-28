using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.SignalR.Middlewares;

/// <summary>
/// Authenticates SignalR hub requests using the Orchard Core <c>Api</c> authentication scheme so that
/// token based clients, such as headless front-ends, mobile applications, and service-to-service callers,
/// can connect to hubs the same way they call the API endpoints.
/// </summary>
/// <remarks>
/// Hubs opt in by applying <see cref="AllowApiTokenAuthenticationAttribute"/>, so hubs declared by Orchard Core
/// or by a host application are never affected. Cookie authenticated requests are left untouched. The <c>Api</c>
/// scheme is only evaluated when the request targets an opted-in hub endpoint, the caller is still anonymous, and
/// a bearer token was provided. Browsers cannot send an <c>Authorization</c> header during a WebSocket handshake,
/// so SignalR clients send the token using the standard <c>access_token</c> query string parameter, which this
/// middleware promotes to an <c>Authorization</c> header before authenticating.
/// </remarks>
public sealed class HubApiAuthenticationMiddleware
{
    private const string ApiAuthenticationScheme = "Api";

    private const string AccessTokenQueryParameterName = "access_token";

    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubApiAuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public HubApiAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<HubApiAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the request and authenticates anonymous hub requests that carry a bearer token.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsAnonymousOptedInHubRequest(context))
        {
            await AuthenticateAsync(context);
        }

        await _next(context);
    }

    private static bool IsAnonymousOptedInHubRequest(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<HubMetadata>() is null)
        {
            return false;
        }

        // MapHub copies the hub's class level attributes onto the hub and negotiate endpoints,
        // which makes the opt-in visible here without inspecting the hub type directly.
        return endpoint.Metadata.GetMetadata<AllowApiTokenAuthenticationAttribute>() is not null;
    }

    private async Task AuthenticateAsync(HttpContext context)
    {
        var accessToken = GetAccessToken(context.Request);

        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var schemeProvider = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        if (await schemeProvider.GetSchemeAsync(ApiAuthenticationScheme) is null)
        {
            return;
        }

        context.Request.Headers.Authorization = BearerPrefix + accessToken;

        var result = await context.AuthenticateAsync(ApiAuthenticationScheme);

        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(result.Failure, "Unable to authenticate the hub request '{Path}' using the '{Scheme}' authentication scheme.", context.Request.Path, ApiAuthenticationScheme);
        }
    }

    private static string GetAccessToken(HttpRequest request)
    {
        var authorization = request.Headers[HeaderNames.Authorization].ToString();

        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization.Substring(BearerPrefix.Length).Trim();
        }

        if (!string.IsNullOrEmpty(authorization))
        {
            // A different authentication scheme is already in use. Leave the request untouched.
            return null;
        }

        return request.Query[AccessTokenQueryParameterName].ToString();
    }
}
