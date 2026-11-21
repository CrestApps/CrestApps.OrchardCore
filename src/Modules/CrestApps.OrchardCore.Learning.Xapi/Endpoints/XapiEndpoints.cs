using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Learning.Xapi.Endpoints;

/// <summary>
/// The LRS endpoint under <c>/learning/xapi/</c>: one route for every resource and method, so the request reader can
/// unwrap the alternate request syntax before the resource is chosen. The endpoint authenticates the request itself
/// (an OpenID application or a launch token, never the sign-in cookie) and answers cross-origin requests from any
/// origin, without credentials.
/// </summary>
internal static class XapiEndpoints
{
    public static IEndpointRouteBuilder MapLearningXapi(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var pattern = XapiConstants.RoutePrefix + "/{**resource}";

        routes.MapMethods(pattern, [HttpMethods.Get, HttpMethods.Head, HttpMethods.Put, HttpMethods.Post, HttpMethods.Delete], HandleAsync)
            .DisableAntiforgery()
            .WithRateLimitGroup(XapiConstants.RateLimitGroup)
            .WithName("LearningXapi")
            .ExcludeFromDescription();

        routes.MapMethods(pattern, [HttpMethods.Options], Preflight)
            .DisableAntiforgery()
            .WithName("LearningXapiPreflight")
            .ExcludeFromDescription();

        return routes;
    }

    internal static async Task HandleAsync(
        HttpContext httpContext,
        string resource,
        [FromServices] IOptions<XapiOptions> options,
        [FromServices] XapiAuthenticator authenticator,
        [FromServices] XapiEndpointHandler handler)
    {
        var endpointPath = httpContext.Request.PathBase.Add("/" + XapiConstants.RoutePrefix).Value;
        var (request, error) = await XapiHttp.ReadAsync(httpContext, resource, endpointPath, options.Value.MaxRequestBytes);

        if (error is not null)
        {
            await XapiHttp.WriteAsync(httpContext, error, head: false);

            return;
        }

        // Only the request's own credential counts: the user the sign-in cookie made is never looked at.
        var credential = await authenticator.AuthenticateAsync(httpContext, request);
        var response = await handler.HandleAsync(request, credential, httpContext.RequestAborted);

        await XapiHttp.WriteAsync(httpContext, response, request.Method == HttpMethods.Head);
    }

    internal static void Preflight(HttpContext httpContext)
        => XapiHttp.WritePreflight(httpContext);
}
