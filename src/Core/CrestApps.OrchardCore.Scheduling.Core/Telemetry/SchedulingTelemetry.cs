using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Scheduling.Core.Telemetry;

/// <summary>
/// The meter of appointment scheduling, <c>CrestApps.OrchardCore.Scheduling</c>. Appointment, customer and resource
/// ids go on logs only.
/// </summary>
public static class SchedulingTelemetry
{
    /// <summary>
    /// The name of the scheduling meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Scheduling";

    /// <summary>
    /// The name of the deferred work that tells the appointment observers about the changes of a request.
    /// </summary>
    public const string AppointmentChangesWork = "appointment_changes";

    /// <summary>
    /// The tag that records the type name of an observer.
    /// </summary>
    public const string ObserverTag = "observer";

    /// <summary>
    /// The tag that records the kind of a change.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// The meter of appointment scheduling.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Scheduling");

    /// <summary>
    /// The number of appointment changes an observer failed to handle, tagged with <see cref="ObserverTag"/> and
    /// <see cref="KindTag"/>. The failure is logged and swallowed, so the change itself is kept.
    /// </summary>
    public static Counter<long> ObserverFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.scheduling.observer.failures",
        "{failure}",
        "The number of appointment changes an observer failed to handle.");

    /// <summary>
    /// Records that an observer failed to handle an appointment change.
    /// </summary>
    /// <param name="observer">The observer's type.</param>
    /// <param name="kind">The kind of the change.</param>
    public static void RecordObserverFailure(Type observer, string kind)
    {
        if (!ObserverFailures.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ObserverTag, observer?.Name ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { KindTag, kind ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ObserverFailures.Add(1, tags);
    }
}
