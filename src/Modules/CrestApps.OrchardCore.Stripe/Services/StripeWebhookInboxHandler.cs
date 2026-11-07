using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Indexes;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules;
using Stripe;
using YesSql;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Processes a verified Stripe event from the provider webhook inbox: the endpoint stores the event and answers Stripe
/// at once, and the event's payment handlers run from storage, retried until they succeed.
/// </summary>
public sealed class StripeWebhookInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable name Stripe events are stored under in the inbox.
    /// </summary>
    public const string HandlerTechnicalName = "Stripe.Event";

    /// <summary>
    /// The provider name Stripe deliveries are keyed by in the inbox.
    /// </summary>
    public const string ProviderName = "Stripe";

    private readonly IEnumerable<IPaymentEvent> _paymentEvents;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="paymentEvents">The payment event handlers a Stripe event is dispatched to.</param>
    /// <param name="session">The YesSql session the processed-event marker is saved with.</param>
    /// <param name="clock">The clock the marker is stamped with.</param>
    public StripeWebhookInboxHandler(
        IEnumerable<IPaymentEvent> paymentEvents,
        ISession session,
        IClock clock)
    {
        _paymentEvents = paymentEvents;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string TechnicalName => HandlerTechnicalName;

    /// <inheritdoc/>
    public WebhookHandlerReplaySafety ReplaySafety => WebhookHandlerReplaySafety.DeduplicatedByEventId;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        // The signature was checked when the event was accepted; the stored payload is parsed again here.
        var stripeEvent = EventUtility.ParseEvent(payload, throwOnApiVersionMismatch: false);

        // A retried dispatch whose handlers already ran, or an event processed before the inbox was used, is skipped.
        var alreadyProcessed = await _session
            .Query<ProcessedStripeWebhookEvent, ProcessedStripeWebhookEventIndex>(index => index.EventId == stripeEvent.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (alreadyProcessed is not null)
        {
            return;
        }

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, _paymentEvents, cancellationToken);

        // Saved in the same scope as the handlers' writes, so both commit together.
        await _session.SaveAsync(new ProcessedStripeWebhookEvent
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ProcessedUtc = _clock.UtcNow,
        }, cancellationToken: cancellationToken);
    }
}
