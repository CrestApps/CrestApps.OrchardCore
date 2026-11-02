using System.Text.Json;
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

        var arguments = await ReadArgumentsAsync(httpContext.Request, httpContext.RequestAborted);

        if (arguments is null)
        {
            return TypedResults.BadRequest("The tool input must be a JSON object.");
        }

        var result = await toolServiceFactory.Create(part.Tools).InvokeAsync(httpContext.User, name, arguments, httpContext.RequestAborted);

        if (result.Status == WebMcpToolInvocationStatus.NotFound)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new WebMcpToolCallResponse
        {
            Content =
            [
                new WebMcpTextContent
                {
                    Text = result.Text,
                },
            ],
            IsError = result.IsError,
        });
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
        var part = contentItem?.As<WebMcpPart>();

        if (part is null ||
            part.Tools is not { Count: > 0 } ||
            !await authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem))
        {
            return (TypedResults.NotFound(), null);
        }

        return (null, part);
    }

    private static async Task<IDictionary<string, object>> ReadArgumentsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal);

        if (request.ContentLength == 0)
        {
            return arguments;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.Clone();
            }

            return arguments;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
