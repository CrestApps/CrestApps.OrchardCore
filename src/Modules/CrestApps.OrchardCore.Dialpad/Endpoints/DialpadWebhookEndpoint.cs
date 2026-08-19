using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Received Dialpad webhook request. TraceIdentifier: {TraceIdentifier}, Request: {RequestSummary}",
                httpContext.TraceIdentifier,
                FormatRequest(httpContext.Request));
        }

        if (!settings.IsEnabled)
        {
            logger.LogWarning("Rejected the Dialpad webhook because the Dialpad provider is disabled.");

            return TypedResults.NotFound();
        }

        var environment = settings.GetActiveEnvironmentSettings();

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            logger.LogWarning(
                "Rejected the Dialpad webhook because the request body length {ContentLength} exceeds the maximum allowed size of {MaximumRequestBodySizeBytes} bytes.",
                httpContext.Request.ContentLength,
                MaximumRequestBodySizeBytes);

            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // The body arrives at whatever speed the caller chooses to send it, so buffering it is admission-controlled:
        // the permit below bounds how many bodies this tenant holds at once.
        using var concurrencyLease = await ingressLimiter.AcquireConcurrencyAsync(httpContext.RequestAborted);

        if (!concurrencyLease.IsAcquired)
        {
            SetRetryAfter(httpContext, concurrencyLease.RetryAfter);

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Rejected the Dialpad webhook because the ingress concurrency limit was reached. RetryAfterSeconds: {RetryAfterSeconds}",
                    GetRetryAfterSeconds(concurrencyLease.RetryAfter));
            }

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var read = await RequestBodyReader.ReadAsync(httpContext.Request, MaximumRequestBodySizeBytes, httpContext.RequestAborted);

        if (read.IsTooLarge)
        {
            logger.LogWarning("Rejected the Dialpad webhook because the streamed request body exceeded the maximum allowed size of {MaximumRequestBodySizeBytes} bytes.", MaximumRequestBodySizeBytes);

            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = read.Body;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Dialpad webhook raw request body. TraceIdentifier: {TraceIdentifier}, BodyLength: {BodyLength}, Body: {Body}",
                httpContext.TraceIdentifier,
                body.Length,
                body);
        }

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Validated Dialpad webhook payload. TraceIdentifier: {TraceIdentifier}, Payload: {Payload}",
                httpContext.TraceIdentifier,
                payloadJson);
        }

        using var rateLease = await ingressLimiter.AcquireRateAsync(DialpadConstants.ProviderTechnicalName, CancellationToken.None);

        if (!rateLease.IsAcquired)
        {
            SetRetryAfter(httpContext, rateLease.RetryAfter);

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Rejected the Dialpad webhook because the provider rate limit was reached. RetryAfterSeconds: {RetryAfterSeconds}",
                    GetRetryAfterSeconds(rateLease.RetryAfter));
            }

            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        DialpadCallEvent callEvent;

        try
        {
            callEvent = JsonSerializer.Deserialize<DialpadCallEvent>(payloadJson, DialpadJsonSerializerOptions.Default);
        }
        catch (JsonException)
        {
            logger.LogWarning(
                "Rejected the Dialpad webhook because the validated payload could not be deserialized. TraceIdentifier: {TraceIdentifier}, Payload: {Payload}",
                httpContext.TraceIdentifier,
                payloadJson);

            return TypedResults.BadRequest();
        }

        if (callEvent is null)
        {
            logger.LogWarning(
                "Rejected the Dialpad webhook because the validated payload deserialized to null. TraceIdentifier: {TraceIdentifier}, Payload: {Payload}",
                httpContext.TraceIdentifier,
                payloadJson);

            return TypedResults.BadRequest();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Parsed Dialpad webhook event. TraceIdentifier: {TraceIdentifier}, CallId: {CallId}, State: {State}, EventTimestamp: {EventTimestamp}, Event: {Event}",
                httpContext.TraceIdentifier,
                callEvent.CallId,
                callEvent.State,
                callEvent.EventTimestamp,
                JsonSerializer.Serialize(callEvent, DialpadJsonSerializerOptions.Default));
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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Dialpad webhook delivery accepted by the durable inbox. TraceIdentifier: {TraceIdentifier}, DeliveryId: {DeliveryId}, MessageId: {MessageId}, Status: {Status}",
                httpContext.TraceIdentifier,
                DialpadWebhookDelivery.GetDeliveryId(callEvent),
                acceptance.MessageId,
                acceptance.Status);
        }

        if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Busy)
        {
            logger.LogWarning(
                "Dialpad webhook dispatch was deferred because the durable inbox is busy. TraceIdentifier: {TraceIdentifier}, MessageId: {MessageId}",
                httpContext.TraceIdentifier,
                acceptance.MessageId);

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            await inbox.DispatchAsync(acceptance.MessageId, CancellationToken.None);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Dispatched the Dialpad webhook for processing. TraceIdentifier: {TraceIdentifier}, MessageId: {MessageId}",
                    httpContext.TraceIdentifier,
                    acceptance.MessageId);
            }
        }
        catch (ConcurrencyException)
        {
            // A concurrent worker won ownership of the affected call during immediate dispatch. The delivery
            // is already durably accepted, so the background inbox completes it in a fresh scope; the canceled
            // session must not be reused, so acknowledge acceptance without failing the webhook.

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "A concurrent worker took ownership of the Dialpad webhook after durable acceptance. TraceIdentifier: {TraceIdentifier}, MessageId: {MessageId}",
                    httpContext.TraceIdentifier,
                    acceptance.MessageId);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Returning a successful Dialpad webhook response. TraceIdentifier: {TraceIdentifier}, Accepted: {Accepted}",
                httpContext.TraceIdentifier,
                acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted);
        }

        return TypedResults.Ok(new
        {
            accepted = acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted,
        });
    }

    private static string FormatRequest(HttpRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("Method=").Append(request.Method);
        builder.Append(", Scheme=").Append(request.Scheme);
        builder.Append(", Host=").Append(request.Host.Value);
        builder.Append(", PathBase=").Append(request.PathBase.Value);
        builder.Append(", Path=").Append(request.Path.Value);
        builder.Append(", QueryString=").Append(request.QueryString.Value);
        builder.Append(", ContentType=").Append(request.ContentType);
        builder.Append(", ContentLength=").Append(request.ContentLength);
        builder.Append(", Headers=").Append(FormatHeaders(request.Headers));

        return builder.ToString();
    }

    private static string FormatHeaders(IHeaderDictionary headers)
    {
        if (headers.Count == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder("[");
        var isFirst = true;

        foreach (var header in headers)
        {
            if (!isFirst)
            {
                builder.Append(", ");
            }

            isFirst = false;
            builder.Append(header.Key).Append('=');
            builder.Append(IsSensitiveHeader(header.Key) ? "[REDACTED]" : header.Value.ToString());
        }

        builder.Append(']');

        return builder.ToString();
    }

    private static double? GetRetryAfterSeconds(TimeSpan? retryAfter)
    {
        return retryAfter.HasValue ? Math.Ceiling(retryAfter.Value.TotalSeconds) : null;
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        return headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains("key", StringComparison.OrdinalIgnoreCase);
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
