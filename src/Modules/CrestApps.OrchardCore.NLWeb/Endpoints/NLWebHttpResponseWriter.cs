using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CrestApps.OrchardCore.NLWeb.Endpoints;

/// <summary>
/// Writes the events of an NLWeb answer to an HTTP response: as one JSON object, or as server-sent events.
/// </summary>
internal static class NLWebHttpResponseWriter
{
    /// <summary>
    /// Builds the JSON response of protocol version 0.5 and later.
    /// </summary>
    /// <param name="events">The events of the answer.</param>
    /// <returns>The JSON response.</returns>
    public static async Task<JsonObject> BuildAsync(IAsyncEnumerable<NLWebEvent> events)
    {
        var builder = new NLWebResponseBuilder();

        await foreach (var nlwebEvent in events)
        {
            builder.Add(nlwebEvent);
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds the JSON response of the original protocol.
    /// </summary>
    /// <param name="events">The events of the answer.</param>
    /// <param name="site">The site token the results come from.</param>
    /// <returns>The JSON response.</returns>
    public static async Task<JsonObject> BuildLegacyAsync(IAsyncEnumerable<NLWebEvent> events, string site)
    {
        var builder = new NLWebLegacyResponseBuilder(site);

        await foreach (var nlwebEvent in events)
        {
            builder.Add(nlwebEvent);
        }

        return builder.Build();
    }

    /// <summary>
    /// Streams the events as server-sent events, flushing each one as it is produced, and ends the stream with an
    /// <c>end</c> event.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="events">The events of the answer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the stream is written.</returns>
    public static async Task WriteStreamAsync(HttpContext httpContext, IAsyncEnumerable<NLWebEvent> events, CancellationToken cancellationToken)
    {
        var response = httpContext.Response;

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = NLWebProtocol.EventStreamMediaType;
        response.Headers.CacheControl = "no-cache";

        // Proxies such as nginx buffer responses unless told not to, which would hold every event until the end.
        response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await foreach (var nlwebEvent in events.WithCancellation(cancellationToken))
        {
            var frame = NLWebStreamFrame.Create(nlwebEvent);

            if (frame is null)
            {
                continue;
            }

            await response.WriteAsync(frame.Format(), cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }

        await response.WriteAsync(NLWebStreamFrame.End.Format(), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
