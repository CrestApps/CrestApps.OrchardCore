using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Telnyx.Telemetry;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

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
        // What became of the delivery is counted by result, and tagged on the server span ASP.NET Core records around it.
        var receipt = new WebhookReceipt();

        try
        {
            return await HandleCoreAsync(siteService, dataProtectionProvider, webhookService, clock, logger, httpContext, receipt);
        }
        catch
        {
            receipt.Result = CrestAppsOrchardCoreTelemetry.OutcomeError;

            throw;
        }
        finally
        {
            Activity.Current?.SetTag("crestapps.telnyx.webhook.type", receipt.Type);
            Activity.Current?.SetTag("crestapps.telnyx.webhook.result", receipt.Result);
            TelnyxTelemetry.RecordWebhook(TelnyxTelemetry.WebhookKindCall, receipt.Type, receipt.Result ?? CrestAppsOrchardCoreTelemetry.OutcomeError);
        }
    }

    private static async Task<IResult> HandleCoreAsync(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ITelnyxWebhookService webhookService,
        IClock clock,
        ILogger<Startup> logger,
        HttpContext httpContext,
        WebhookReceipt receipt)
    {
        var ingressLimiter = httpContext.RequestServices.GetService<IProviderWebhookIngressLimiter>();
        var inbox = httpContext.RequestServices.GetService<IProviderWebhookInbox>();
        // Health metrics (companion tooling): count webhooks that pass validation and are processed vs. those
        // that validate but cannot be parsed. Backpressure (429/503) and security rejections (auth/signature)
        // are deliberately not counted as delivery failures.
        var healthMetrics = httpContext.RequestServices.GetService<ISoftPhoneHealthMetrics>();
        var settings = await siteService.GetSettingsAsync<TelnyxSettings>();

        if (!settings.IsEnabled)
        {
            logger.LogWarning("Rejected the Telnyx webhook because the Telnyx provider is disabled.");
            receipt.Result = TelnyxTelemetry.WebhookDisabled;

            return TypedResults.NotFound();
        }

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            receipt.Result = TelnyxTelemetry.WebhookRejectedSize;

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
                    receipt.Result = TelnyxTelemetry.WebhookRejectedRate;

                    return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                }
            }

            var read = await RequestBodyReader.ReadAsync(httpContext.Request, MaximumRequestBodySizeBytes, httpContext.RequestAborted);

            if (read.IsTooLarge)
            {
                receipt.Result = TelnyxTelemetry.WebhookRejectedSize;

                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var body = read.Body;

            if (string.IsNullOrEmpty(settings.WebhookPublicKey))
            {
                logger.LogWarning("Rejected a Telnyx webhook because no webhook public key is configured.");
                receipt.Result = TelnyxTelemetry.WebhookRejectedSignature;

                return TypedResults.Unauthorized();
            }

            if (!TryUnprotectSecret(dataProtectionProvider, settings.WebhookPublicKey, out var publicKey))
            {
                logger.LogError("Rejected a Telnyx webhook because the configured webhook public key could not be unprotected.");
                receipt.Result = TelnyxTelemetry.WebhookRejectedSignature;

                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var signature = httpContext.Request.Headers[TelnyxConstants.SignatureHeaderName].ToString();
            var timestamp = httpContext.Request.Headers[TelnyxConstants.TimestampHeaderName].ToString();

            if (!TelnyxWebhookSignatureValidator.TryValidate(publicKey, signature, timestamp, body))
            {
                logger.LogWarning("Rejected a Telnyx webhook because the signature could not be validated.");
                receipt.Result = TelnyxTelemetry.WebhookRejectedSignature;

                return TypedResults.Unauthorized();
            }

            if (!TelnyxCallEventParser.TryParse(body, out var callEvent))
            {
                logger.LogWarning(
                    "Rejected the Telnyx webhook because the validated payload could not be parsed. TraceIdentifier: {TraceIdentifier}",
                    httpContext.TraceIdentifier);

                healthMetrics?.RecordWebhookProcessed(false);
                receipt.Result = TelnyxTelemetry.WebhookParseError;

                return TypedResults.BadRequest();
            }

            receipt.Type = TelnyxTelemetry.GetWebhookType(callEvent.EventType);
            Activity.Current?.SetTag("crestapps.telnyx.event.id", callEvent.EventId);

            // Every line the delivery writes from here names the event and the leg it is about.
            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["TelnyxEventId"] = callEvent.EventId,
                ["CallControlId"] = callEvent.CallControlId,
            });

            var signedUtc = ResolveSignedUtc(timestamp, callEvent, clock.UtcNow);

            if (!IsFresh(ingressLimiter, signedUtc, clock.UtcNow))
            {
                logger.LogWarning("Rejected a Telnyx webhook because its signed timestamp was stale or too far in the future.");
                receipt.Result = TelnyxTelemetry.WebhookRejectedStale;

                return TypedResults.BadRequest();
            }

            callEvent.OccurredUtc = ResolveOccurredUtc(callEvent, signedUtc);
            TelnyxTelemetry.RecordWebhookLag(TelnyxTelemetry.WebhookKindCall, receipt.Type, callEvent.OccurredUtc, clock.UtcNow);

            // Fast-path the voicemail greeting-ended signal: issue record_start immediately, before the durable
            // webhook inbox write (which can stall for seconds under SQLite write contention). The beep-and-record
            // is time-critical -- the caller is on the line waiting for the tone -- so it must not queue behind the
            // inbox. It needs no database work, and a duplicate on redelivery is harmless because Telnyx rejects a
            // second record_start on an already-recording call. The event carries no call-state transition, so it
            // does not need to continue into the durable inbox afterward.
            var greetingEndedEventType = callEvent.EventType?.Trim();

            if ((string.Equals(greetingEndedEventType, TelnyxConstants.Recording.SpeakEndedEventType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(greetingEndedEventType, TelnyxConstants.Recording.PlaybackEndedEventType, StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(callEvent.CallControlId) &&
                TelnyxRecordingClientState.TryParseGreeting(callEvent.ClientState, out var greetingState))
            {
                var recordingStarter = httpContext.RequestServices.GetService<ITelnyxVoicemailRecordingStarter>();

                if (recordingStarter is not null)
                {
                    await StartVoicemailRecordingAsync(recordingStarter, callEvent, greetingState, receipt, logger);
                }

                healthMetrics?.RecordWebhookProcessed(true);
                receipt.Result = TelnyxTelemetry.WebhookAccepted;

                return TypedResults.Ok(new
                {
                    accepted = true,
                });
            }

            ProviderWebhookIngressLease rateLease = null;

            try
            {
                if (ingressLimiter is not null)
                {
                    rateLease = await ingressLimiter.AcquireRateAsync(TelnyxConstants.ProviderTechnicalName, CancellationToken.None);

                    if (!rateLease.IsAcquired)
                    {
                        SetRetryAfter(httpContext, rateLease.RetryAfter);
                        receipt.Result = TelnyxTelemetry.WebhookRejectedRate;

                        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                    }
                }

                if (inbox is null)
                {
                    var result = await webhookService.ProcessAsync(callEvent, CancellationToken.None);

                    healthMetrics?.RecordWebhookProcessed(true);
                    receipt.Result = TelnyxTelemetry.WebhookAccepted;

                    return TypedResults.Ok(new
                    {
                        accepted = true,
                        result,
                    });
                }

                ProviderWebhookInboxAcceptanceResult acceptance;

                try
                {
                    acceptance = await inbox.AcceptAsync(new ProviderWebhookInboxDelivery
                    {
                        ProviderName = TelnyxConstants.ProviderTechnicalName,
                        DeliveryId = TelnyxWebhookDelivery.GetDeliveryId(callEvent),
                        HandlerName = TelnyxWebhookInboxHandler.HandlerTechnicalName,
                        Payload = System.Text.Json.JsonSerializer.Serialize(callEvent, TelnyxJsonSerializerOptions.Default),
                    }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Nothing was stored, so the delivery genuinely has to come again. Answering with a retryable
                    // status says so in the provider's own language; letting the exception escape would answer 500
                    // and, on a burst that is already contending for the store, add an error page to the load.
                    logger.LogWarning(ex, "Could not durably accept a Telnyx webhook delivery. Asking the provider to redeliver it.");
                    healthMetrics?.RecordWebhookProcessed(false);
                    receipt.Result = TelnyxTelemetry.WebhookStoreFailed;

                    return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Busy)
                {
                    receipt.Result = TelnyxTelemetry.WebhookRejectedRate;

                    return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                receipt.Result = acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate
                    ? TelnyxTelemetry.WebhookDuplicate
                    : TelnyxTelemetry.WebhookAccepted;

                // Past this point the delivery is durable and the background inbox owns finishing it, so the
                // provider must be told it landed no matter how the immediate attempt goes. Dispatching inline is
                // only a latency optimization: it saves waiting for the next sweep on a quiet system, and it is
                // exactly what must be given up on a busy one. Failing the request instead would ask the provider
                // to redeliver something already stored, and a provider that redelivers under contention makes
                // the contention worse -- which is how one call's teardown burst turned into a cascade of 503s.
                try
                {
                    await inbox.DispatchAsync(acceptance.MessageId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Could not process Telnyx webhook delivery '{MessageId}' while receiving it. It stays queued for the inbox worker.",
                        acceptance.MessageId.SanitizeLogValue());
                }

                healthMetrics?.RecordWebhookProcessed(true);

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

    /// <summary>
    /// When the change the delivery reports happened: the event's own <c>occurred_at</c>, which Telnyx reports to the
    /// millisecond, and only without one the whole-second time the delivery was signed at. The signed time is when
    /// the delivery was sent, which for a retried delivery is not when the call changed.
    /// </summary>
    internal static DateTime ResolveOccurredUtc(TelnyxCallEvent callEvent, DateTime signedUtc)
        => callEvent.OccurredUtc.HasValue
            ? DateTime.SpecifyKind(callEvent.OccurredUtc.Value, DateTimeKind.Utc)
            : signedUtc;

    // Starts the voicemail recording on the fast path, and measures how long after the greeting-ended delivery
    // arrived it was started: the caller is waiting for the tone the whole time.
    private static async Task StartVoicemailRecordingAsync(
        ITelnyxVoicemailRecordingStarter recordingStarter,
        TelnyxCallEvent callEvent,
        TelnyxRecordingClientState greetingState,
        WebhookReceipt receipt,
        ILogger logger)
    {
        var outcome = CrestAppsOrchardCoreTelemetry.OutcomeOk;

        try
        {
            await recordingStarter.StartAsync(
                callEvent.CallControlId,
                greetingState.InteractionId,
                greetingState.RecipientUserId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(ex);
            TelnyxLog.VoicemailRecordStartFailed(logger, ex, callEvent.CallControlId.SanitizeLogValue());

            throw;
        }
        finally
        {
            if (TelnyxTelemetry.VoicemailRecordStartLatency.Enabled)
            {
                var tags = new TagList
                {
                    { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
                };

                CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
                TelnyxTelemetry.VoicemailRecordStartLatency.Record(Stopwatch.GetElapsedTime(receipt.StartedTimestamp).TotalSeconds, tags);
            }
        }
    }

    /// <summary>
    /// The time the delivery was signed at, which the replay window is judged by.
    /// </summary>
    private static DateTime ResolveSignedUtc(string timestamp, TelnyxCallEvent callEvent, DateTime nowUtc)
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

    /// <summary>
    /// What became of one delivery, filled in as it is handled and recorded once when it is answered.
    /// </summary>
    private sealed class WebhookReceipt
    {
        public long StartedTimestamp { get; } = Stopwatch.GetTimestamp();

        public string Type { get; set; }

        public string Result { get; set; }
    }
}
