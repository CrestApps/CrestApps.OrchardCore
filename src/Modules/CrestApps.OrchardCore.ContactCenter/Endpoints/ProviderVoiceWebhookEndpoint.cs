using System.Globalization;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
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

        string body;

        try
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            body = await reader.ReadToEndAsync(httpContext.RequestAborted);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

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
