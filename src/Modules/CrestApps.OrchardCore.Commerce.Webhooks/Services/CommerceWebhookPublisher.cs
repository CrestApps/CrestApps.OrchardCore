using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Services;

/// <summary>
/// Queues an event for every subscription that takes it, and sends them once the request's work is saved; what isn't
/// delivered then is retried by the deliveries background task.
/// </summary>
public sealed class CommerceWebhookPublisher : ICommerceWebhookPublisher
{
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    private IReadOnlyList<WebhookSubscription> _enabled;

    public CommerceWebhookPublisher(
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        IClock clock,
        ILogger<CommerceWebhookPublisher> logger)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _clock = clock;
        _logger = logger;
    }

    public Task PublishAsync(string eventName, object data, CancellationToken cancellationToken = default)
        => PublishAsync(eventName, data, eventId: null, cancellationToken);

    public async Task PublishAsync(string eventName, object data, string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        try
        {
            // Read once per request: one checkout can raise several events.
            _enabled ??= await _subscriptions.ListEnabledAsync(cancellationToken);

            var subscribers = _enabled.Where(subscription => subscription.Takes(eventName)).ToArray();

            if (subscribers.Length == 0)
            {
                return;
            }

            var now = _clock.UtcNow;
            eventId = string.IsNullOrEmpty(eventId) ? "evt_" + IdGenerator.GenerateId() : eventId;
            var payload = JsonSerializer.Serialize(new { id = eventId, @event = eventName, createdUtc = now, data }, JsonSerializerOptions.Web);
            var ids = new List<string>();

            foreach (var subscription in subscribers)
            {
                var delivery = new WebhookDelivery
                {
                    ItemId = IdGenerator.GenerateId(),
                    SubscriptionId = subscription.ItemId,
                    EventId = eventId,
                    EventName = eventName,
                    Payload = payload,
                    Status = WebhookDeliveryStatus.Pending,

                    // Sent right after the request; the retry task only picks it up if that send didn't finish.
                    NextAttemptUtc = now + WebhookDispatcher.Backoff[0],
                    CreatedUtc = now,
                };

                await _deliveries.CreateAsync(delivery, cancellationToken);
                ids.Add(delivery.ItemId);
            }

            // Sent after the work that raised the event is saved, in a scope of its own, so a receiver that's slow or
            // down never holds up or fails the buyer's request.
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
            // A webhook is a notification: failing to queue one must never fail the order that raised it.
            _logger.LogError(exception, "The webhook event '{EventName}' couldn't be queued.", eventName);
        }
    }
}
