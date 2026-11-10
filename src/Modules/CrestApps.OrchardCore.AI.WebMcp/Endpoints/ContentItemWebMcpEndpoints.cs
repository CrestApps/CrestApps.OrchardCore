using CrestApps.Core.AI.WebMcp.Endpoints;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.Core.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.WebMcp.Endpoints;

/// <summary>
/// Maps the WebMCP endpoints of content items: the tools a content item's <see cref="WebMcpPart"/> exposes, listed
/// and called the same way as CrestApps.Core's site-wide endpoints.
/// </summary>
internal static class ContentItemWebMcpEndpoints
{
    /// <summary>
    /// Maps <c>GET api/ai/webmcp/content-items/{contentItemId}/tools</c> and
    /// <c>POST api/ai/webmcp/content-items/{contentItemId}/tools/{name}/invoke</c>.
    /// </summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapContentItemWebMcpTools(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // The invoke endpoint validates the antiforgery token itself, from the header the page script sends.
        routes.MapGet(WebMcpConstants.ContentItemToolsRoute, GetToolsAsync)
            .DisableAntiforgery();

        routes.MapPost(WebMcpConstants.ContentItemInvokeRoute, InvokeAsync)
            .DisableAntiforgery();

        return routes;
    }

    internal static async Task<IResult> GetToolsAsync(
        [FromRoute] string contentItemId,
        HttpContext httpContext,
        [FromServices] IWebMcpAccessEvaluator accessEvaluator,
        [FromServices] IContentManager contentManager,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] ContentItemWebMcpToolServiceFactory toolServiceFactory)
    {
        var (problem, part) = await ResolveAsync(contentItemId, httpContext, accessEvaluator, contentManager, authorizationService);

        if (problem is not null)
        {
            return problem;
        }

        var tools = await toolServiceFactory.Create(part.Tools).GetToolsAsync(httpContext.User, httpContext.RequestAborted);

        return TypedResults.Ok(new WebMcpToolListResponse
        {
            Tools = tools,
        });
    }

    internal static async Task<IResult> InvokeAsync(
        [FromRoute] string contentItemId,
        [FromRoute] string name,
        HttpContext httpContext,
        [FromServices] IWebMcpAccessEvaluator accessEvaluator,
        [FromServices] IContentManager contentManager,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IAntiforgery antiforgery,
        [FromServices] ContentItemWebMcpToolServiceFactory toolServiceFactory)
    {
        var (problem, part) = await ResolveAsync(contentItemId, httpContext, accessEvaluator, contentManager, authorizationService);

        if (problem is not null)
        {
            return problem;
        }

        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            return TypedResults.BadRequest("The antiforgery token is missing or not valid.");
        }

        if (httpContext.Request.ContentLength > WebMcpEndpoints.MaxRequestBodyLength)
        {
            return TypedResults.BadRequest("The tool input is too large.");
        }

        var arguments = await WebMcpToolRequests.ReadArgumentsAsync(httpContext.Request, httpContext.RequestAborted);

        if (arguments is null)
        {
            return TypedResults.BadRequest("The tool input must be a JSON object.");
        }

        var result = await toolServiceFactory.Create(part.Tools).InvokeAsync(httpContext.User, name, arguments, httpContext.RequestAborted);

        return WebMcpToolRequests.ToResult(result);
    }

    /// <summary>
    /// Checks the user and finds the published content item's WebMCP part. A content item that does not exist, is not
    /// published, has no tools, or that the user may not view answers 404, so the endpoint does not tell which exist.
    /// </summary>
    private static async Task<(IResult Problem, WebMcpPart Part)> ResolveAsync(
        string contentItemId,
        HttpContext httpContext,
        IWebMcpAccessEvaluator accessEvaluator,
        IContentManager contentManager,
        IAuthorizationService authorizationService)
    {
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return (TypedResults.Unauthorized(), null);
        }

        if (!await accessEvaluator.CanUseAsync(user, httpContext.RequestAborted))
        {
            return (TypedResults.Forbid(), null);
        }

        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            return (TypedResults.NotFound(), null);
        }

        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contentItem is null ||
            !contentItem.TryGet<WebMcpPart>(out var part) ||
            part.Tools is not { Count: > 0 } ||
            !await authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem))
        {
            return (TypedResults.NotFound(), null);
        }

        return (null, part);
    }
}
