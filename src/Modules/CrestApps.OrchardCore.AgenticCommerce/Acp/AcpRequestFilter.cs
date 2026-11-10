using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Signs an ACP request in before the endpoint runs: it reads the body once, checks <c>API-Version</c>, and finds the
/// platform by its API key or OAuth bearer token.
/// </summary>
public sealed class AcpRequestFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;

        if (request.Headers["Request-Id"].ToString() is { Length: > 0 } requestId)
        {
            httpContext.Response.Headers["Request-Id"] = requestId;
        }

        var version = request.Headers[AcpConstants.VersionHeader].ToString();

        if (string.IsNullOrWhiteSpace(version) || !AcpConstants.Versions.Contains(version.Trim(), StringComparer.Ordinal))
        {
            return Results.Json(new JsonObject
            {
                ["type"] = "invalid_request",
                ["code"] = string.IsNullOrWhiteSpace(version) ? "missing_api_version" : "unsupported_api_version",
                ["message"] = string.IsNullOrWhiteSpace(version) ? "Send the API-Version header." : $"API version '{version}' is not supported.",
                ["supported_versions"] = new JsonArray([.. AcpConstants.Versions.Reverse().Select(entry => (JsonNode)entry)]),
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        var body = await AgentRequestBody.ReadAsync(request);

        if (body is null)
        {
            return AcpResults.Error(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request is too large.");
        }

        var authentication = await httpContext.RequestServices.GetRequiredService<AgentPlatformAuthenticator>().AuthenticateAsync(httpContext, AgenticCommerceConstants.Protocols.Acp);

        if (!authentication.Succeeded)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

            return AcpResults.Error(
                authentication.StatusCode == 0 ? StatusCodes.Status401Unauthorized : authentication.StatusCode,
                authentication.StatusCode == StatusCodes.Status403Forbidden ? "forbidden" : "unauthorized",
                authentication.Error ?? "Sign in with the platform's API key.");
        }

        var platform = authentication.Platform;
        var agentRequest = new AgentRequest
        {
            Platform = platform,
            Body = body,
            Json = AgentRequestBody.Parse(body),
            Version = version.Trim(),
            Context = new AgentCheckoutContext
            {
                PlatformId = platform.ItemId,
                Protocol = AgenticCommerceConstants.Protocols.Acp,
                ProtocolVersion = version.Trim(),
                TestMode = platform.TestMode,
                AllowedPaymentHandlers = platform.AllowedPaymentHandlers ?? [],
                UserId = authentication.UserId,
                ClientAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            },
        };

        agentRequest.Set(httpContext);
        httpContext.User = AgentPrincipal.Create(platform.ItemId);

        return await next(context);
    }
}
