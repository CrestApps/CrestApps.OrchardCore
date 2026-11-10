using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Keeps direct webhook deliveries in the same outbox as subscriptions' deliveries, so they are retried and logged the
/// same way.
/// </summary>
public sealed class WebhookOutbox : IWebhookOutbox
{
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookOutbox"/> class.
    /// </summary>
    /// <param name="deliveries">The delivery store.</param>
    /// <param name="clock">The clock.</param>
    public WebhookOutbox(IWebhookDeliveryStore deliveries, IClock clock)
    {
        _deliveries = deliveries;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(WebhookDirectDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrEmpty(delivery.Url);
        ArgumentException.ThrowIfNullOrEmpty(delivery.FormatterName);

        var now = _clock.UtcNow;
        var record = new WebhookDelivery
        {
            ItemId = IdGenerator.GenerateId(),
            SubscriptionId = delivery.OwnerId,
            Url = delivery.Url,
            FormatterName = delivery.FormatterName,
            FormatterData = delivery.Data is null ? [] : new Dictionary<string, string>(delivery.Data),
            EventId = string.IsNullOrEmpty(delivery.EventId) ? "evt_" + IdGenerator.GenerateId() : delivery.EventId,
            EventName = delivery.EventName,
            Payload = delivery.Body ?? "{}",
            Status = WebhookDeliveryStatus.Pending,

            // Sent right after the request; the retry task only picks it up if that send didn't finish.
            NextAttemptUtc = now + WebhookDispatcher.Backoff[0],
            CreatedUtc = now,
        };

        await _deliveries.CreateAsync(record, cancellationToken);

        var id = record.ItemId;

        // Sent after the work that raised it is saved, in a scope of its own, so a slow receiver never holds the
        // request up.
        ShellScope.AddDeferredTask(async scope =>
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<WebhookDispatcher>();
            var store = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryStore>();

            if (await store.FindByIdAsync(id) is { } queued)
            {
                await dispatcher.SendAsync(queued);
            }
        });
    }
}
