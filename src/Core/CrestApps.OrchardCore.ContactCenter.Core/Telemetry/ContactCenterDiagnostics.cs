using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// Defines the stable OpenTelemetry contract for the Contact Center module. The <see cref="ActivitySource"/>
/// and <see cref="Meter"/> names are a public integration surface: operators subscribe to them from an
/// OpenTelemetry exporter, so they must not change without a documented migration. Instruments are process-wide
/// and thread-safe by design, following the standard <see cref="System.Diagnostics.Metrics"/> pattern.
/// </summary>
/// <remarks>
/// The instruments are named <c>crestapps.contactcenter.&lt;thing&gt;</c> and are grouped by funnel in the partial
/// files of this class: calls, offers, queues, inbound routing, the outbox, provider commands, the dialer, agents and
/// recovery. Each is recorded at the state transition it describes, never in a replayable outbox or projection
/// handler, so a redelivered event is not counted twice. The <c>contactcenter.outbox.*</c> instruments are the
/// names this meter shipped first; they keep working for one release beside their <c>crestapps.contactcenter.outbox.*</c>
/// replacements, then are retired.
/// </remarks>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> that emits Contact Center distributed traces.
    /// </summary>
    public const string ActivitySourceName = "CrestApps.OrchardCore.ContactCenter";

    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits Contact Center metrics.
    /// </summary>
    public const string MeterName = "CrestApps.OrchardCore.ContactCenter";

    internal const string Area = "ContactCenter";

    internal const string StateTag = "state";

    internal const string KindTag = "kind";

    internal const string StageTag = "stage";

    internal const string SiteTag = "site";

    internal const string HandlerTag = "handler";

    internal const string DeliveryTag = "delivery";

    internal const string ActionTag = "action";

    internal const string HangupCauseTag = "hangup.cause";

    internal const string HangupSourceTag = "hangup.source";

    internal const string CommandTypeTag = "command.type";

    internal const string DialerModeTag = "dialer.mode";

    internal const string SuppressReasonTag = "suppress.reason";

    // Retired after one release: dashboards move to crestapps.contactcenter.outbox.redelivered and .dead_lettered.
    private static readonly Counter<long> _outboxRedelivered = Meter.CreateCounter<long>(
        "contactcenter.outbox.redelivered",
        unit: "{message}",
        description: "Deprecated: use crestapps.contactcenter.outbox.redelivered. The number of Contact Center domain events successfully redelivered from the durable outbox.");

    private static readonly Counter<long> _outboxDeadLettered = Meter.CreateCounter<long>(
        "contactcenter.outbox.dead_lettered",
        unit: "{message}",
        description: "Deprecated: use crestapps.contactcenter.outbox.dead_lettered. The number of Contact Center domain events dead-lettered after exhausting their retry budget.");

    /// <summary>
    /// Gets the shared <see cref="System.Diagnostics.ActivitySource"/> used to start Contact Center trace spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// Gets the meter every Contact Center instrument is created on.
    /// </summary>
    /// <remarks>
    /// The instruments are fields of the partial files of this class, whose initializers run in no defined order, so
    /// the meter is held by a nested class that is always initialized before the first instrument asks for it.
    /// </remarks>
    internal static Meter Meter => MeterHolder.Instance;

    /// <summary>
    /// Records that one or more Contact Center domain events were redelivered from the outbox.
    /// </summary>
    /// <param name="count">The number of redelivered events. Non-positive values are ignored.</param>
    public static void RecordOutboxRedelivered(long count)
    {
        if (count <= 0)
        {
            return;
        }

        _outboxRedelivered.Add(count);

        if (_outboxRedeliveredNew.Enabled)
        {
            Add(_outboxRedeliveredNew, default, count);
        }
    }

    /// <summary>
    /// Records that a Contact Center domain event was dead-lettered.
    /// </summary>
    /// <param name="reason">A low-cardinality reason category used as a metric dimension.</param>
    public static void RecordOutboxDeadLettered(string reason)
    {
        reason ??= "unspecified";

        _outboxDeadLettered.Add(1, new KeyValuePair<string, object>(CrestAppsOrchardCoreTelemetry.ReasonTag, reason));

        if (_outboxDeadLetteredNew.Enabled)
        {
            Add(_outboxDeadLetteredNew, new TagList { { CrestAppsOrchardCoreTelemetry.ReasonTag, reason } });
        }
    }

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.DirectionTag"/> value of a direction.
    /// </summary>
    /// <param name="direction">The direction of the interaction.</param>
    /// <returns><c>inbound</c> or <c>outbound</c>.</returns>
    internal static string GetDirection(InteractionDirection direction)
        => direction == InteractionDirection.Outbound ? "outbound" : "inbound";

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.ChannelTag"/> value of a channel.
    /// </summary>
    /// <param name="channel">The channel of the interaction, or <see langword="null"/> when it is not known.</param>
    /// <returns><c>voice</c>, <c>sms</c>, <c>email</c>, <c>chat</c> or <c>unknown</c>.</returns>
    internal static string GetChannel(InteractionChannel? channel)
        => channel switch
        {
            InteractionChannel.Voice => "voice",
            InteractionChannel.Sms => "sms",
            InteractionChannel.Email => "email",
            InteractionChannel.Chat => "chat",
            _ => CrestAppsOrchardCoreTelemetry.Unknown,
        };

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> value of a provider's canonical technical name.
    /// The names come from the installed providers, so they form a bounded set.
    /// </summary>
    /// <param name="providerName">The canonical provider name.</param>
    /// <returns>The provider name, or <c>unknown</c>.</returns>
    internal static string GetProvider(string providerName)
        => string.IsNullOrWhiteSpace(providerName) ? CrestAppsOrchardCoreTelemetry.Unknown : providerName;

    /// <summary>
    /// Gets the <see cref="KindTag"/> of the routing a queue id stands for: a direct-to-agent line, a campaign's virtual
    /// queue, or a real queue.
    /// </summary>
    /// <param name="queueId">The queue id. It is read, never tagged.</param>
    /// <returns><c>direct</c>, <c>campaign</c> or <c>queue</c>.</returns>
    internal static string GetRoutingKind(string queueId)
        => ContactCenterConstants.IsDirectRoutingQueue(queueId)
            ? "direct"
            : ContactCenterConstants.IsCampaignQueue(queueId)
                ? "campaign"
                : "queue";

    // Only the exception's type goes on the span: a message can carry a phone number or a SIP address.
    private static void AddExceptionEvent(Exception exception)
        => Activity.Current?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
        }));

    private static void Add(Counter<long> counter, TagList tags, long value = 1)
    {
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    private static void Add(UpDownCounter<long> counter, TagList tags, long value)
    {
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    private static void Record(Histogram<double> histogram, double value, TagList tags)
    {
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(value, tags);
    }

    private static void Record(Gauge<long> gauge, long value, TagList tags)
    {
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        gauge.Record(value, tags);
    }

    private static class MeterHolder
    {
        public static readonly Meter Instance = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);
    }
}
