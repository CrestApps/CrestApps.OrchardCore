using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// Queues an event for every enabled webhook subscription that receives it. Sending happens in the background, so a
/// slow endpoint never holds up a conversation.
/// </summary>
public sealed class AIWebhookPublisher
{
    /// <summary>
    /// The data protector purpose of subscription secrets.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Webhooks";

    /// <summary>
    /// The longest payload kept, in characters.
    /// </summary>
    public const int MaxPayloadLength = 64_000;

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAIWebhookSubscriptionStore _subscriptionStore;
    private readonly IAIWebhookDeliveryStore _deliveryStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookPublisher"/> class.
    /// </summary>
    /// <param name="subscriptionStore">The subscription store.</param>
    /// <param name="deliveryStore">The delivery store.</param>
    /// <param name="clock">The clock.</param>
    public AIWebhookPublisher(IAIWebhookSubscriptionStore subscriptionStore, IAIWebhookDeliveryStore deliveryStore, IClock clock)
    {
        _subscriptionStore = subscriptionStore;
        _deliveryStore = deliveryStore;
        _clock = clock;
    }

    /// <summary>
    /// Queues an event when the webhooks feature is on; does nothing otherwise. Never throws.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="eventName">The event, from <see cref="AIWebhookEvents"/>.</param>
    /// <param name="data">What happened.</param>
    /// <returns>A task that completes when the event is queued.</returns>
    public static async Task TryPublishAsync(IServiceProvider services, string eventName, object data)
    {
        var publisher = services?.GetService<AIWebhookPublisher>();

        if (publisher is null)
        {
            return;
        }

        try
        {
            await publisher.PublishAsync(eventName, data);
        }
        catch (Exception ex)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger<AIWebhookPublisher>()
                .LogWarning(ex, "The '{EventName}' webhook event could not be queued.", eventName.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Queues an event for every enabled subscription that receives it.
    /// </summary>
    /// <param name="eventName">The event.</param>
    /// <param name="data">What happened.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deliveries queued.</returns>
    public async Task<IReadOnlyList<AIWebhookDelivery>> PublishAsync(string eventName, object data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        var deliveries = new List<AIWebhookDelivery>();

        foreach (var subscription in await _subscriptionStore.ListEnabledAsync(cancellationToken))
        {
            if (subscription.Events?.Contains(eventName, StringComparer.Ordinal) == true)
            {
                deliveries.Add(await QueueAsync(subscription, eventName, data, cancellationToken));
            }
        }

        return deliveries;
    }

    /// <summary>
    /// Queues an event for one subscription, whatever it receives, such as a test event.
    /// </summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="data">What happened.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The delivery queued.</returns>
    public async Task<AIWebhookDelivery> QueueAsync(AIWebhookSubscription subscription, string eventName, object data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var id = UniqueId.GenerateId();
        var now = _clock.UtcNow;
        var payload = JsonSerializer.Serialize(new { id, @event = eventName, createdUtc = now, data }, _serializerOptions);

        var delivery = new AIWebhookDelivery
        {
            ItemId = id,
            SubscriptionId = subscription.ItemId,
            EventName = eventName,
            Payload = payload.Length > MaxPayloadLength ? payload[..MaxPayloadLength] : payload,
            Status = AIWebhookDeliveryStatus.Pending,
            CreatedUtc = now,
            NextAttemptUtc = now,
        };

        await _deliveryStore.CreateAsync(delivery, cancellationToken);

        return delivery;
    }
}

/// <summary>
/// Sends a queued webhook delivery, signed like the commerce webhooks, and schedules the next attempt when it fails.
/// </summary>
public sealed class AIWebhookSender
{
    /// <summary>
    /// The named HTTP client deliveries are sent with.
    /// </summary>
    public const string HttpClientName = "CrestApps.AIWebhooks";

    /// <summary>
    /// How long after each failed attempt the next one is made. A delivery fails for good after the last.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="dataProtectionProvider">Decrypts the subscription's secret.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIWebhookSender(IHttpClientFactory httpClientFactory, IDataProtectionProvider dataProtectionProvider, IClock clock, ILogger<AIWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns the signature of a body at a time: <c>v1=</c> and the lowercase hex HMAC-SHA256 of
    /// <c>{timestamp}.{body}</c> keyed with the secret.
    /// </summary>
    /// <param name="secret">The subscription's secret.</param>
    /// <param name="timestamp">The Unix time in seconds, sent as <c>X-Webhook-Timestamp</c>.</param>
    /// <param name="body">The body.</param>
    /// <returns>The <c>X-Webhook-Signature</c> header value.</returns>
    public static string Sign(string secret, long timestamp, string body)
        => "v1=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret ?? string.Empty),
            Encoding.UTF8.GetBytes($"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}")));

    /// <summary>
    /// Whether a failed attempt is worth repeating: timeouts, throttling and server errors are; a refusal is not.
    /// </summary>
    /// <param name="statusCode">The status the endpoint answered.</param>
    /// <returns><see langword="true"/> when the delivery is tried again.</returns>
    public static bool IsWorthRetrying(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    /// <summary>
    /// Makes one attempt and records it on the delivery.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="subscription">Its subscription, or <see langword="null"/> when it was deleted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the attempt is recorded.</returns>
    public async Task SendAsync(AIWebhookDelivery delivery, AIWebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var now = _clock.UtcNow;
        delivery.Attempts++;

        if (subscription is null || string.IsNullOrWhiteSpace(subscription.Url))
        {
            Fail(delivery, null, "The subscription no longer exists.", now, retry: false);

            return;
        }

        string secret;

        try
        {
            secret = string.IsNullOrEmpty(subscription.ProtectedSecret)
                ? string.Empty
                : _dataProtectionProvider.CreateProtector(AIWebhookPublisher.ProtectorPurpose).Unprotect(subscription.ProtectedSecret);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "The secret of webhook subscription '{SubscriptionId}' can't be read; it needs to be entered again.", subscription.ItemId.SanitizeLogValue());
            Fail(delivery, null, "The signing secret can't be read; enter it again on the subscription.", now, retry: false);

            return;
        }

        try
        {
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(delivery.Payload ?? "{}", Encoding.UTF8, "application/json"),
            };

            request.Headers.Add("X-Webhook-Id", delivery.ItemId);
            request.Headers.Add("X-Webhook-Event", delivery.EventName);
            request.Headers.Add("X-Webhook-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add("X-Webhook-Signature", Sign(secret, timestamp, delivery.Payload ?? "{}"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CrestApps-Webhooks", "1.0"));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            delivery.StatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = AIWebhookDeliveryStatus.Delivered;
                delivery.Error = null;
                delivery.CompletedUtc = now;

                return;
            }

            Fail(delivery, (int)response.StatusCode, $"The endpoint answered {(int)response.StatusCode}.", now, IsWorthRetrying(response.StatusCode));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            Fail(delivery, null, ex is TaskCanceledException ? "The endpoint did not answer within 15 seconds." : ex.Message, now, retry: true);
        }
    }

    private static void Fail(AIWebhookDelivery delivery, int? statusCode, string error, DateTime now, bool retry)
    {
        delivery.StatusCode = statusCode;
        delivery.Error = error;

        if (retry && delivery.Attempts <= RetryDelays.Count)
        {
            delivery.Status = AIWebhookDeliveryStatus.Pending;
            delivery.NextAttemptUtc = now + RetryDelays[delivery.Attempts - 1];

            return;
        }

        delivery.Status = AIWebhookDeliveryStatus.Failed;
        delivery.CompletedUtc = now;
    }
}
