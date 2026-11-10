using System.Security.Cryptography;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Telemetry;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// The delivery outbox's sender: posts deliveries to their subscriptions' URLs, signed, and keeps what each attempt came
/// to. A 2xx answer delivers it; any other answer or error is tried again later, less and less often, until it gives
/// up. A host the egress guard refuses fails at once, because it is refused again on every attempt.
/// </summary>
public sealed class WebhookDispatcher
{
    /// <summary>
    /// How long after each failed attempt the next one is made. After the last, the delivery fails.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
    ];

    private readonly IWebhookSender _sender;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IDataProtector _protector;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDispatcher"/> class.
    /// </summary>
    /// <param name="sender">Makes each signed attempt.</param>
    /// <param name="deliveries">The delivery store.</param>
    /// <param name="subscriptions">The subscription store.</param>
    /// <param name="dataProtectionProvider">Protects subscription secrets.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public WebhookDispatcher(
        IWebhookSender sender,
        IWebhookDeliveryStore deliveries,
        IWebhookSubscriptionStore subscriptions,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<WebhookDispatcher> logger)
    {
        _sender = sender;
        _deliveries = deliveries;
        _subscriptions = subscriptions;
        _protector = dataProtectionProvider.CreateProtector(WebhookStorage.SecretProtectionPurpose);
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns a new secret for a subscription, protected for storage.
    /// </summary>
    /// <returns>The protected secret.</returns>
    public string NewProtectedSecret()
        => ProtectSecret("whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    /// <summary>
    /// Protects a secret for storage, such as one carried by a recipe.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The protected secret.</returns>
    public string ProtectSecret(string secret)
        => _protector.Protect(secret);

    /// <summary>
    /// Returns a subscription's secret, to show the administrator and to sign with; <see langword="null"/> when it can't
    /// be read, such as after the site's data protection keys were lost.
    /// </summary>
    /// <param name="subscription">The subscription.</param>
    /// <returns>The secret, or <see langword="null"/>.</returns>
    public string GetSecret(WebhookSubscription subscription)
    {
        if (string.IsNullOrEmpty(subscription?.ProtectedSecret))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(subscription.ProtectedSecret);
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(exception, "The signing secret of webhook '{SubscriptionId}' can't be read; it needs a new one.", subscription.ItemId);

            return null;
        }
    }

    /// <summary>
    /// Returns when a delivery that failed for the time given is tried next, or <see langword="null"/> when it gives up.
    /// </summary>
    /// <param name="attempts">How many attempts were made.</param>
    /// <returns>The delay before the next attempt, or <see langword="null"/>.</returns>
    public static TimeSpan? GetRetryDelay(int attempts)
        => attempts >= 1 && attempts <= Backoff.Count ? Backoff[attempts - 1] : null;

    /// <summary>
    /// Makes one attempt at a pending delivery, and records what it came to on the delivery.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the attempt is recorded.</returns>
    public async Task SendAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (delivery.Status != WebhookDeliveryStatus.Pending)
        {
            return;
        }

        var now = _clock.UtcNow;
        var subscription = await _subscriptions.FindByIdAsync(delivery.SubscriptionId, cancellationToken);

        delivery.Attempts++;
        delivery.LastAttemptUtc = now;

        var secret = subscription is null ? null : GetSecret(subscription);

        if (subscription is null)
        {
            // The webhook was deleted; nothing is sent to where it pointed.
            Fail(delivery, statusCode: null, "The webhook was deleted.");
            RecordRefused("deleted");
        }
        else if (!subscription.IsEnabled)
        {
            Fail(delivery, statusCode: null, "The webhook was turned off.");
            RecordRefused("disabled");
        }
        else if (secret is null)
        {
            // Unsigned, a receiver can't trust it; signed with the wrong key, it would refuse it anyway.
            Fail(delivery, statusCode: null, "The webhook's signing secret can't be read. Give it a new secret, then send it again.");
            RecordRefused("no_secret");
        }
        else
        {
            using var telemetry = ExternalCallTelemetry.Start(
                WebhookTelemetry.ActivitySource,
                "crestapps.webhook.delivery.send",
                WebhookTelemetry.DeliveryDuration,
                WebhookTelemetry.OutboundProvider,
                "deliver");

            telemetry.SetSpanTag("crestapps.webhook.delivery.id", delivery.ItemId);
            telemetry.SetSpanTag("crestapps.webhook.event.name", delivery.EventName);
            telemetry.SetSpanTag("crestapps.webhook.delivery.attempt", delivery.Attempts);

            var result = await _sender.SendAsync(new WebhookSendRequest
            {
                Url = subscription.Url,
                Secret = secret,
                EventId = delivery.EventId,
                EventName = delivery.EventName,
                Body = delivery.Payload ?? "{}",
            }, cancellationToken);

            telemetry.SetStatusCode(result.StatusCode);

            if (!result.Succeeded)
            {
                telemetry.Fail(GetFailureReason(result));
            }

            Record(delivery, result, now);
            RecordAttempt(delivery, result);
        }

        await _deliveries.UpdateAsync(delivery, cancellationToken);
    }

    // Each attempt is counted once, by what it came to, and an attempt that will be made again is a retry of the
    // dispatcher's own back-off loop, logged with its number so a storm reads the same on a chart and in the logs.
    private void RecordAttempt(WebhookDelivery delivery, WebhookSendResult result)
    {
        if (result.Succeeded)
        {
            WebhookTelemetry.RecordDelivery(WebhookTelemetry.DeliveryOk, "none", result.StatusCode);

            return;
        }

        var reason = GetFailureReason(result);

        if (delivery.Status == WebhookDeliveryStatus.Failed)
        {
            var finalReason = result.EgressRefused ? "egress_refused" : "exhausted";

            WebhookTelemetry.RecordDelivery(WebhookTelemetry.DeliveryError, finalReason, result.StatusCode);
            WebhookLog.DeliveryFailed(_logger, delivery.ItemId, delivery.EventName, delivery.Attempts, finalReason);

            return;
        }

        var deliveryId = delivery.ItemId;
        var retryDelay = GetRetryDelay(delivery.Attempts);

        WebhookTelemetry.RecordDelivery(WebhookTelemetry.DeliveryRetry, reason, result.StatusCode);
        RetryTelemetry.RecordRetry("webhooks", reason, delivery.Attempts + 1);
        WebhookLog.DeliveryRetryScheduled(_logger, deliveryId, delivery.EventName, delivery.Attempts, result.StatusCode, retryDelay);
    }

    private static string GetFailureReason(WebhookSendResult result)
        => result.EgressRefused
            ? "egress_refused"
            : result.StatusCode is null
                ? "network"
                : "status_" + ExternalCallTelemetry.GetStatusClass(result.StatusCode);

    private static void RecordRefused(string reason)
        => WebhookTelemetry.RecordDelivery(WebhookTelemetry.DeliveryError, reason, statusCode: null);

    /// <summary>
    /// Sends a delivery again from the start, as the administrator asked.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the attempt is recorded.</returns>
    public async Task ResendAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptUtc = _clock.UtcNow;

        await SendAsync(delivery, cancellationToken);
    }

    /// <summary>
    /// Records what an attempt came to on a delivery: delivered, failed for good, or due again after its backoff.
    /// </summary>
    /// <param name="delivery">The delivery, with its attempt already counted.</param>
    /// <param name="result">What the attempt came to.</param>
    /// <param name="now">When the attempt was made.</param>
    public static void Record(WebhookDelivery delivery, WebhookSendResult result, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            delivery.Status = WebhookDeliveryStatus.Delivered;
            delivery.DeliveredUtc = now;
            delivery.NextAttemptUtc = null;
            delivery.LastStatusCode = result.StatusCode;
            delivery.LastError = null;

            return;
        }

        if (result.EgressRefused)
        {
            Fail(delivery, result.StatusCode, result.Error);

            return;
        }

        if (GetRetryDelay(delivery.Attempts) is { } delay)
        {
            delivery.NextAttemptUtc = now + delay;
            delivery.LastStatusCode = result.StatusCode;
            delivery.LastError = result.Error;

            return;
        }

        Fail(delivery, result.StatusCode, result.Error);
    }

    private static void Fail(WebhookDelivery delivery, int? statusCode, string error)
    {
        delivery.Status = WebhookDeliveryStatus.Failed;
        delivery.NextAttemptUtc = null;
        delivery.LastStatusCode = statusCode;
        delivery.LastError = error;
    }
}
