using CrestApps.Core.ContactCenter;
using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

public static class VoiceIngressEndpoint
{
    /// <summary>
    /// Maps the inbound voice ingress endpoint.
    /// </summary>
    /// <remarks>
    /// Where a provider hands an inbound call to the Contact Center.
    /// </remarks>
    /// <param name="builder">The route builder to map onto.</param>
    /// <param name="configure">
    /// Applied to every route mapped here, so a host can add its own filters or metadata.
    /// </param>
    /// <returns>The same route builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapContactCenterVoiceIngressEndpoint(
        this IEndpointRouteBuilder builder,
        Action<RouteHandlerBuilder> configure = null)
    {
        var route = builder.MapPost("api/contact-center/voice/inbound", HandleAsync)
            .DisableAntiforgery();

        configure?.Invoke(route);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        InboundVoiceEvent inboundEvent,
        IAuthorizationService authorizationService,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IProviderWebhookIngressLimiter ingressLimiter,
        IContactCenterFeatureWorkManager workManager,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.ManageInteractions))
        {
            return TypedResults.Forbid();
        }

        if (inboundEvent is null)
        {
            return TypedResults.BadRequest();
        }

        using var workLease = workManager.TryEnter(ContactCenterCapabilities.Voice);

        if (workLease is null)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Admission is bounded here rather than by a deadline on the routing call itself. Routing performs a
        // sequence of durable writes (activity, interaction, queue item, reservation, provider command) that is
        // not atomic, so cancelling it partway strands the call. Limiting how many routes may be in flight caps
        // resource consumption without ever tearing one, which is the same contract every provider-owned webhook
        // endpoint relies on.
        using var concurrencyLease = await ingressLimiter.AcquireConcurrencyAsync(httpContext.RequestAborted);

        if (!concurrencyLease.IsAcquired)
        {
            SetRetryAfter(httpContext, concurrencyLease.RetryAfter);

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        // Deliberately not the request token. A caller who hangs up must not abandon the routing sequence.
        var result = await voiceCallRouter.RouteInboundAsync(inboundEvent, CancellationToken.None);

        return TypedResults.Ok(result);
    }

    private static void SetRetryAfter(HttpContext httpContext, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }
}
