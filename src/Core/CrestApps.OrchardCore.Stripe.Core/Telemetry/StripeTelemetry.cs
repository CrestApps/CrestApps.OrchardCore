using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Stripe.Core.Telemetry;

/// <summary>
/// The meter and activity source of the Stripe integration, <c>CrestApps.OrchardCore.Stripe</c>: the webhook events
/// Stripe delivers and the calls made to Stripe's API. Event ids go on spans only; customer, payment and invoice ids,
/// amounts and card data go nowhere.
/// </summary>
public static class StripeTelemetry
{
    /// <summary>
    /// The name of the Stripe meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Stripe";

    /// <summary>
    /// The tag that records the type of a Stripe event, such as <c>invoice.payment_succeeded</c>. A type the webhook
    /// does not handle is <c>other</c>, so the tag stays bounded.
    /// </summary>
    public const string EventTypeTag = "event.type";

    /// <summary>
    /// The span tag that carries the id of a Stripe event. It is never a metric tag.
    /// </summary>
    public const string EventIdTag = "crestapps.stripe.event.id";

    /// <summary>
    /// The event was processed, or accepted for processing.
    /// </summary>
    public const string WebhookOk = CrestAppsOrchardCoreTelemetry.OutcomeOk;

    /// <summary>
    /// The event was stored in the provider webhook inbox, to be processed right after the response.
    /// </summary>
    public const string WebhookQueued = "queued";

    /// <summary>
    /// The event was processed before, so it was acknowledged without running its handlers again.
    /// </summary>
    public const string WebhookDuplicate = "duplicate";

    /// <summary>
    /// Another delivery of the same event was being processed, so Stripe was asked to retry.
    /// </summary>
    public const string WebhookBusy = "busy";

    /// <summary>
    /// The delivery had no signature, or its signature did not verify.
    /// </summary>
    public const string WebhookInvalidSignature = "invalid_signature";

    /// <summary>
    /// The site has no webhook secret, so no delivery can be verified.
    /// </summary>
    public const string WebhookNotConfigured = "not_configured";

    /// <summary>
    /// The event's handlers failed, so Stripe was asked to retry.
    /// </summary>
    public const string WebhookError = CrestAppsOrchardCoreTelemetry.OutcomeError;

    /// <summary>
    /// The event could not be stored in the provider webhook inbox, so Stripe was asked to retry.
    /// </summary>
    public const string WebhookInboxError = "inbox_error";

    /// <summary>
    /// Stripe declined the card, answering 402.
    /// </summary>
    public const string ApiCardDeclined = "card_declined";

    /// <summary>
    /// Stripe limited the rate of requests, answering 429.
    /// </summary>
    public const string ApiRateLimited = "rate_limited";

    /// <summary>
    /// Stripe refused the API key, answering 401 or 403.
    /// </summary>
    public const string ApiAuthenticationError = "authentication_error";

    /// <summary>
    /// Stripe refused the request as invalid, answering another 4xx status.
    /// </summary>
    public const string ApiInvalidRequest = "invalid_request";

    /// <summary>
    /// Stripe failed, answering a 5xx status.
    /// </summary>
    public const string ApiError = "api_error";

    /// <summary>
    /// Stripe could not be reached.
    /// </summary>
    public const string ApiConnectivityError = "connectivity_error";

    // The API resources the integration calls, by the first segment of their path. Anything else is "other", so a
    // resource id or an unexpected path never becomes a tag value.
    private static readonly HashSet<string> _apiResources = new(StringComparer.Ordinal)
    {
        "account_links",
        "accounts",
        "apple_pay",
        "balance",
        "billing_portal",
        "charges",
        "checkout",
        "customers",
        "disputes",
        "ephemeral_keys",
        "events",
        "invoices",
        "payment_intents",
        "payment_method_domains",
        "payment_methods",
        "payouts",
        "plans",
        "prices",
        "products",
        "refunds",
        "setup_intents",
        "subscription_items",
        "subscription_schedules",
        "subscriptions",
        "terminal",
        "tokens",
        "transfers",
        "webhook_endpoints",
    };

    /// <summary>
    /// The meter of the Stripe integration.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Stripe");

    /// <summary>
    /// The activity source of the Stripe integration.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Stripe");

    /// <summary>
    /// The number of webhook deliveries Stripe made, tagged with the <see cref="EventTypeTag"/> and the <c>outcome</c>
    /// of receiving it, such as <see cref="WebhookOk"/> or <see cref="WebhookInvalidSignature"/>. Each delivery counts
    /// once, when it is received; an inbox retry is not a delivery.
    /// </summary>
    public static Counter<long> WebhookEvents { get; } = Meter.CreateCounter<long>(
        "crestapps.stripe.webhook.events",
        "{event}",
        "The number of webhook deliveries received from Stripe, by event type and outcome.");

    /// <summary>
    /// The time the payment handlers took to process a Stripe event, inline or from the inbox, tagged with the
    /// <see cref="EventTypeTag"/> and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> WebhookHandlerDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.stripe.webhook.handler.duration",
        "The time the payment handlers took to process a Stripe event.");

    /// <summary>
    /// The number of calls made to Stripe's API, tagged with the <c>operation</c> (the API resource), the
    /// <c>outcome</c> and the <c>status_class</c>.
    /// </summary>
    public static Counter<long> ApiRequests { get; } = Meter.CreateCounter<long>(
        "crestapps.stripe.api.requests",
        "{request}",
        "The number of calls made to Stripe's API, by resource and outcome.");

    /// <summary>
    /// The duration of calls made to Stripe's API, tagged with the <c>operation</c> and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> ApiDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.stripe.api.duration",
        "The duration of calls made to Stripe's API.");

    /// <summary>
    /// Records a webhook delivery Stripe made.
    /// </summary>
    /// <param name="eventType">The bounded event type, with unhandled types as <c>other</c>.</param>
    /// <param name="outcome">How receiving it went, such as <see cref="WebhookDuplicate"/>.</param>
    public static void RecordWebhookEvent(string eventType, string outcome)
    {
        if (!WebhookEvents.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { EventTypeTag, eventType ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        WebhookEvents.Add(1, tags);
    }

    /// <summary>
    /// Starts measuring the payment handlers' processing of a Stripe event.
    /// </summary>
    /// <param name="eventType">The bounded event type, with unhandled types as <c>other</c>.</param>
    /// <param name="eventId">The id of the event, set on the span only.</param>
    /// <returns>The scope, which records the <see cref="WebhookHandlerDuration"/> when disposed.</returns>
    public static TelemetryScope StartWebhookDispatch(string eventType, string eventId)
    {
        var scope = TelemetryScope.Start(
            ActivitySource,
            "crestapps.stripe.webhook.dispatch",
            WebhookHandlerDuration,
            new TagList
            {
                { EventTypeTag, eventType ?? CrestAppsOrchardCoreTelemetry.Unknown },
            });

        if (!string.IsNullOrEmpty(eventId))
        {
            scope.SetSpanTag(EventIdTag, eventId);
        }

        return scope;
    }

    /// <summary>
    /// Gets the bounded <c>operation</c> of a call to Stripe's API: the API resource from the first segment of the
    /// path after the version, such as <c>payment_intents</c> for <c>/v1/payment_intents/pi_123/confirm</c>.
    /// </summary>
    /// <param name="requestUri">The URI of the call.</param>
    /// <returns>The resource, or <c>other</c> when it is not one the integration calls.</returns>
    public static string GetApiOperation(Uri requestUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        var segments = requestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // The first segment is the API version, such as "v1".
        if (segments.Length < 2)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        return _apiResources.Contains(segments[1])
            ? segments[1]
            : CrestAppsOrchardCoreTelemetry.Other;
    }

    /// <summary>
    /// Gets the <c>outcome</c> of a call to Stripe's API from its HTTP status code.
    /// </summary>
    /// <param name="statusCode">The status code of the response.</param>
    /// <returns><c>ok</c> for a success, otherwise the kind of failure, such as <see cref="ApiCardDeclined"/>.</returns>
    public static string GetApiOutcome(int statusCode)
    {
        return statusCode switch
        {
            < 400 => CrestAppsOrchardCoreTelemetry.OutcomeOk,
            402 => ApiCardDeclined,
            429 => ApiRateLimited,
            401 or 403 => ApiAuthenticationError,
            < 500 => ApiInvalidRequest,
            _ => ApiError,
        };
    }

    /// <summary>
    /// Records a call made to Stripe's API.
    /// </summary>
    /// <param name="operation">The bounded API resource, from <see cref="GetApiOperation(Uri)"/>.</param>
    /// <param name="outcome">How the call went, such as <see cref="ApiRateLimited"/>.</param>
    /// <param name="statusCode">The status code of the response, or <see langword="null"/> when there was none.</param>
    /// <param name="elapsed">The duration of the call.</param>
    public static void RecordApiRequest(string operation, string outcome, int? statusCode, TimeSpan elapsed)
    {
        if (!ApiRequests.Enabled && !ApiDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation ?? CrestAppsOrchardCoreTelemetry.Other },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        ApiDuration.Record(elapsed.TotalSeconds, tags);

        tags.Add(CrestAppsOrchardCoreTelemetry.StatusClassTag, ExternalCallTelemetry.GetStatusClass(statusCode));
        ApiRequests.Add(1, tags);
    }
}
