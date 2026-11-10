using CrestApps.Core.AI.WebMcp.Endpoints;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.AI.WebMcp.Endpoints;

/// <summary>
/// Maps the endpoints the WebMCP page script calls on the storefront to list and call the storefront tools: the
/// selected tools marked safe for anonymous callers. Anyone may call them, signed in or not, but only while the
/// <see cref="WebMcpSettings.EnableStorefrontTools"/> setting is on, and only for the marked tools.
/// </summary>
internal static class StorefrontWebMcpEndpoints
{
    /// <summary>
    /// Maps <c>GET api/ai/webmcp/storefront/tools</c> and <c>POST api/ai/webmcp/storefront/tools/{name}/invoke</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapStorefrontWebMcpTools(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // The invoke endpoint validates the antiforgery token itself, from the header the page script sends.
        routes.MapGet(WebMcpConstants.StorefrontToolsRoute, GetTools)
            .DisableAntiforgery()
            .WithRateLimitGroup(WebMcpConstants.StorefrontRateLimitGroup);

        routes.MapPost(WebMcpConstants.StorefrontInvokeRoute, InvokeAsync)
            .DisableAntiforgery()
            .WithRateLimitGroup(WebMcpConstants.StorefrontRateLimitGroup);

        return routes;
    }

    /// <summary>
    /// Lists the storefront tools.
    /// </summary>
    /// <param name="options">The WebMCP page options, which turn the storefront tools on.</param>
    /// <param name="toolService">The storefront tool service.</param>
    /// <returns>The tools, or <c>404</c> when the storefront tools are off.</returns>
    internal static IResult GetTools(
        [FromServices] IOptionsSnapshot<WebMcpPageOptions> options,
        [FromServices] StorefrontWebMcpToolService toolService)
    {
        if (!options.Value.EnableStorefrontTools)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new WebMcpToolListResponse
        {
            Tools = toolService.GetTools(),
        });
    }

    /// <summary>
    /// Calls one storefront tool.
    /// </summary>
    /// <param name="name">The function name of the tool.</param>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="options">The WebMCP page options, which turn the storefront tools on.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="toolService">The storefront tool service.</param>
    /// <returns>The tool's answer as an MCP tool result, or the reason the call is refused.</returns>
    internal static async Task<IResult> InvokeAsync(
        [FromRoute] string name,
        HttpContext httpContext,
        [FromServices] IOptionsSnapshot<WebMcpPageOptions> options,
        [FromServices] IAntiforgery antiforgery,
        [FromServices] StorefrontWebMcpToolService toolService)
    {
        if (!options.Value.EnableStorefrontTools)
        {
            return TypedResults.NotFound();
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

        var result = await toolService.InvokeAsync(name, arguments, httpContext.RequestAborted);

        return WebMcpToolRequests.ToResult(result);
    }
}
