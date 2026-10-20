using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Services;

/// <summary>
/// Posts deliveries to their subscriptions' URLs, signed, and keeps what each attempt came to: delivered on a 2xx
/// answer, otherwise tried again later, less and less often, until it gives up.
/// </summary>
public sealed class WebhookDispatcher
{
    /// <summary>
    /// How long after each failed attempt the next one is made. After the last, the delivery fails.
    /// </summary>
    internal static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
    ];

    private const int MaxErrorLength = 500;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IDataProtector _protector;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public WebhookDispatcher(
        IHttpClientFactory httpClientFactory,
        IWebhookDeliveryStore deliveries,
        IWebhookSubscriptionStore subscriptions,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<WebhookDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _deliveries = deliveries;
        _subscriptions = subscriptions;
        _protector = dataProtectionProvider.CreateProtector(WebhookConstants.SecretProtectionPurpose);
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns a new secret for a subscription, protected for storage.
    /// </summary>
    public string NewProtectedSecret()
        => _protector.Protect("whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    /// <summary>
    /// Returns a subscription's secret, to show the merchant and to sign with; <see langword="null"/> when it can't be
    /// read, such as after the site's data protection keys were lost.
    /// </summary>
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
    /// Returns the signature of a body at a time: <c>v1=</c> and the lowercase hex HMAC-SHA256 of
    /// <c>{timestamp}.{body}</c>.
    /// </summary>
    public static string Sign(string secret, long timestamp, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + body));

        return "v1=" + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns when a delivery that failed for the time given is tried next, or <see langword="null"/> when it gives up.
    /// </summary>
    public static TimeSpan? GetRetryDelay(int attempts)
        => attempts >= 1 && attempts <= Backoff.Length ? Backoff[attempts - 1] : null;

    /// <summary>
    /// Makes one attempt at a delivery, and saves what it came to.
    /// </summary>
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
        }
        else if (!subscription.IsEnabled)
        {
            Fail(delivery, statusCode: null, "The webhook was turned off.");
        }
        else if (secret is null)
        {
            // Unsigned, a receiver can't trust it; signed with the wrong key, it would refuse it anyway.
            Fail(delivery, statusCode: null, "The webhook's signing secret can't be read. Give it a new secret, then send it again.");
        }
        else
        {
            try
            {
                var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
                {
                    Content = new StringContent(delivery.Payload ?? "{}", Encoding.UTF8, "application/json"),
                };

                request.Headers.Add(WebhookConstants.Headers.Id, delivery.EventId);
                request.Headers.Add(WebhookConstants.Headers.Event, delivery.EventName);
                request.Headers.Add(WebhookConstants.Headers.Timestamp, timestamp.ToString(CultureInfo.InvariantCulture));
                request.Headers.Add(WebhookConstants.Headers.Signature, Sign(secret, timestamp, delivery.Payload ?? "{}"));
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CrestApps-Commerce-Webhooks", "1.0"));

                using var response = await _httpClientFactory.CreateClient(WebhookConstants.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                delivery.LastStatusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    delivery.Status = WebhookDeliveryStatus.Delivered;
                    delivery.DeliveredUtc = now;
                    delivery.NextAttemptUtc = null;
                    delivery.LastError = null;
                }
                else
                {
                    var body = await ReadStartAsync(response, cancellationToken);

                    Retry(delivery, (int)response.StatusCode, $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}", now);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
            {
                _logger.LogWarning(exception, "A webhook delivery '{DeliveryId}' to subscription '{SubscriptionId}' failed.", delivery.ItemId, delivery.SubscriptionId);

                Retry(delivery, statusCode: null, exception.Message, now);
            }
        }

        await _deliveries.UpdateAsync(delivery, cancellationToken);
    }

    /// <summary>
    /// Sends a delivery again from the start, as the merchant asked.
    /// </summary>
    public async Task ResendAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptUtc = _clock.UtcNow;

        await SendAsync(delivery, cancellationToken);
    }

    // Only the start of a refusal is kept, and a receiver slow to send it isn't waited on.
    private static async Task<string> ReadStartAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new char[MaxErrorLength];
            using var reader = new StreamReader(stream);
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), timeout.Token);

            return new string(buffer, 0, read);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or HttpRequestException)
        {
            return string.Empty;
        }
    }

    private static void Retry(WebhookDelivery delivery, int? statusCode, string error, DateTime now)
    {
        if (GetRetryDelay(delivery.Attempts) is { } delay)
        {
            delivery.NextAttemptUtc = now + delay;
            delivery.LastStatusCode = statusCode;
            delivery.LastError = Truncate(error);

            return;
        }

        Fail(delivery, statusCode, error);
    }

    private static void Fail(WebhookDelivery delivery, int? statusCode, string error)
    {
        delivery.Status = WebhookDeliveryStatus.Failed;
        delivery.NextAttemptUtc = null;
        delivery.LastStatusCode = statusCode;
        delivery.LastError = Truncate(error);
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) || value.Length <= MaxErrorLength ? value : value[..MaxErrorLength] + "…";
}
