using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Dialpad.Endpoints;

internal static class DialpadWebhookEndpoint
{
    public const long MaximumRequestBodySizeBytes = 1024 * 1024;

    public static IEndpointRouteBuilder AddDialpadWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/dialpad/webhook/call", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodySizeBytes));

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        IProviderWebhookInbox inbox,
        IProviderWebhookIngressLimiter ingressLimiter,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DialpadContactCenterStartup> logger,
        HttpContext httpContext)
    {
        var settings = await siteService.GetSettingsAsync<DialpadSettings>();

        if (!settings.IsEnabled)
        {
            return TypedResults.NotFound();
        }

        var environment = settings.GetActiveEnvironmentSettings();

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // The body arrives at whatever speed the caller chooses to send it, so buffering it is admission-controlled:
        // the permit below bounds how many bodies this tenant holds at once.
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

        if (string.IsNullOrEmpty(environment.WebhookSigningSecret))
        {
            logger.LogWarning("Rejected a Dialpad webhook because no webhook signing secret is configured.");

            return TypedResults.Unauthorized();
        }

        if (!TryUnprotectSecret(dataProtectionProvider, environment.WebhookSigningSecret, out var secret))
        {
            logger.LogError("Rejected a Dialpad webhook because the configured signing secret could not be unprotected.");

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!DialpadJwtValidator.TryValidateAndExtract(body, secret, out var payloadJson))
        {
            logger.LogWarning("Rejected a Dialpad webhook because the signature could not be validated.");

            return TypedResults.Unauthorized();
        }

        using var rateLease = await ingressLimiter.AcquireRateAsync(DialpadConstants.ProviderTechnicalName, CancellationToken.None);

        if (!rateLease.IsAcquired)
        {
            SetRetryAfter(httpContext, rateLease.RetryAfter);

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        DialpadCallEvent callEvent;

        try
        {
            callEvent = JsonSerializer.Deserialize<DialpadCallEvent>(payloadJson, DialpadJsonSerializerOptions.Default);
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
            logger.LogWarning("Rejected a Dialpad webhook because its signed event timestamp was missing, stale, or too far in the future.");

            return TypedResults.BadRequest();
        }

        var acceptance = await inbox.AcceptAsync(new ProviderWebhookInboxDelivery
        {
            ProviderName = DialpadConstants.ProviderTechnicalName,
            DeliveryId = DialpadWebhookDelivery.GetDeliveryId(callEvent),
            HandlerName = DialpadWebhookInboxHandler.HandlerTechnicalName,
            Payload = JsonSerializer.Serialize(callEvent, DialpadJsonSerializerOptions.Default),
        }, CancellationToken.None);

        if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Busy)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            await inbox.DispatchAsync(acceptance.MessageId, CancellationToken.None);
        }
        catch (ConcurrencyException)
        {
            // A concurrent worker won ownership of the affected call during immediate dispatch. The delivery
            // is already durably accepted, so the background inbox completes it in a fresh scope; the canceled
            // session must not be reused, so acknowledge acceptance without failing the webhook.
        }

        return TypedResults.Ok(new
        {
            accepted = acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted,
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
            var protector = dataProtectionProvider.CreateProtector(DialpadConstants.WebhookProtectorName);
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
