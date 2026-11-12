using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Signs an ACP request in before the endpoint runs: it reads the body once, checks <c>API-Version</c>, finds the
/// platform by its API key or OAuth bearer token, and verifies the request's <c>Signature</c> when the platform signs.
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

        if (CheckVersion(version) is { } versionError)
        {
            return Results.Json(versionError, statusCode: StatusCodes.Status400BadRequest);
        }

        var body = await AgentRequestBody.ReadAsync(request);

        if (body is null)
        {
            return AcpResults.Error(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request is too large.");
        }

        var signIn = await SignInAsync(httpContext, body, version.Trim());

        if (signIn.Error is not null)
        {
            return Results.Json(signIn.Error.Body, statusCode: signIn.Error.StatusCode);
        }

        var agentRequest = signIn.Request;
        var signature = request.Headers[AcpRequestSignature.SignatureHeader].ToString();
        var signatureError = AcpSignatureCheck.Check(
            agentRequest.Platform,
            AcpRequestSignature.TryGetSigningInput(body, out var input) ? input : null,
            signature,
            request.Headers[AcpRequestSignature.TimestampHeader].ToString(),
            request.Method,
            $"{request.PathBase}{request.Path}",
            GetNow(httpContext));

        if (signatureError is not null)
        {
            return AcpResults.Error(StatusCodes.Status401Unauthorized, AcpSignatureCheck.ErrorCode, signatureError);
        }

        // A signature is accepted once in its window; again only for the same idempotent request.
        if (!await AcpSignatureCheck.TryUseAsync(httpContext, agentRequest.Platform, signature, request.Headers["Idempotency-Key"].ToString()))
        {
            return AcpResults.Error(StatusCodes.Status401Unauthorized, AcpSignatureCheck.ErrorCode, "The signature was used before. Sign each request anew.");
        }

        agentRequest.Set(httpContext);
        httpContext.User = AgentPrincipal.Create(agentRequest.Platform.ItemId);

        return await next(context);
    }

    /// <summary>
    /// Returns the store's clock, as the time a request's <c>Timestamp</c> is checked against.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public static DateTimeOffset GetNow(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return new DateTimeOffset(DateTime.SpecifyKind(httpContext.RequestServices.GetRequiredService<IClock>().UtcNow, DateTimeKind.Utc));
    }

    /// <summary>
    /// Returns ACP's error for a missing or unsupported API version, or <see langword="null"/> when the store serves it.
    /// </summary>
    /// <param name="version">The version the agent named.</param>
    public static JsonObject CheckVersion(string version)
    {
        if (!string.IsNullOrWhiteSpace(version) && AcpConstants.Versions.Contains(version.Trim(), StringComparer.Ordinal))
        {
            return null;
        }

        return new JsonObject
        {
            ["type"] = "invalid_request",
            ["code"] = string.IsNullOrWhiteSpace(version) ? "missing_api_version" : "unsupported_api_version",
            ["message"] = string.IsNullOrWhiteSpace(version) ? "Send the API-Version header." : $"API version '{version}' is not supported.",
            ["supported_versions"] = new JsonArray([.. AcpConstants.Versions.Reverse().Select(entry => (JsonNode)entry)]),
        };
    }

    /// <summary>
    /// Finds the platform that sent an ACP request, by its API key or OAuth bearer token, and builds the request the
    /// checkout operations take. The REST and the MCP bindings both sign in this way.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="body">The raw body.</param>
    /// <param name="version">The ACP version of the request.</param>
    public static async Task<(AgentRequest Request, AcpOperationResult Error)> SignInAsync(HttpContext httpContext, byte[] body, string version)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var authentication = await httpContext.RequestServices.GetRequiredService<AgentPlatformAuthenticator>().AuthenticateAsync(httpContext, AgenticCommerceConstants.Protocols.Acp);

        if (!authentication.Succeeded)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

            return (null, AcpOperationResult.Error(
                authentication.StatusCode == 0 ? StatusCodes.Status401Unauthorized : authentication.StatusCode,
                authentication.StatusCode == StatusCodes.Status403Forbidden ? "forbidden" : "unauthorized",
                authentication.Error ?? "Sign in with the platform's API key."));
        }

        var platform = authentication.Platform;
        var request = new AgentRequest
        {
            Platform = platform,
            Body = body ?? [],
            Json = AgentRequestBody.Parse(body),
            Version = version,
            Context = new AgentCheckoutContext
            {
                PlatformId = platform.ItemId,
                Protocol = AgenticCommerceConstants.Protocols.Acp,
                ProtocolVersion = version,
                TestMode = platform.TestMode,
                AllowedPaymentHandlers = platform.AllowedPaymentHandlers ?? [],
                UserId = authentication.UserId,
                ClientAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            },
        };

        return (request, null);
    }
}
