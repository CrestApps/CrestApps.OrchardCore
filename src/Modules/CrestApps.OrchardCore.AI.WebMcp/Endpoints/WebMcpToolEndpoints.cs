using System.Text.Json;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.WebMcp.Endpoints;

/// <summary>
/// The endpoints the page script calls to list and call the site's WebMCP tools.
/// </summary>
internal static class WebMcpToolEndpoints
{
    /// <summary>
    /// The largest tool input, in bytes, the invoke endpoint reads.
    /// </summary>
    public const long MaxRequestBodyLength = 256 * 1024;

    /// <summary>
    /// Maps the WebMCP tool endpoints.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddWebMcpToolEndpoints(this IEndpointRouteBuilder builder)
    {
        // The invoke endpoint validates the antiforgery token itself, from the request header the page script sends,
        // because the framework's automatic check only reads form posts.
        _ = builder.MapGet(WebMcpConstants.ToolsRoute, GetToolsAsync)
            .DisableAntiforgery();

        _ = builder.MapPost(WebMcpConstants.InvokeRoute, InvokeAsync)
            .DisableAntiforgery();

        return builder;
    }

    /// <summary>
    /// Lists the tools the signed-in user may call.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="toolService">The WebMCP tool service.</param>
    /// <returns>The tools, or the reason they are refused.</returns>
    internal static async Task<IResult> GetToolsAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IWebMcpToolService toolService)
    {
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Unauthorized();
        }

        if (!await authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.UseWebMcpTools))
        {
            return TypedResults.Forbid();
        }

        var tools = await toolService.GetToolsAsync(user, httpContext.RequestAborted);

        return TypedResults.Ok(new WebMcpToolListResponse
        {
            Tools = tools,
        });
    }

    /// <summary>
    /// Calls one tool for the signed-in user.
    /// </summary>
    /// <param name="name">The function name of the tool.</param>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="toolService">The WebMCP tool service.</param>
    /// <returns>The tool's answer as an MCP tool result, or the reason the call is refused.</returns>
    internal static async Task<IResult> InvokeAsync(
        [FromRoute] string name,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IAntiforgery antiforgery,
        [FromServices] IWebMcpToolService toolService)
    {
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Unauthorized();
        }

        if (!await authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.UseWebMcpTools))
        {
            return TypedResults.Forbid();
        }

        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            return TypedResults.BadRequest("The antiforgery token is missing or not valid.");
        }

        if (httpContext.Request.ContentLength > MaxRequestBodyLength)
        {
            return TypedResults.BadRequest("The tool input is too large.");
        }

        var arguments = await ReadArgumentsAsync(httpContext.Request, httpContext.RequestAborted);

        if (arguments is null)
        {
            return TypedResults.BadRequest("The tool input must be a JSON object.");
        }

        var result = await toolService.InvokeAsync(user, name, arguments, httpContext.RequestAborted);

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
    /// Reads the tool's arguments from the request body.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The arguments, an empty set for an empty body, or <see langword="null"/> when the body is not a JSON object.</returns>
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
