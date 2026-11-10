using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
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

    private static string Fingerprint(HttpContext httpContext)
    {
        var request = AgentRequest.Get(httpContext);

        // Equal JSON is the same request, whatever its key order or number form.
        var canonical = request?.Json is null ? request?.Body ?? [] : JsonCanonicalizer.CanonicalizeToUtf8(request.Json);

        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private static async Task<IResult> DiscoveryAsync(HttpContext httpContext, AgentSiteUrls urls)
    {
        var baseUrl = await urls.GetBaseUrlAsync();

        httpContext.Response.Headers.CacheControl = "public, max-age=3600";

        return Results.Json(new JsonObject
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
        });
    }

    private static async Task<IResult> CreateAsync(HttpContext httpContext, IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return AcpResults.Error(StatusCodes.Status400BadRequest, "invalid", "Send the checkout as a JSON object.");
        }

        request.Context.RunsThreeDSecure = AcpCheckoutMapper.ReadRunsThreeDSecure(body);

        var result = await checkouts.CreateAsync(request.Context, AcpCheckoutMapper.ReadInput(body), httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Fail(result, httpContext);
        }

        return AcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted), StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);
        var result = await checkouts.GetAsync(request.Context, id, httpContext.RequestAborted);

        return result.Succeeded
            ? AcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : Fail(result, httpContext);
    }

    private static async Task<IResult> UpdateAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return AcpResults.Error(StatusCodes.Status400BadRequest, "invalid", "Send the changes as a JSON object.");
        }

        var current = await checkouts.GetAsync(request.Context, id, httpContext.RequestAborted);

        if (!current.Succeeded)
        {
            return Fail(current, httpContext);
        }

        var result = await checkouts.UpdateAsync(request.Context, id, AcpCheckoutMapper.ReadUpdate(body, current.View), httpContext.RequestAborted);

        return result.Succeeded
            ? AcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : Fail(result, httpContext);
    }

    private static async Task<IResult> CompleteAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return AcpResults.Error(StatusCodes.Status400BadRequest, "invalid", "Send the payment as a JSON object.");
        }

        if (AcpCheckoutMapper.ReadPayment(body) is not { } payment || string.IsNullOrEmpty(payment.Credential))
        {
            return AcpResults.Error(StatusCodes.Status400BadRequest, "missing", "Send payment_data with the payment handler and its credential.", "$.payment_data");
        }

        AcpCheckoutMapper.ReadAuthenticationResult(body, out var invalidAuthentication);

        if (invalidAuthentication is not null)
        {
            return AcpResults.Error(StatusCodes.Status400BadRequest, "invalid", "The 3-D Secure result isn't complete or valid.", invalidAuthentication);
        }

        var update = new AgentCheckoutUpdate
        {
            Signals = AcpCheckoutMapper.ReadSignals(body),
            Consents = AcpCheckoutMapper.ReadConsents(body),
            Attribution = body["affiliate_attribution"]?.DeepClone() as JsonObject,
            OrderNotes = AgentJson.String(body, "order_notes"),
        };

        if (body["buyer"] is JsonObject)
        {
            update.Buyer = AcpCheckoutMapper.ReadInput(new JsonObject { ["buyer"] = body["buyer"].DeepClone() }).Buyer;
        }

        var result = await checkouts.CompleteAsync(request.Context, id, payment, update, httpContext.RequestAborted);

        return result.Succeeded
            ? AcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : Fail(result, httpContext);
    }

    private static async Task<IResult> CancelAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);
        var trace = (request.Json as JsonObject)?["intent_trace"]?.DeepClone() as JsonObject;
        var result = await checkouts.CancelAsync(request.Context, id, trace, httpContext.RequestAborted);

        if (result.Failure == AgentCheckoutFailure.InvalidState)
        {
            return AcpResults.Error(StatusCodes.Status405MethodNotAllowed, "invalid_state", result.Error);
        }

        return result.Succeeded
            ? AcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : Fail(result, httpContext);
    }

    private static IResult Fail(AgentCheckoutResult result, HttpContext httpContext)
    {
        switch (result.Failure)
        {
            case AgentCheckoutFailure.NotFound:
                return AcpResults.Error(StatusCodes.Status404NotFound, "not_found", result.Error);

            case AgentCheckoutFailure.Busy:
                httpContext.Response.Headers[HeaderNames.RetryAfter] = "1";

                return AcpResults.Error(StatusCodes.Status409Conflict, "conflict", result.Error);

            case AgentCheckoutFailure.TooMany:
                return AcpResults.Error(StatusCodes.Status429TooManyRequests, "rate_limited", result.Error);

            case AgentCheckoutFailure.InvalidState:
                return AcpResults.Error(StatusCodes.Status409Conflict, "invalid_state", result.Error);

            case AgentCheckoutFailure.AuthenticationRequired:
                // A checkout waiting for authentication is completed with the result (ACP 3-D Secure flow).
                return AcpResults.Error(StatusCodes.Status400BadRequest, "requires_3ds", result.Error, "$.authentication_result");
        }

        return AcpResults.Error(StatusCodes.Status400BadRequest, "invalid", result.Error);
    }
}
