using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Dispatch.Models;

namespace CrestApps.OrchardCore.Dispatch.Core.Telemetry;

/// <summary>
/// The meter and activity source of dispatch, <c>CrestApps.OrchardCore.Dispatch</c>: the visits' transitions, the
/// observers that failed to handle them and the open dispatch boards. Visit, work order and worker ids go on spans
/// and logs only.
/// </summary>
public static class DispatchTelemetry
{
    /// <summary>
    /// The name of the dispatch meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Dispatch";

    /// <summary>
    /// The name of the deferred work that tells the open dispatch boards a visit changed.
    /// </summary>
    public const string BoardNotificationWork = "dispatch_board_notification";

    /// <summary>
    /// The tag that records what happened to a visit: <c>created</c>, <c>assigned</c>, <c>unassigned</c>,
    /// <c>status_changed</c>, <c>at_risk</c> or <c>other</c>.
    /// </summary>
    public const string EventTag = "event";

    /// <summary>
    /// The tag that records the status a visit has after the event.
    /// </summary>
    public const string StatusTag = "status";

    /// <summary>
    /// The tag that records the type name of an observer.
    /// </summary>
    public const string ObserverTag = "observer";

    /// <summary>
    /// The meter of dispatch.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Dispatch");

    /// <summary>
    /// The activity source of dispatch.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Dispatch");

    /// <summary>
    /// The number of visit transitions, tagged with <see cref="EventTag"/> and <see cref="StatusTag"/>.
    /// </summary>
    public static Counter<long> VisitEvents { get; } = Meter.CreateCounter<long>(
        "crestapps.dispatch.visit.events",
        "{event}",
        "The number of visits created, assigned, unassigned or moved to another status.");

    /// <summary>
    /// The number of visit events an observer failed to handle, tagged with <see cref="ObserverTag"/> and
    /// <see cref="EventTag"/>. The failure is logged and swallowed, so the change itself is kept.
    /// </summary>
    public static Counter<long> ObserverFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.dispatch.observer.failures",
        "{failure}",
        "The number of visit events an observer failed to handle.");

    /// <summary>
    /// The number of connections open to the dispatch board hub on this node.
    /// </summary>
    public static UpDownCounter<long> HubConnections { get; } = Meter.CreateUpDownCounter<long>(
        "crestapps.dispatch.hub.connections",
        "{connection}",
        "The number of connections open to the dispatch board hub.");

    /// <summary>
    /// Records a visit transition.
    /// </summary>
    /// <param name="eventName">The event, one of the <c>DispatchConstants.Events</c> names.</param>
    /// <param name="status">The status of the visit after the event.</param>
    public static void RecordVisitEvent(string eventName, VisitStatus status)
    {
        if (!VisitEvents.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { EventTag, GetEventTag(eventName) },
            { StatusTag, status.ToString() },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        VisitEvents.Add(1, tags);
    }

    /// <summary>
    /// Records that an observer failed to handle a visit event.
    /// </summary>
    /// <param name="observer">The observer's type.</param>
    /// <param name="eventName">The event, one of the <c>DispatchConstants.Events</c> names.</param>
    public static void RecordObserverFailure(Type observer, string eventName)
    {
        if (!ObserverFailures.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ObserverTag, observer?.Name ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { EventTag, GetEventTag(eventName) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ObserverFailures.Add(1, tags);
    }

    /// <summary>
    /// Records that a connection opened to the dispatch board hub.
    /// </summary>
    public static void HubConnected()
    {
        AddConnection(1);
    }

    /// <summary>
    /// Records that a connection to the dispatch board hub closed.
    /// </summary>
    public static void HubDisconnected()
    {
        AddConnection(-1);
    }

    /// <summary>
    /// Maps a visit event name to its bounded tag value.
    /// </summary>
    /// <param name="eventName">The event, one of the <c>DispatchConstants.Events</c> names.</param>
    /// <returns>The tag value, or <c>other</c> for an event this list does not know.</returns>
    public static string GetEventTag(string eventName)
    {
        return eventName switch
        {
            DispatchConstants.Events.Created => "created",
            DispatchConstants.Events.Assigned => "assigned",
            DispatchConstants.Events.Unassigned => "unassigned",
            DispatchConstants.Events.StatusChanged => "status_changed",
            DispatchConstants.Events.AtRisk => "at_risk",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    private static void AddConnection(long delta)
    {
        if (!HubConnections.Enabled)
        {
            return;
        }

        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        HubConnections.Add(delta, tags);
    }
}
