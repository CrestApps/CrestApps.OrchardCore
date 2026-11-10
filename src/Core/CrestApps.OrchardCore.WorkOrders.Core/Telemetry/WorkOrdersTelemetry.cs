using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.WorkOrders.Core.Telemetry;

/// <summary>
/// The meter and activity source of work orders, <c>CrestApps.OrchardCore.WorkOrders</c>: the service level warnings
/// and breaches raised. Work order, account and agreement ids go on spans and logs only.
/// </summary>
public static class WorkOrdersTelemetry
{
    /// <summary>
    /// The name of the work orders meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".WorkOrders";

    /// <summary>
    /// The tag that records what the service level monitor raised: <see cref="SlaWarning"/> or <see cref="SlaBreached"/>.
    /// </summary>
    public const string EventTag = "event";

    /// <summary>
    /// The tag that records which service level target the event is about: <c>response</c> or <c>resolution</c>.
    /// </summary>
    public const string TargetTag = "target";

    /// <summary>
    /// A work order came close to missing a target.
    /// </summary>
    public const string SlaWarning = "warning";

    /// <summary>
    /// A work order missed a target.
    /// </summary>
    public const string SlaBreached = "breached";

    /// <summary>
    /// The meter of work orders.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("WorkOrders");

    /// <summary>
    /// The activity source of work orders.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("WorkOrders");

    /// <summary>
    /// The number of service level warnings and breaches raised, tagged with <see cref="EventTag"/> and
    /// <see cref="TargetTag"/>. Each target of a work order raises each event once.
    /// </summary>
    public static Counter<long> SlaEvents { get; } = Meter.CreateCounter<long>(
        "crestapps.workorders.sla.events",
        "{event}",
        "The number of service level warnings and breaches raised.");

    /// <summary>
    /// Records a service level warning or breach.
    /// </summary>
    /// <param name="breached"><see langword="true"/> for a breach; <see langword="false"/> for a warning.</param>
    /// <param name="target">The target, <c>Response</c> or <c>Resolution</c>.</param>
    public static void RecordSlaEvent(bool breached, string target)
    {
        if (!SlaEvents.Enabled)
        {
            return;
        }

        var targetTag = target switch
        {
            "Response" => "response",
            "Resolution" => "resolution",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };

        var tags = new TagList
        {
            { EventTag, breached ? SlaBreached : SlaWarning },
            { TargetTag, targetTag },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SlaEvents.Add(1, tags);
    }
}
