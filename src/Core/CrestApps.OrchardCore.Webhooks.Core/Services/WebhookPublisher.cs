using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Queues an event for every subscription that takes it, and sends the deliveries once the request's work is saved;
/// what isn't delivered then is retried from the outbox by the deliveries background task.
/// </summary>
public sealed class WebhookPublisher : IWebhookPublisher
{
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly IWebhookEventCatalog _events;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    private IReadOnlyList<WebhookSubscription> _enabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPublisher"/> class.
    /// </summary>
    /// <param name="subscriptions">The subscription store.</param>
    /// <param name="deliveries">The delivery store.</param>
    /// <param name="events">The declared events; an undeclared event is not sent.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public WebhookPublisher(
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        IWebhookEventCatalog events,
        IClock clock,
        ILogger<WebhookPublisher> logger)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default)
        => PublishAsync(eventName, payload, eventId: null, cancellationToken);

    /// <inheritdoc/>
    public async Task PublishAsync(string eventName, object payload, string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        try
        {
            // Only events an enabled module declares are sent, so a subscription to every event never receives one from
            // a feature that is off.
            if (_events.Find(eventName) is null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("The webhook event '{EventName}' is not declared by an enabled feature, so it is not sent.", eventName.SanitizeLogValue());
                }

                return;
            }

            // Read once per request: one checkout can raise several events.
            _enabled ??= await _subscriptions.ListEnabledAsync(cancellationToken);

            var subscribers = _enabled.Where(subscription => subscription.Takes(eventName)).ToArray();

            if (subscribers.Length == 0)
            {
                return;
            }

            var now = _clock.UtcNow;
            eventId = string.IsNullOrEmpty(eventId) ? "evt_" + IdGenerator.GenerateId() : eventId;
            var body = JsonSerializer.Serialize(new { id = eventId, @event = eventName, createdUtc = now, data = payload }, JsonSerializerOptions.Web);
            var ids = new List<string>();

            foreach (var subscription in subscribers)
            {
                var delivery = new WebhookDelivery
                {
                    ItemId = IdGenerator.GenerateId(),
                    SubscriptionId = subscription.ItemId,
                    EventId = eventId,
                    EventName = eventName,
                    Payload = body,
                    Status = WebhookDeliveryStatus.Pending,

                    // Sent right after the request; the retry task only picks it up if that send didn't finish.
                    NextAttemptUtc = now + WebhookDispatcher.Backoff[0],
                    CreatedUtc = now,
                };

                await _deliveries.CreateAsync(delivery, cancellationToken);
                ids.Add(delivery.ItemId);
            }

            // Sent after the work that raised the event is saved, in a scope of its own, so a receiver that's slow or
            // down never holds up or fails the request.
            ShellScope.AddDeferredTask(async scope =>
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<WebhookDispatcher>();
                var store = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryStore>();

                foreach (var id in ids)
                {
                    if (await store.FindByIdAsync(id) is { } delivery)
                    {
                        await dispatcher.SendAsync(delivery);
                    }
                }
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A webhook is a notification: failing to queue one must never fail the work that raised it.
            _logger.LogError(exception, "The webhook event '{EventName}' couldn't be queued.", eventName.SanitizeLogValue());
        }
    }
}
