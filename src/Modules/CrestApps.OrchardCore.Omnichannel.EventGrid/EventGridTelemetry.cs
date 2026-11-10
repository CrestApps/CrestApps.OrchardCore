using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid;

/// <summary>
/// The OpenTelemetry instruments and spans of the Azure Event Grid webhook, under the meter and activity source
/// <c>CrestApps.OrchardCore.EventGrid</c>: each delivery request and each event it carried. An event's id, subject and
/// payload never go on a metric; the subject of an Azure Communication Services event is the customer's phone number.
/// </summary>
public static class EventGridTelemetry
{
    /// <summary>
    /// The name of the meter and the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".EventGrid";

    /// <summary>
    /// The tag that records the bounded type of an event (see <see cref="GetEventTypeTag(string)"/>).
    /// </summary>
    public const string TypeTag = "type";

    /// <summary>
    /// The meter of the Event Grid instruments.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("EventGrid");

    /// <summary>
    /// The activity source of the Event Grid spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("EventGrid");

    /// <summary>
    /// The number of Event Grid delivery requests, tagged with the result (see <see cref="RequestResults"/>).
    /// </summary>
    public static Counter<long> Requests { get; } = Meter.CreateCounter<long>(
        "crestapps.eventgrid.request",
        "{request}",
        "The number of Azure Event Grid delivery requests, by result.");

    /// <summary>
    /// The time the webhook took to answer an Event Grid delivery request, tagged with the result and the outcome.
    /// Event Grid gives up on a delivery it waits more than 30 seconds for.
    /// </summary>
    public static Histogram<double> RequestDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.eventgrid.request.duration",
        "The time the webhook took to answer an Azure Event Grid delivery request.");

    /// <summary>
    /// The number of events the Event Grid deliveries carried, tagged with the <see cref="TypeTag"/> and the result
    /// (see <see cref="EventResults"/>).
    /// </summary>
    public static Counter<long> Events { get; } = Meter.CreateCounter<long>(
        "crestapps.eventgrid.event",
        "{event}",
        "The number of Azure Event Grid events received, by type and result.");

    /// <summary>
    /// Starts measuring a delivery request: the <c>crestapps.eventgrid.request</c> span and <see cref="RequestDuration"/>.
    /// Complete it with <see cref="CompleteRequest(TelemetryScope, string)"/>.
    /// </summary>
    /// <returns>The scope.</returns>
    public static TelemetryScope StartRequest()
    {
        return TelemetryScope.Start(ActivitySource, "crestapps.eventgrid.request", RequestDuration);
    }

    /// <summary>
    /// Records how a delivery request ended.
    /// </summary>
    /// <param name="scope">The scope of the request.</param>
    /// <param name="result">The result, one of <see cref="RequestResults"/>.</param>
    public static void CompleteRequest(TelemetryScope scope, string result)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrEmpty(result);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        switch (result)
        {
            case RequestResults.Ok:
            case RequestResults.Validated:
                scope.Succeed();
                break;

            case RequestResults.Unauthorized:
            case RequestResults.TooLarge:
                scope.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeBlocked);
                break;

            default:
                scope.Fail();
                break;
        }

        RecordRequest(result);
    }

    /// <summary>
    /// Records a delivery request that threw. The exception type is recorded, but not its message: an error raised
    /// while storing an inbound text can carry the phone numbers it was given.
    /// </summary>
    /// <param name="scope">The scope of the request.</param>
    /// <param name="exception">The exception the request ended with.</param>
    public static void FailRequest(TelemetryScope scope, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(exception);

        var outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, RequestResults.Error);

        if (outcome == CrestAppsOrchardCoreTelemetry.OutcomeError)
        {
            scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ErrorTypeTag, exception.GetType().FullName);
            scope.Fail();
        }
        else
        {
            scope.SetOutcome(outcome);
        }

        RecordRequest(RequestResults.Error);
    }

    /// <summary>
    /// Records one event a delivery carried.
    /// </summary>
    /// <param name="eventType">The event type Event Grid sent, mapped to a bounded tag with <see cref="GetEventTypeTag(string)"/>.</param>
    /// <param name="result">The result, one of <see cref="EventResults"/>.</param>
    public static void RecordEvent(string eventType, string result)
    {
        if (!Events.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { TypeTag, GetEventTypeTag(eventType) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Events.Add(1, tags);
    }

    private static void RecordRequest(string result)
    {
        if (!Requests.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Requests.Add(1, tags);
    }

    /// <summary>
    /// Gets the <see cref="TypeTag"/> value of an event type. The event type is chosen by whoever publishes to the topic,
    /// so any type the webhook does not act on is recorded as <c>other</c>.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <returns><c>subscription_validation</c>, <c>sms_received</c>, <c>sms_delivery_report</c> or <c>other</c>.</returns>
    public static string GetEventTypeTag(string eventType)
    {
        if (string.Equals(eventType, EventGridEventTypes.SubscriptionValidation, StringComparison.OrdinalIgnoreCase))
        {
            return "subscription_validation";
        }

        if (string.Equals(eventType, EventGridEventTypes.AcsSmsReceived, StringComparison.OrdinalIgnoreCase))
        {
            return "sms_received";
        }

        if (string.Equals(eventType, EventGridEventTypes.AcsSmsDeliveryReportReceived, StringComparison.OrdinalIgnoreCase))
        {
            return "sms_delivery_report";
        }

        return CrestAppsOrchardCoreTelemetry.Other;
    }

    /// <summary>
    /// The results of <see cref="Requests"/>.
    /// </summary>
    public static class RequestResults
    {
        /// <summary>
        /// Every event of the delivery was taken.
        /// </summary>
        public const string Ok = "ok";

        /// <summary>
        /// The delivery was the subscription validation handshake, and was answered.
        /// </summary>
        public const string Validated = "validated";

        /// <summary>
        /// The delivery carried neither a valid SAS key nor a valid AAD token.
        /// </summary>
        public const string Unauthorized = "unauthorized";

        /// <summary>
        /// The delivery exceeded the maximum payload size.
        /// </summary>
        public const string TooLarge = "too_large";

        /// <summary>
        /// The payload was not a batch of Event Grid events.
        /// </summary>
        public const string ParseError = "parse_error";

        /// <summary>
        /// A subscription validation event carried no validation code.
        /// </summary>
        public const string Invalid = "invalid";

        /// <summary>
        /// An inbound text could not be stored, so Event Grid was asked to deliver the batch again.
        /// </summary>
        public const string Retry = "retry";

        /// <summary>
        /// The webhook threw.
        /// </summary>
        public const string Error = "error";
    }

    /// <summary>
    /// The results of <see cref="Events"/>.
    /// </summary>
    public static class EventResults
    {
        /// <summary>
        /// A subscription validation event was answered with its code.
        /// </summary>
        public const string Validated = "validated";

        /// <summary>
        /// A subscription validation event carried no validation code.
        /// </summary>
        public const string Invalid = "invalid";

        /// <summary>
        /// An inbound text was stored in the provider webhook inbox, to be processed after the response.
        /// </summary>
        public const string Accepted = "accepted";

        /// <summary>
        /// An inbound text was already in the provider webhook inbox.
        /// </summary>
        public const string Duplicate = "duplicate";

        /// <summary>
        /// The provider webhook inbox was busy, so the text was not stored and is delivered again.
        /// </summary>
        public const string Busy = "busy";

        /// <summary>
        /// The text could not be stored, so it is delivered again.
        /// </summary>
        public const string Failed = "failed";

        /// <summary>
        /// The event was stored and handed to the event handlers inline.
        /// </summary>
        public const string Stored = "stored";

        /// <summary>
        /// The event could not be mapped; it was stored with its raw event type.
        /// </summary>
        public const string Malformed = "malformed";
    }
}
