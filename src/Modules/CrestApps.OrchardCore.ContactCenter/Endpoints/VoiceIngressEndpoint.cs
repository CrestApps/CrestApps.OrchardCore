using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class VoiceIngressEndpoint
{
    public static IEndpointRouteBuilder AddVoiceIngressEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/contact-center/voice/inbound", HandleAsync)
            .DisableAntiforgery();

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

        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Admission is bounded here rather than by a deadline on the routing call itself. Routing performs a
        // sequence of durable writes (activity, interaction, queue item, reservation, provider command) that is
        // not atomic, so cancelling it partway strands the call. Limiting how many routes may be in flight caps
        // resource consumption without ever tearing one, which is the same contract ProviderVoiceWebhookEndpoint
        // relies on.
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
