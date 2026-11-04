using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Protocol;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using OrchardCore.RateLimits;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb.Endpoints;

/// <summary>
/// Maps the NLWeb endpoints at the root of the tenant: <c>POST /ask</c> (protocol 0.5 and later, JSON or server-sent
/// events), <c>GET /ask</c> (the original query-string protocol), and <c>POST /await</c>.
/// </summary>
internal static class NLWebEndpoints
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>
    /// Maps the NLWeb endpoints.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapNLWebEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost(NLWebConstants.AskRoute, AskAsync)
            .DisableAntiforgery()
            .WithRateLimitGroup(NLWebConstants.RateLimitGroup);

        routes.MapGet(NLWebConstants.AskRoute, AskLegacyAsync)
            .WithRateLimitGroup(NLWebConstants.RateLimitGroup);

        routes.MapPost(NLWebConstants.AwaitRoute, AwaitAsync)
            .DisableAntiforgery()
            .WithRateLimitGroup(NLWebConstants.RateLimitGroup);

        routes.MapMethods(NLWebConstants.AskRoute, [HttpMethods.Options], PreflightAsync);
        routes.MapMethods(NLWebConstants.AwaitRoute, [HttpMethods.Options], PreflightAsync);

        return routes;
    }

    private static async Task AskAsync(
        HttpContext httpContext,
        [FromServices] INLWebAskService askService,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] ISiteService siteService)
    {
        var settings = await siteService.GetSettingsAsync<NLWebSettings>();

        AddCorsHeaders(httpContext, settings);

        if (!await AuthorizeAsync(httpContext, authorizationService))
        {
            return;
        }

        var body = await ReadBodyAsync(httpContext);

        if (body is null)
        {
            await WriteFailureAsync(httpContext, StatusCodes.Status400BadRequest, NLWebProtocol.ErrorCodes.InvalidRequest, "The request body must be a JSON object.");

            return;
        }

        if (!NLWebRequestReader.TryRead(body, out var request, out var error))
        {
            await WriteFailureAsync(httpContext, StatusCodes.Status400BadRequest, NLWebProtocol.ErrorCodes.InvalidRequest, error);

            return;
        }

        await WriteAnswerAsync(httpContext, askService, request);
    }

    private static async Task AskLegacyAsync(
        HttpContext httpContext,
        [FromServices] INLWebAskService askService,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] ISiteService siteService)
    {
        var settings = await siteService.GetSettingsAsync<NLWebSettings>();

        AddCorsHeaders(httpContext, settings);

        if (!settings.AllowLegacyRequests)
        {
            httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            httpContext.Response.Headers.Allow = "POST, OPTIONS";

            return;
        }

        if (!await AuthorizeAsync(httpContext, authorizationService))
        {
            return;
        }

        var values = httpContext.Request.Query.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.ToString()));

        if (!NLWebRequestReader.TryReadQueryString(values, out var request, out var error))
        {
            await WriteFailureAsync(httpContext, StatusCodes.Status400BadRequest, NLWebProtocol.ErrorCodes.InvalidRequest, error);

            return;
        }

        await WriteAnswerAsync(httpContext, askService, request);
    }

    private static async Task AwaitAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] ISiteService siteService)
    {
        var settings = await siteService.GetSettingsAsync<NLWebSettings>();

        AddCorsHeaders(httpContext, settings);

        if (!await AuthorizeAsync(httpContext, authorizationService))
        {
            return;
        }

        // The site answers every question at once and never returns a promise, so no promise token is known.
        await WriteFailureAsync(httpContext, StatusCodes.Status200OK, NLWebProtocol.ErrorCodes.UnknownPromise, "The promise token is unknown.");
    }

    private static async Task PreflightAsync(
        HttpContext httpContext,
        [FromServices] ISiteService siteService)
    {
        var settings = await siteService.GetSettingsAsync<NLWebSettings>();

        if (settings.AllowCrossOrigin)
        {
            AddCorsHeaders(httpContext, settings);
            httpContext.Response.Headers.AccessControlAllowMethods = "GET, POST, OPTIONS";
            httpContext.Response.Headers.AccessControlAllowHeaders = "Content-Type, Accept, Authorization";
            httpContext.Response.Headers.AccessControlMaxAge = "86400";
        }

        httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task WriteAnswerAsync(HttpContext httpContext, INLWebAskService askService, NLWebAskRequest request)
    {
        var cancellationToken = httpContext.RequestAborted;
        var events = askService.AskAsync(request, httpContext.User, cancellationToken);

        if (WantsStream(httpContext.Request, request))
        {
            await NLWebHttpResponseWriter.WriteStreamAsync(httpContext, events, cancellationToken);

            return;
        }

        var response = request.IsLegacy
            ? await NLWebHttpResponseWriter.BuildLegacyAsync(events, httpContext.Request.Host.Host)
            : await NLWebHttpResponseWriter.BuildAsync(events);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = JsonContentType;

        await httpContext.Response.WriteAsync(response.ToJsonString(NLWebJson.SerializerOptions), cancellationToken);
    }

    /// <summary>
    /// Decides whether the response is a server-sent event stream: the request's <c>streaming</c> preference wins, and
    /// without one the <c>Accept</c> header decides.
    /// </summary>
    /// <param name="httpRequest">The HTTP request.</param>
    /// <param name="request">The NLWeb request.</param>
    /// <returns><see langword="true"/> to stream the response.</returns>
    internal static bool WantsStream(HttpRequest httpRequest, NLWebAskRequest request)
    {
        if (request.Prefer.Streaming is bool streaming)
        {
            return streaming;
        }

        foreach (var accept in httpRequest.GetTypedHeaders().Accept ?? [])
        {
            if (accept.MediaType.Equals(NLWebProtocol.EventStreamMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> AuthorizeAsync(HttpContext httpContext, IAuthorizationService authorizationService)
    {
        if (await authorizationService.AuthorizeAsync(httpContext.User, NLWebPermissionsProvider.QueryNLWeb))
        {
            return true;
        }

        var statusCode = httpContext.User?.Identity?.IsAuthenticated == true
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;

        await WriteFailureAsync(httpContext, statusCode, NLWebProtocol.ErrorCodes.Forbidden, "You may not ask this site questions.");

        return false;
    }

    private static async Task<JsonObject> ReadBodyAsync(HttpContext httpContext)
    {
        var sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = NLWebConstants.MaxRequestBodySize;
        }

        if (httpContext.Request.ContentLength > NLWebConstants.MaxRequestBodySize)
        {
            return null;
        }

        try
        {
            return await JsonNode.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (BadHttpRequestException)
        {
            return null;
        }
    }

    private static Task WriteFailureAsync(HttpContext httpContext, int statusCode, string code, string message)
    {
        var builder = new NLWebResponseBuilder();
        builder.Add(new NLWebFailureEvent(code, message));

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = JsonContentType;

        return httpContext.Response.WriteAsync(builder.Build().ToJsonString(NLWebJson.SerializerOptions), httpContext.RequestAborted);
    }

    private static void AddCorsHeaders(HttpContext httpContext, NLWebSettings settings)
    {
        if (!settings.AllowCrossOrigin)
        {
            return;
        }

        // Any origin may call the endpoints, but browsers never send credentials with a wildcard origin, so a page on
        // another site only ever gets the answers an anonymous visitor would get.
        httpContext.Response.Headers[HeaderNames.AccessControlAllowOrigin] = "*";
    }
}
