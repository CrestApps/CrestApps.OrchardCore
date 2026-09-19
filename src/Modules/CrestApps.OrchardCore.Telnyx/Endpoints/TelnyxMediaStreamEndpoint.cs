using CrestApps.OrchardCore.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Endpoints;

/// <summary>
/// Hosts the WebSocket endpoint Telnyx dials after a <c>streaming_start</c> command. The correlation token carried in
/// the query string is looked up in the per-node <see cref="ITelnyxMediaStreamRegistry"/>: an unknown token (a forged
/// request, an expired session, or a callback routed to a different node than the one that started the stream) is
/// refused. When the token resolves, the accepted socket is handed to the awaiting media session and the request is
/// parked until the session stops, keeping the socket alive for the call.
/// </summary>
public static class TelnyxMediaStreamEndpoint
{
    /// <summary>
    /// Maps the provider media stream endpoint.
    /// </summary>
    /// <remarks>
    /// The socket the voice provider streams call audio over.
    /// </remarks>
    /// <param name="builder">The route builder to map onto.</param>
    /// <param name="configure">
    /// Applied to every route mapped here, so a host can add its own filters or metadata.
    /// </param>
    /// <returns>The same route builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapTelnyxMediaStreamEndpoint(
        this IEndpointRouteBuilder builder,
        Action<RouteHandlerBuilder> configure = null)
    {
        var route = builder.MapGet(TelnyxConstants.MediaStreamPath, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        configure?.Invoke(route);

        return builder;
    }

    internal static async Task HandleAsync(
        HttpContext httpContext,
        [FromQuery(Name = "t")] string token,
        IWebSocketConnectionRegistry registry,
        ILogger<Startup> logger)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            return;
        }

        var connection = await registry.TryClaimAsync(token, httpContext.RequestAborted);

        if (connection is null)
        {
            // Unknown/expired token, or a callback that reached a node that never started this stream.
            logger.LogWarning("Rejected a Telnyx media-stream WebSocket because its correlation token was not recognized.");

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();

        if (!connection.TryComplete(webSocket))
        {
            // The provider already abandoned this connection (timeout); do not leave the socket dangling.
            webSocket.Abort();
            webSocket.Dispose();
            connection.Release();

            return;
        }

        // Keep the request (and therefore the socket) alive until the session finishes with it.
        await connection.ReleasedTask;
    }
}
