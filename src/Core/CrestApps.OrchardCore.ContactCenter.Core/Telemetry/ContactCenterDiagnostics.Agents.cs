using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// Agents and self-healing: agent state, the agent hub's connections, real-time broadcasts, and the recovery passes
/// that put routing state right. Any non-zero rate of healing is a state-machine leak worth an alert.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    private static readonly Counter<long> _recoveryHealed = Meter.CreateCounter<long>(
        "crestapps.contactcenter.recovery.healed",
        "{repair}",
        "The number of agent or work states the healing passes found wrong and put right, by kind. Any steady rate is a state-machine leak.");

    private static readonly Counter<long> _recoveryOrphans = Meter.CreateCounter<long>(
        "crestapps.contactcenter.recovery.orphan_recovered",
        "{item}",
        "The number of orphaned activities, reservations or queue items recovered, by kind. Any steady rate is a state-machine leak.");

    private static readonly Histogram<double> _agentStateDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.agent.state.duration",
        "The time an agent spent in a presence state before leaving it, by state.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    private static readonly UpDownCounter<long> _hubConnections = Meter.CreateUpDownCounter<long>(
        "crestapps.contactcenter.hub.connections",
        "{connection}",
        "The number of agent and supervisor connections open on this node's Contact Center hub.");

    private static readonly Counter<long> _hubRejected = Meter.CreateCounter<long>(
        "crestapps.contactcenter.hub.rejected",
        "{connection}",
        "The number of Contact Center hub connections or calls refused, by reason.");

    private static readonly Counter<long> _hubHeartbeatsLost = Meter.CreateCounter<long>(
        "crestapps.contactcenter.hub.heartbeat.lost",
        "{session}",
        "The number of agent sessions ended because their heartbeat stopped.");

    private static readonly Histogram<double> _realtimeBroadcastDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.realtime.broadcast.duration",
        "The time from when a change occurred to when it was broadcast to agent and supervisor screens, by notification.");

    private static readonly Counter<long> _realtimeNotifyFailures = Meter.CreateCounter<long>(
        "crestapps.contactcenter.realtime.notify.failures",
        "{notification}",
        "The number of real-time notifications that could not be sent to agent or supervisor screens, by notification.");

    /// <summary>
    /// Records that a healing pass found a state wrong and put it right.
    /// </summary>
    /// <param name="kind">What was repaired, from a bounded set, such as <c>agent_state</c> or <c>work_state</c>.</param>
    /// <param name="count">How many.</param>
    internal static void RecordRecoveryHealed(string kind, long count = 1)
    {
        if (count <= 0 || !_recoveryHealed.Enabled)
        {
            return;
        }

        Add(_recoveryHealed, new TagList { { KindTag, kind } }, count);
    }

    /// <summary>
    /// Records that orphaned work was recovered.
    /// </summary>
    /// <param name="kind">What was recovered: <c>activity</c>, <c>reservation</c> or <c>queue_item</c>.</param>
    /// <param name="count">How many.</param>
    internal static void RecordOrphanRecovered(string kind, long count = 1)
    {
        if (count <= 0 || !_recoveryOrphans.Enabled)
        {
            return;
        }

        Add(_recoveryOrphans, new TagList { { KindTag, kind } }, count);
    }

    /// <summary>
    /// Records how long an agent spent in a presence state they are leaving.
    /// </summary>
    /// <param name="state">The state they leave, a presence status name.</param>
    /// <param name="elapsed">How long they were in it.</param>
    internal static void RecordAgentStateDuration(string state, TimeSpan elapsed)
    {
        if (!_agentStateDuration.Enabled)
        {
            return;
        }

        Record(_agentStateDuration, Math.Max(0, elapsed.TotalSeconds), new TagList { { StateTag, state } });
    }

    /// <summary>
    /// Records that a hub connection opened or closed on this node.
    /// </summary>
    /// <param name="delta"><c>1</c> when it opened, <c>-1</c> when it closed.</param>
    internal static void RecordHubConnection(int delta)
    {
        if (_hubConnections.Enabled)
        {
            Add(_hubConnections, default, delta);
        }
    }

    /// <summary>
    /// Records that the hub refused a connection or a call.
    /// </summary>
    /// <param name="reason">Why, from a bounded set, such as <c>unauthorized</c> or <c>feature_unavailable</c>.</param>
    internal static void RecordHubRejected(string reason)
    {
        if (_hubRejected.Enabled)
        {
            Add(_hubRejected, new TagList { { CrestAppsOrchardCoreTelemetry.ReasonTag, reason } });
        }
    }

    /// <summary>
    /// Records agent sessions ended because their heartbeat stopped.
    /// </summary>
    /// <param name="count">How many.</param>
    internal static void RecordHeartbeatLost(long count)
    {
        if (count > 0 && _hubHeartbeatsLost.Enabled)
        {
            Add(_hubHeartbeatsLost, default, count);
        }
    }

    /// <summary>
    /// Records a real-time broadcast and how far behind the change it announces it went out.
    /// </summary>
    /// <param name="notification">The notification, from the bounded set of hub client methods.</param>
    /// <param name="lag">The time from the change to the broadcast, when the change carries its time.</param>
    /// <param name="exception">The exception the broadcast failed with, or <see langword="null"/>.</param>
    internal static void RecordRealtimeBroadcast(string notification, TimeSpan? lag, Exception exception)
    {
        if (exception is not null)
        {
            AddExceptionEvent(exception);

            if (_realtimeNotifyFailures.Enabled)
            {
                Add(_realtimeNotifyFailures, new TagList { { "event.type", notification } });
            }

            return;
        }

        if (lag is { } elapsed && _realtimeBroadcastDuration.Enabled)
        {
            Record(_realtimeBroadcastDuration, Math.Max(0, elapsed.TotalSeconds), new TagList { { "event.type", notification } });
        }
    }
}
