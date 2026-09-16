namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Contains the identifiers used to register and select the Contact Center operational health checks.
    /// The readiness endpoint selects checks by <see cref="HealthChecks.ReadyTag"/>, so a registration that
    /// omits the tag silently disappears from readiness. Both sides must reference these constants.
    /// </summary>
    public static class HealthChecks
    {
        /// <summary>
        /// The tag applied to every Contact Center health check, used to distinguish them from checks
        /// contributed by other modules.
        /// </summary>
        public const string AreaTag = "contactcenter";

        /// <summary>
        /// The tag applied to node-local readiness checks. The readiness probe selects this tag and nothing
        /// else, so a check that observes a condition every node shares must never carry it: gating rotation on
        /// such a condition drains the whole fleet at once.
        /// </summary>
        /// <remarks>
        /// The tag is namespaced rather than the conventional bare <c>ready</c> because the probe selects by tag
        /// across the whole shell container. A bare tag would silently enlist any other module's readiness check
        /// — including a shared-infrastructure check such as a Redis backplane probe — and reintroduce exactly
        /// the fleet-wide drain the split exists to prevent.
        /// </remarks>
        public const string ReadyTag = "contactcenter-ready";

        /// <summary>
        /// The tag applied to checks that consult an external dependency. These are alerting signals surfaced
        /// through the dependency probe and must never gate load balancer rotation.
        /// </summary>
        public const string DependencyTag = "contactcenter-dependency";

        /// <summary>
        /// The registration name of the node-local readiness check.
        /// </summary>
        public const string NodeCheckName = "contactcenter-node";

        /// <summary>
        /// The registration name of the opt-in node-local serving gate.
        /// </summary>
        public const string NodeServingCheckName = "contactcenter-node-serving";

        /// <summary>
        /// The registration name of the durable-storage reachability check.
        /// </summary>
        public const string StorageCheckName = "contactcenter-storage";

        /// <summary>
        /// The registration name of the event outbox backlog check.
        /// </summary>
        public const string OutboxCheckName = "contactcenter-outbox";

        /// <summary>
        /// The registration name of the live active-call gauge check that surfaces the count of call sessions
        /// that have not yet ended as health data. It reports the number of live calls an operator would
        /// interrupt by draining a node rather than gating a probe, so it is healthy whenever the count can be
        /// read.
        /// </summary>
        public const string ActiveCallsCheckName = "contactcenter-active-calls";

        /// <summary>
        /// The registration name of the queued-interaction backlog gauge check that surfaces the count of
        /// interactions waiting for an agent across every queue as health data. It reports the routed work still
        /// waiting rather than gating a probe, so it is healthy whenever the count can be read.
        /// </summary>
        public const string QueueBacklogCheckName = "contactcenter-queue-backlog";

        /// <summary>
        /// The registration name of the provider ingress inbox backlog check.
        /// </summary>
        public const string ProviderIngressCheckName = "contactcenter-provider-ingress";

        /// <summary>
        /// The registration name of the shared aggregate health endpoint hazard check.
        /// </summary>
        /// <remarks>
        /// This is an alerting-only dependency check, never a readiness check. It surfaces the case where the
        /// <c>OrchardCore.HealthChecks</c> aggregate endpoint is named as a liveness probe while Contact Center is
        /// enabled. Failing readiness here would drain the node, which is the very restart behavior the hazard
        /// warns against, so it reports degraded at most.
        /// </remarks>
        public const string SharedEndpointCheckName = "contactcenter-shared-endpoint";

        /// <summary>
        /// The registration name of the deployment topology check.
        /// </summary>
        /// <remarks>
        /// This is one of two readiness checks that observe a condition every node shares (the other is
        /// <see cref="BaseVoiceVerificationCheckName"/>). The exception is deliberate: a topology violation
        /// cannot self-heal, and serving traffic from a deployment that does not satisfy its declared support
        /// contract is the failure being prevented rather than collateral damage from preventing it.
        /// </remarks>
        public const string TopologyCheckName = "contactcenter-topology";

        /// <summary>
        /// The registration name of the base-voice audio verification check.
        /// </summary>
        /// <remarks>
        /// Like <see cref="TopologyCheckName"/>, this readiness check observes a deployment-wide condition rather
        /// than node-local state, and the exception is deliberate for the same reason: whether the base-voice
        /// media path was proven is fixed infrastructure that no amount of waiting repairs, so a production host
        /// that has not acknowledged the verification withholds readiness rather than serve an unproven voice
        /// path.
        /// </remarks>
        public const string BaseVoiceVerificationCheckName = "contactcenter-base-voice-verification";

        /// <summary>
        /// The registration name of the distributed-lock acquire/release probe.
        /// </summary>
        /// <remarks>
        /// A dependency probe that proves the resolved <c>IDistributedLock</c> can be taken and released within a
        /// bounded time. In a production topology this exercises the Redis-backed lock end to end; in a
        /// development topology it exercises the process-local lock and is trivially satisfied.
        /// </remarks>
        public const string DistributedLockCheckName = "contactcenter-distributed-lock";

        /// <summary>
        /// The registration name of the Redis connectivity probe.
        /// </summary>
        /// <remarks>
        /// A dependency probe that pings the Redis connection shared by the distributed lock and the SignalR
        /// backplane. It reports healthy with nothing probed when Redis is not enabled, because a deployment
        /// that declares no Redis dependency has none to be unhealthy about; the topology validator, not this
        /// probe, decides whether Redis is required.
        /// </remarks>
        public const string RedisConnectivityCheckName = "contactcenter-redis";

        /// <summary>
        /// The registration name of the SignalR backplane publish/subscribe round-trip probe.
        /// </summary>
        /// <remarks>
        /// Redis connectivity alone does not prove the backplane works: a pub/sub round-trip on a dedicated,
        /// tenant-qualified channel is the only signal that a message published on one node would reach the
        /// subscribers on another. Reports healthy with nothing probed when Redis is not enabled.
        /// </remarks>
        public const string BackplaneCheckName = "contactcenter-backplane";

        /// <summary>
        /// The default path of the process liveness probe. It reports only that the process can serve a
        /// request and never consults a dependency, so a failing database or a growing backlog cannot trigger a
        /// restart.
        /// </summary>
        /// <remarks>
        /// Liveness is answered by host middleware placed ahead of the Orchard Core pipeline, not by a tenant
        /// feature. A route mapped inside a shell answers 404 whenever that tenant is disabled, renamed, or
        /// fails to start, and an orchestrator reads 404 as a probe failure — so a tenant-scoped liveness route
        /// restarts an otherwise healthy process for a tenant-level problem.
        /// <para>
        /// The path deliberately avoids <c>/health/live</c>, which is the default route of the
        /// <c>OrchardCore.HealthChecks</c> module. Host middleware short-circuits before routing, so taking that
        /// path would silently shadow that module's endpoint for every tenant in the process — including
        /// tenants that never enable Contact Center — and answer a permanent <c>200 Healthy</c> in its place.
        /// Shadowing a health endpoint with an unconditional success is a worse failure than any it could
        /// report.
        /// </para>
        /// </remarks>
        public const string ProcessLivenessPath = "/health/process";

        /// <summary>
        /// The route of the readiness probe. It aggregates every check tagged <see cref="ReadyTag"/>, which is
        /// only node-local state, and reports whether this node should receive traffic for this tenant.
        /// </summary>
        public const string ReadinessRoute = "api/contact-center/health/ready";

        /// <summary>
        /// The route of the dependency probe. It aggregates every check tagged <see cref="DependencyTag"/> and
        /// reports per-check detail, so it requires authorization and must never be wired to an orchestrator
        /// probe or a load balancer.
        /// </summary>
        public const string DependenciesRoute = "api/contact-center/health/dependencies";
    }
}
