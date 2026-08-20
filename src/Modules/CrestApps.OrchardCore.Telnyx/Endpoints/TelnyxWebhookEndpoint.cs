using System.Globalization;
using System.Security.Cryptography;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Endpoints;

internal static class TelnyxWebhookEndpoint
{
    public const long MaximumRequestBodySizeBytes = 1024 * 1024;

    public static IEndpointRouteBuilder AddTelnyxWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(TelnyxConstants.WebhookPath, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodySizeBytes));

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ITelnyxWebhookService webhookService,
        IClock clock,
        ILogger<Startup> logger,
        HttpContext httpContext)
    {
        var ingressLimiter = httpContext.RequestServices.GetService<IProviderWebhookIngressLimiter>();
        var inbox = httpContext.RequestServices.GetService<IProviderWebhookInbox>();
        var settings = await siteService.GetSettingsAsync<TelnyxSettings>();

        if (!settings.IsEnabled)
        {
            logger.LogWarning("Rejected the Telnyx webhook because the Telnyx provider is disabled.");

            return TypedResults.NotFound();
        }

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        ProviderWebhookIngressLease concurrencyLease = null;

        try
        {
            if (ingressLimiter is not null)
            {
                concurrencyLease = await ingressLimiter.AcquireConcurrencyAsync(httpContext.RequestAborted);

                if (!concurrencyLease.IsAcquired)
                {
                    SetRetryAfter(httpContext, concurrencyLease.RetryAfter);

                    return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                }
            }

            var read = await RequestBodyReader.ReadAsync(httpContext.Request, MaximumRequestBodySizeBytes, httpContext.RequestAborted);

            if (read.IsTooLarge)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var body = read.Body;

            if (string.IsNullOrEmpty(settings.WebhookPublicKey))
            {
                logger.LogWarning("Rejected a Telnyx webhook because no webhook public key is configured.");

                return TypedResults.Unauthorized();
            }

            if (!TryUnprotectSecret(dataProtectionProvider, settings.WebhookPublicKey, out var publicKey))
            {
                logger.LogError("Rejected a Telnyx webhook because the configured webhook public key could not be unprotected.");

                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var signature = httpContext.Request.Headers[TelnyxConstants.SignatureHeaderName].ToString();
            var timestamp = httpContext.Request.Headers[TelnyxConstants.TimestampHeaderName].ToString();

            if (!TelnyxWebhookSignatureValidator.TryValidate(publicKey, signature, timestamp, body))
            {
                logger.LogWarning("Rejected a Telnyx webhook because the signature could not be validated.");

                return TypedResults.Unauthorized();
            }

            if (!TelnyxCallEventParser.TryParse(body, out var callEvent))
            {
                logger.LogWarning(
                    "Rejected the Telnyx webhook because the validated payload could not be parsed. TraceIdentifier: {TraceIdentifier}",
                    httpContext.TraceIdentifier);

                return TypedResults.BadRequest();
            }

            var occurredUtc = ResolveOccurredUtc(timestamp, callEvent, clock.UtcNow);

            if (!IsFresh(ingressLimiter, occurredUtc, clock.UtcNow))
            {
                logger.LogWarning("Rejected a Telnyx webhook because its signed timestamp was stale or too far in the future.");

                return TypedResults.BadRequest();
            }

            callEvent.OccurredUtc = occurredUtc;

            ProviderWebhookIngressLease rateLease = null;

            try
            {
                if (ingressLimiter is not null)
                {
                    rateLease = await ingressLimiter.AcquireRateAsync(TelnyxConstants.ProviderTechnicalName, CancellationToken.None);

                    if (!rateLease.IsAcquired)
                    {
                        SetRetryAfter(httpContext, rateLease.RetryAfter);

                        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                    }
                }

                if (inbox is null)
                {
                    var result = await webhookService.ProcessAsync(callEvent, CancellationToken.None);

                    return TypedResults.Ok(new
                    {
                        accepted = true,
                        result,
                    });
                }

                var acceptance = await inbox.AcceptAsync(new ProviderWebhookInboxDelivery
                {
                    ProviderName = TelnyxConstants.ProviderTechnicalName,
                    DeliveryId = TelnyxWebhookDelivery.GetDeliveryId(callEvent),
                    HandlerName = TelnyxWebhookInboxHandler.HandlerTechnicalName,
                    Payload = System.Text.Json.JsonSerializer.Serialize(callEvent, TelnyxJsonSerializerOptions.Default),
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
                    // A concurrent worker won ownership during immediate dispatch. The delivery is already
                    // durably accepted, so the background inbox completes it in a fresh scope.
                }

                return TypedResults.Ok(new
                {
                    accepted = acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted,
                });
            }
            finally
            {
                rateLease?.Dispose();
            }
        }
        finally
        {
            concurrencyLease?.Dispose();
        }
    }

    private static DateTime ResolveOccurredUtc(string timestamp, TelnyxCallEvent callEvent, DateTime nowUtc)
    {
        if (long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to the event's own timestamp.
            }
        }

        return callEvent.OccurredUtc ?? nowUtc;
    }

    private static bool IsFresh(IProviderWebhookIngressLimiter ingressLimiter, DateTime occurredUtc, DateTime nowUtc)
    {
        if (ingressLimiter is not null)
        {
            return ingressLimiter.IsFresh(occurredUtc);
        }

        return occurredUtc >= nowUtc.AddSeconds(-900) &&
            occurredUtc <= nowUtc.AddSeconds(120);
    }

    private static bool TryUnprotectSecret(
        IDataProtectionProvider dataProtectionProvider,
        string protectedSecret,
        out string secret)
    {
        secret = null;

        try
        {
            var protector = dataProtectionProvider.CreateProtector(TelnyxConstants.WebhookProtectorName);
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
}
