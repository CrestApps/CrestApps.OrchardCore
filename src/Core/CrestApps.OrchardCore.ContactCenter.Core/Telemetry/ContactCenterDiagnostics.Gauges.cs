using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The gauges: backlog numbers the health checks and the dispatch passes already read, kept in memory per tenant and
/// reported by observable gauges whose callbacks never touch a database.
/// </summary>
/// <remarks>
/// An observable gauge's callback is synchronous and runs with no tenant scope, so it cannot query a tenant's store.
/// The code that already reads these numbers inside a tenant scope (the Contact Center health checks, and the outbox
/// redelivery pass) stores the last value it read here, and the callbacks report it. A value is only as fresh as the
/// last health probe or pass that read it, and one not refreshed for 15 minutes is no longer
/// reported. Without the tenant tag, counts are summed across tenants and ages take the oldest. Every node reports
/// what it last read from the shared store, so aggregate across nodes with <c>max</c>, not <c>sum</c>.
/// </remarks>
public static partial class ContactCenterDiagnostics
{
    internal const string SubsystemTag = "subsystem";

    internal const string SubsystemOutbox = "outbox";

    internal const string SubsystemProviderIngress = "provider_ingress";

    private static readonly TimeSpan _gaugeStaleAfter = TimeSpan.FromMinutes(15);

    private static readonly ConcurrentDictionary<(string Tenant, string Name, string Qualifier), Observation> _observations = new();

    private static readonly ObservableGauge<long> _queueDepth = Meter.CreateObservableGauge(
        "crestapps.contactcenter.queue.depth",
        () => Observe("queue.depth", sum: true),
        "{item}",
        "The number of interactions waiting for an agent across every queue, as the queue backlog health check last read it.");

    private static readonly ObservableGauge<long> _backlog = Meter.CreateObservableGauge(
        "crestapps.contactcenter.backlog",
        () => Observe("backlog", sum: true),
        "{message}",
        "The dead-lettered and overdue messages of the event outbox and the provider ingress inbox, as their health checks last read them.");

    private static readonly ObservableGauge<long> _backlogStatus = Meter.CreateObservableGauge(
        "crestapps.contactcenter.backlog.status",
        () => Observe("backlog.status", sum: false),
        "1",
        "The health the backlog health checks last reported: 0 healthy, 1 degraded, 2 unhealthy.");

    private static readonly ObservableGauge<long> _outboxOldestDueAge = Meter.CreateObservableGauge(
        "crestapps.contactcenter.outbox.pending.age",
        () => Observe("outbox.pending.age", sum: false),
        "s",
        "The age of the oldest event outbox message that was due for redelivery when the last redelivery pass ran.");

    private static readonly ObservableGauge<long> _nodeServing = Meter.CreateObservableGauge(
        "crestapps.contactcenter.node.serving",
        () => Observe("node.serving", sum: false),
        "1",
        "Whether this node's serving gate lets it take Contact Center work: 1 serving, 0 drained after failed store probes.");

    /// <summary>
    /// Stores the number of interactions waiting across every queue of the current tenant.
    /// </summary>
    /// <param name="waiting">The number waiting.</param>
    internal static void ObserveQueueDepth(long waiting)
        => Store("queue.depth", null, waiting);

    /// <summary>
    /// Stores the backlog a health check read and the health <see cref="HealthChecks.BacklogHealthEvaluator"/>
    /// decided from it.
    /// </summary>
    /// <param name="subsystem"><see cref="SubsystemOutbox"/> or <see cref="SubsystemProviderIngress"/>.</param>
    /// <param name="deadLettered">The number of dead-lettered messages.</param>
    /// <param name="overdue">The number of messages past their due time.</param>
    /// <param name="status">The health the evaluator decided.</param>
    internal static void ObserveBacklog(string subsystem, int deadLettered, int overdue, HealthStatus status)
    {
        Store("backlog", subsystem + "|dead_lettered", deadLettered);
        Store("backlog", subsystem + "|overdue", overdue);
        Store("backlog.status", subsystem, status switch
        {
            HealthStatus.Healthy => 0,
            HealthStatus.Degraded => 1,
            _ => 2,
        });
    }

    /// <summary>
    /// Stores the age of the oldest outbox message the redelivery pass found due.
    /// </summary>
    /// <param name="age">The age, or <see cref="TimeSpan.Zero"/> when nothing was due.</param>
    internal static void ObserveOutboxPendingAge(TimeSpan age)
        => Store("outbox.pending.age", null, (long)Math.Max(0, age.TotalSeconds));

    /// <summary>
    /// Stores whether this node's serving gate lets it take work.
    /// </summary>
    /// <param name="serving">Whether the node is serving.</param>
    internal static void ObserveNodeServing(bool serving)
        => Store("node.serving", null, serving ? 1 : 0);

    private static void Store(string name, string qualifier, long value)
    {
        var tenant = ShellScope.Context?.Settings?.Name ?? string.Empty;

        _observations[(tenant, name, qualifier)] = new Observation(value, Stopwatch.GetTimestamp());
    }

    private static IEnumerable<Measurement<long>> Observe(string name, bool sum)
    {
        var byTag = new Dictionary<(string Tenant, string Qualifier), long>();

        foreach (var (key, observation) in _observations)
        {
            if (Stopwatch.GetElapsedTime(observation.Timestamp) > _gaugeStaleAfter)
            {
                // A tenant that stopped reporting, such as one released or removed, is dropped rather than kept for
                // the life of the process. Removed only when no newer value replaced it in the meantime.
                _observations.TryRemove(KeyValuePair.Create(key, observation));

                continue;
            }

            if (!string.Equals(key.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            var tenant = CrestAppsOrchardCoreTelemetry.IncludeTenantName ? key.Tenant : string.Empty;
            var groupKey = (tenant, key.Qualifier);

            byTag[groupKey] = byTag.TryGetValue(groupKey, out var current)
                ? sum ? current + observation.Value : Math.Max(current, observation.Value)
                : observation.Value;
        }

        foreach (var ((tenant, qualifier), value) in byTag)
        {
            var tags = new TagList();

            if (!string.IsNullOrEmpty(qualifier))
            {
                var separator = qualifier.IndexOf('|');

                if (separator < 0)
                {
                    tags.Add(SubsystemTag, qualifier);
                }
                else
                {
                    tags.Add(SubsystemTag, qualifier[..separator]);
                    tags.Add(StateTag, qualifier[(separator + 1)..]);
                }
            }

            if (!string.IsNullOrEmpty(tenant))
            {
                tags.Add(CrestAppsOrchardCoreTelemetry.TenantTag, tenant);
            }

            yield return new Measurement<long>(value, tags);
        }
    }

    private readonly record struct Observation(long Value, long Timestamp);
}
