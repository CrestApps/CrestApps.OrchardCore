using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// Defines the stable OpenTelemetry contract for the Contact Center module. The <see cref="ActivitySource"/>
/// and <see cref="Meter"/> names are a public integration surface: operators subscribe to them from an
/// OpenTelemetry exporter, so they must not change without a documented migration. Instruments are process-wide
/// and thread-safe by design, following the standard <see cref="System.Diagnostics.Metrics"/> pattern.
/// </summary>
public static class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> that emits Contact Center distributed traces.
    /// </summary>
    public const string ActivitySourceName = "CrestApps.OrchardCore.ContactCenter";

    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits Contact Center metrics.
    /// </summary>
    public const string MeterName = "CrestApps.OrchardCore.ContactCenter";

    private static readonly Meter _meter = new(MeterName);

    private static readonly Counter<long> _outboxRedelivered = _meter.CreateCounter<long>(
        "contactcenter.outbox.redelivered",
        unit: "{message}",
        description: "The number of Contact Center domain events successfully redelivered from the durable outbox.");

    private static readonly Counter<long> _outboxDeadLettered = _meter.CreateCounter<long>(
        "contactcenter.outbox.dead_lettered",
        unit: "{message}",
        description: "The number of Contact Center domain events dead-lettered after exhausting their retry budget.");

    /// <summary>
    /// Gets the shared <see cref="System.Diagnostics.ActivitySource"/> used to start Contact Center trace spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

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
    }

    /// <summary>
    /// Records that a Contact Center domain event was dead-lettered.
    /// </summary>
    /// <param name="reason">A low-cardinality reason category used as a metric dimension.</param>
    public static void RecordOutboxDeadLettered(string reason)
    {
        _outboxDeadLettered.Add(1, new KeyValuePair<string, object>("reason", reason ?? "unspecified"));
    }
}
