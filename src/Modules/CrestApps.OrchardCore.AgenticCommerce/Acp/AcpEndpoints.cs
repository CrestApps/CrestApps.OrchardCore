using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// The ACP REST binding: checkout sessions, and the discovery document at <c>/.well-known/acp.json</c>.
/// </summary>
public static class AcpEndpoints
{
    /// <summary>
    /// The idempotency rules of ACP: every POST needs a key; a retry with the same key and an equal body (compared as
    /// canonical JSON) gets the first answer with <c>Idempotent-Replayed</c>, another body gets 422.
    /// </summary>
    public static readonly IdempotencyPolicy Idempotency = new()
    {
        Lifetime = TimeSpan.FromHours(24),
        MaxKeyLength = 255,
        EchoKey = true,
        ReplayedHeader = "Idempotent-Replayed",
        MissingKey = _ => AcpResults.Error(StatusCodes.Status400BadRequest, "idempotency_key_required", "Send an Idempotency-Key header with every POST."),
        InvalidKey = _ => AcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "An idempotency key is 1 to 255 characters."),
        Fingerprint = Fingerprint,
        Conflict = _ => AcpResults.Error(StatusCodes.Status422UnprocessableEntity, "idempotency_conflict", "This idempotency key was used with a different request."),
        InFlight = httpContext =>
        {
            httpContext.Response.Headers[HeaderNames.RetryAfter] = "1";

            return AcpResults.Error(StatusCodes.Status409Conflict, "idempotency_in_flight", "A request with this idempotency key is still running.");
        },
    };

    /// <summary>
    /// Maps the ACP endpoints.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapAcp(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/" + AcpConstants.DiscoveryPath, DiscoveryAsync).AllowAnonymous().WithName("AcpDiscovery");

        var group = routes.MapGroup(AcpConstants.RoutePrefix)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<AcpRequestFilter>();

        group.MapPost("checkout_sessions", CreateAsync).WithIdempotency(Idempotency).WithName("AcpCreateCheckout");
        group.MapGet("checkout_sessions/{id}", GetAsync).WithName("AcpGetCheckout");
        group.MapPost("checkout_sessions/{id}", UpdateAsync).WithIdempotency(Idempotency).WithName("AcpUpdateCheckout");
        group.MapPost("checkout_sessions/{id}/complete", CompleteAsync).WithIdempotency(Idempotency).WithName("AcpCompleteCheckout");
        group.MapPost("checkout_sessions/{id}/cancel", CancelAsync).WithIdempotency(Idempotency).WithName("AcpCancelCheckout");

        return routes;
    }

    /// <summary>
    /// Returns the fingerprint a retry with the same idempotency key must match: the SHA-256 of the body's RFC 8785
    /// canonical JSON, so equal JSON is the same request whatever its key order or number form, or of the raw body when
    /// it isn't JSON.
    /// </summary>
    /// <param name="json">The body as JSON, or <see langword="null"/>.</param>
    /// <param name="body">The raw body, used when it isn't JSON.</param>
    public static string ComputeFingerprint(JsonNode json, byte[] body = null)
    {
        var canonical = json is null ? body ?? [] : JsonCanonicalizer.CanonicalizeToUtf8(json);

        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private static string Fingerprint(HttpContext httpContext)
    {
        var request = AgentRequest.Get(httpContext);

        return ComputeFingerprint(request?.Json, request?.Body);
    }

    private static async Task<IResult> DiscoveryAsync(HttpContext httpContext, AgentSiteUrls urls, IEnumerable<IAcpDiscoveryContributor> contributors)
    {
        var baseUrl = await urls.GetBaseUrlAsync();

        httpContext.Response.Headers.CacheControl = "public, max-age=3600";

        var document = new JsonObject
        {
            ["protocol"] = new JsonObject
            {
                ["name"] = "acp",
                ["version"] = AcpConstants.Versions[AcpConstants.Versions.Count - 1],
                ["supported_versions"] = new JsonArray([.. AcpConstants.Versions.Select(version => (JsonNode)version)]),
            },
            ["api_base_url"] = $"{baseUrl}/{AcpConstants.RoutePrefix}",
            ["transports"] = new JsonArray("rest"),
            ["capabilities"] = new JsonObject
            {
                ["services"] = new JsonArray("checkout", "orders"),
                ["extensions"] = new JsonArray(new JsonObject { ["name"] = "discount" }),
            },
        };

        foreach (var contributor in contributors)
        {
            await contributor.ContributeAsync(document, baseUrl);
        }

        return Results.Json(document);
    }

    private static async Task<IResult> CreateAsync(HttpContext httpContext, AcpCheckoutOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return ToResult(httpContext, await operations.CreateAsync(request, request.Json, httpContext.RequestAborted));
    }

    private static async Task<IResult> GetAsync(string id, HttpContext httpContext, AcpCheckoutOperations operations)
        => ToResult(httpContext, await operations.GetAsync(AgentRequest.Get(httpContext), id, httpContext.RequestAborted));

    private static async Task<IResult> UpdateAsync(string id, HttpContext httpContext, AcpCheckoutOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return ToResult(httpContext, await operations.UpdateAsync(request, id, request.Json, httpContext.RequestAborted));
    }

    private static async Task<IResult> CompleteAsync(string id, HttpContext httpContext, AcpCheckoutOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return ToResult(httpContext, await operations.CompleteAsync(request, id, request.Json, httpContext.RequestAborted));
    }

    private static async Task<IResult> CancelAsync(string id, HttpContext httpContext, AcpCheckoutOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return ToResult(httpContext, await operations.CancelAsync(request, id, request.Json, httpContext.RequestAborted));
    }

    private static IResult ToResult(HttpContext httpContext, AcpOperationResult result)
    {
        if (result.RetryAfterSeconds is { } seconds)
        {
            httpContext.Response.Headers[HeaderNames.RetryAfter] = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return AcpResults.Json(result.Body, result.StatusCode);
    }
}
