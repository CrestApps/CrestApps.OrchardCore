using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DialPad.Endpoints;

internal static class DialPadWebhookEndpoint
{
    public const long MaximumRequestBodySizeBytes = 1024 * 1024;

    public static IEndpointRouteBuilder AddDialPadWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/dialpad/webhook/call", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodySizeBytes));

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        IDialPadWebhookService webhookService,
        IProviderWebhookIngressLimiter ingressLimiter,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DialPadContactCenterStartup> logger,
        HttpContext httpContext)
    {
        var settings = await siteService.GetSettingsAsync<DialPadSettings>();

        if (!settings.IsEnabled)
        {
            return TypedResults.NotFound();
        }

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
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

        if (string.IsNullOrEmpty(settings.WebhookSigningSecret))
        {
            logger.LogWarning("Rejected a DialPad webhook because no webhook signing secret is configured.");

            return TypedResults.Unauthorized();
        }

        if (!TryUnprotectSecret(dataProtectionProvider, settings.WebhookSigningSecret, out var secret))
        {
            logger.LogError("Rejected a DialPad webhook because the configured signing secret could not be unprotected.");

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!DialPadJwtValidator.TryValidateAndExtract(body, secret, out var payloadJson))
        {
            logger.LogWarning("Rejected a DialPad webhook because the signature could not be validated.");

            return TypedResults.Unauthorized();
        }

        using var rateLease = await ingressLimiter.AcquireRateAsync(DialPadConstants.ProviderTechnicalName, CancellationToken.None);

        if (!rateLease.IsAcquired)
        {
            SetRetryAfter(httpContext, rateLease.RetryAfter);

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        DialPadCallEvent callEvent;

        try
        {
            callEvent = JsonSerializer.Deserialize<DialPadCallEvent>(payloadJson, DialPadJsonSerializerOptions.Default);
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest();
        }

        if (callEvent is null)
        {
            return TypedResults.BadRequest();
        }

        if (!callEvent.EventTimestamp.HasValue ||
            !TryGetOccurredUtc(callEvent.EventTimestamp.Value, out var occurredUtc) ||
            !ingressLimiter.IsFresh(occurredUtc))
        {
            logger.LogWarning("Rejected a DialPad webhook because its signed event timestamp was missing, stale, or too far in the future.");

            return TypedResults.BadRequest();
        }

        var result = await webhookService.ProcessAsync(callEvent, CancellationToken.None);

        return TypedResults.Ok(new
        {
            result = result.ToString(),
        });
    }

    private static bool TryUnprotectSecret(
        IDataProtectionProvider dataProtectionProvider,
        string protectedSecret,
        out string secret)
    {
        secret = null;

        try
        {
            var protector = dataProtectionProvider.CreateProtector(DialPadConstants.WebhookProtectorName);
            secret = protector.Unprotect(protectedSecret);

            return !string.IsNullOrEmpty(secret);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static void SetRetryAfter(HttpContext httpContext, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool TryGetOccurredUtc(long eventTimestamp, out DateTime occurredUtc)
    {
        try
        {
            occurredUtc = DateTimeOffset.FromUnixTimeMilliseconds(eventTimestamp).UtcDateTime;

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            occurredUtc = default;

            return false;
        }
    }
}
