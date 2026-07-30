using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class ProviderVoiceWebhookEndpoint
{
    internal const long MaximumRequestBodySizeBytes = 1024 * 1024;

    public static IEndpointRouteBuilder AddProviderVoiceWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/contact-center/voice/webhook/{provider}", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodySizeBytes));

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string provider,
        IProviderVoiceWebhookProcessor processor,
        IProviderWebhookIngressLimiter ingressLimiter,
        IContactCenterFeatureWorkManager workManager,
        HttpContext httpContext)
    {
        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // The body arrives at whatever speed the caller chooses to send it, so buffering it is admission-controlled:
        // the leases below bound how many bodies this tenant holds at once, and a caller that sends slowly is the
        // server's minimum-data-rate problem rather than a way to make this process hold unbounded memory.
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var concurrencyLease = await ingressLimiter.AcquireConcurrencyAsync(httpContext.RequestAborted);

        if (!concurrencyLease.IsAcquired)
        {
            SetRetryAfter(httpContext, concurrencyLease.RetryAfter);

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var read = await RequestBodyReader.ReadAsync(httpContext.Request, MaximumRequestBodySizeBytes, httpContext.RequestAborted);

        if (read.IsTooLarge)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = read.Body;

        var request = new ProviderVoiceWebhookRequest
        {
            Provider = provider,
            Body = body,
        };

        foreach (var header in httpContext.Request.Headers)
        {
            request.Headers[header.Key] = header.Value.ToString();
        }

        foreach (var query in httpContext.Request.Query)
        {
            request.Query[query.Key] = query.Value.ToString();
        }

        var outcome = await processor.ProcessAsync(request, CancellationToken.None);

        return outcome.Status switch
        {
            ProviderVoiceWebhookStatus.Accepted => TypedResults.Ok(new { processed = outcome.ProcessedCount }),
            ProviderVoiceWebhookStatus.UnknownProvider => TypedResults.NotFound(),
            ProviderVoiceWebhookStatus.InvalidSignature => TypedResults.Unauthorized(),
            ProviderVoiceWebhookStatus.MissingIdempotencyKey => TypedResults.BadRequest(),
            ProviderVoiceWebhookStatus.RateLimited => CreateRateLimitedResult(httpContext, outcome.RetryAfter),
            ProviderVoiceWebhookStatus.StaleDelivery => TypedResults.BadRequest(),
            ProviderVoiceWebhookStatus.InboxBusy => TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(),
        };
    }

    private static StatusCodeHttpResult CreateRateLimitedResult(HttpContext httpContext, TimeSpan? retryAfter)
    {
        SetRetryAfter(httpContext, retryAfter);

        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    private static void SetRetryAfter(HttpContext httpContext, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }
}
