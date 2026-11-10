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
    private readonly IEnumerable<IWebhookDeliveryFormatter> _formatters;
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
        IEnumerable<IWebhookDeliveryFormatter> formatters,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<WebhookDispatcher> logger)
    {
        _sender = sender;
        _deliveries = deliveries;
        _subscriptions = subscriptions;
        _formatters = formatters;
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

        if (delivery.IsDirect)
        {
            await SendDirectAsync(delivery, now, cancellationToken);
            await _deliveries.UpdateAsync(delivery, cancellationToken);

            return;
        }

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
            var result = await _sender.SendAsync(new WebhookSendRequest
            {
                Url = subscription.Url,
                Secret = secret,
                EventId = delivery.EventId,
                EventName = delivery.EventName,
                Body = delivery.Payload ?? "{}",
            }, cancellationToken);

            Record(delivery, result, now);
            RecordAttempt(delivery, result);
        }

        await _deliveries.UpdateAsync(delivery, cancellationToken);
    }

    // Each attempt is counted once, by what it came to; one that failed for good is also logged.
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

        WebhookTelemetry.RecordDelivery(WebhookTelemetry.DeliveryRetry, reason, result.StatusCode);
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
    private async Task SendDirectAsync(WebhookDelivery delivery, DateTime now, CancellationToken cancellationToken)
    {
        delivery.Attempts++;
        delivery.LastAttemptUtc = now;

        var formatter = _formatters.FirstOrDefault(entry => string.Equals(entry.Name, delivery.FormatterName, StringComparison.Ordinal));

        if (formatter is null)
        {
            // The feature that queued it is off; nothing knows how to sign it.
            Fail(delivery, statusCode: null, $"Nothing can send '{delivery.FormatterName}' webhooks now; the feature that sends them is off.");

            return;
        }

        var formatted = await formatter.FormatAsync(new WebhookFormatContext
        {
            Url = delivery.Url,
            EventId = delivery.EventId,
            EventName = delivery.EventName,
            Body = delivery.Payload ?? "{}",
            Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
            Data = delivery.FormatterData ?? [],
        }, cancellationToken);

        if (formatted.Error is not null)
        {
            Fail(delivery, statusCode: null, formatted.Error);

            return;
        }

        var result = await _sender.SendAsync(new WebhookSendRequest
        {
            Url = delivery.Url,
            EventId = delivery.EventId,
            EventName = delivery.EventName,
            Body = delivery.Payload ?? "{}",
            Headers = formatted.Headers,
            Sign = false,
        }, cancellationToken);

        Record(delivery, result, now);
    }

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
