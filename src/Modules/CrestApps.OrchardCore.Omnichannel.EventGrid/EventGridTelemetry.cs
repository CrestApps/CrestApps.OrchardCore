using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid;

/// <summary>
/// The OpenTelemetry instruments of the Azure Event Grid webhook, under the meter <c>CrestApps.OrchardCore.EventGrid</c>:
/// each event a delivery carried. What became of the delivery itself is tagged on the ASP.NET Core server span. An event's id, subject and
/// payload never go on a metric; the subject of an Azure Communication Services event is the customer's phone number.
/// </summary>
public static class EventGridTelemetry
{
    /// <summary>
    /// The name of the meter.
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
    /// The number of events the Event Grid deliveries carried, tagged with the <see cref="TypeTag"/> and the result
    /// (see <see cref="EventResults"/>).
    /// </summary>
    public static Counter<long> Events { get; } = Meter.CreateCounter<long>(
        "crestapps.eventgrid.event",
        "{event}",
        "The number of Azure Event Grid events received, by type and result.");

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
    /// The results of a delivery, set as <c>crestapps.eventgrid.result</c> on the ASP.NET Core server span.
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
