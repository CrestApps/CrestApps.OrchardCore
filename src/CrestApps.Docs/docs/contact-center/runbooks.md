---
sidebar_label: Failure runbooks
sidebar_position: 23
title: Contact Center failure and deployment runbooks
description: Operational runbooks for SQL, Redis/backplane, provider, node, and network failures plus rolling and blue-green deployment.
---

These runbooks cover the dependency and node failures an operator must handle for a Contact Center deployment, plus the two supported deployment strategies. They assume the health, telemetry, retention, and upgrade contracts described in [Production support](production-support.md) and the gates in [Service objectives](service-objectives.md).

Every runbook uses the same signals so responders do not have to learn per-incident tooling:

- **Health probes.** `/api/contact-center/health/dependencies` (requires `MonitorContactCenter`) reports per-check status for `contactcenter-storage`, `contactcenter-outbox`, and — on tenants that enable Voice — `contactcenter-provider-ingress`. **This is the probe to read during an incident.** The anonymous `/api/contact-center/health/ready` and `/health/process` probes are orchestrator signals only: readiness reports node-local state and deliberately stays healthy while a dependency is degraded, so that a shared outage cannot drain every node at once, and `/health/process` reports only that the process is scheduling requests. Never diagnose a dependency from them. Readiness and the dependency report are per tenant and inherit the tenant's request URL prefix; `/health/process` is served by the host and is prefix-independent. See [Production support](production-support.md) for the full probe contract.
- **Metrics** from the `CrestApps.OrchardCore.ContactCenter` meter: `contactcenter.outbox.redelivered` and `contactcenter.outbox.dead_lettered` (tagged by `reason`).
- **Traces** from the `CrestApps.OrchardCore.ContactCenter` activity source.

Thresholds are configurable under `CrestApps_ContactCenter:HealthChecks`; tune them per deployment before relying on the states below.

## General triage

1. Read `/api/contact-center/health/dependencies` for the affected tenant and identify which check is `Degraded` or `Unhealthy`. Corroborate with the `contactcenter.outbox.redelivered` and `contactcenter.outbox.dead_lettered` counters.
2. Correlate with the outbox counters. A rising `contactcenter.outbox.dead_lettered{reason="retry_exhausted"}` means downstream dispatch is failing; a rising `redelivered` with no dead-letters means transient retries are recovering.
3. Pick the matching runbook below. Storage failures cascade into every other subsystem, so always rule out SQL and Redis first.

## SQL (primary datastore) failure

**Detection.** `contactcenter-storage` reports `Unhealthy`; store operations throw; the outbox and provider-ingress checks may also fail because they read the same database.

**Impact.** Contact Center is stateful in SQL: interactions, queue items, call sessions, the durable event outbox, the provider webhook inbox, projection checkpoints, and the interaction event log all live there. A total SQL outage stops routing, disposition, and provider command execution. No data is lost that was committed, because the outbox and inbox are durable and replayed after recovery.

**Response.**

1. Confirm the outage is the database, not the app tier, using the storage health check and the database provider's own metrics.
2. Fail the affected nodes out of the load balancer so they stop returning 5xx to agents and providers. Provider webhooks continue to be accepted only if at least one healthy node remains; otherwise providers will retry per their own policy and the inbox replays on recovery.
3. Restore or fail over the database. Contact Center makes no assumption about the engine beyond YesSql portability, so follow the runbook for the deployed engine (SQLite file restore, or SQL Server / PostgreSQL / MySQL HA failover).
4. After the database is healthy, bring nodes back. The outbox dispatch loop resumes and redelivers pending messages; idempotency keys and fence tokens make redelivery safe. Watch `contactcenter.outbox.redelivered` drain to steady state.
5. If projections look stale after a restore from backup, run the metrics projection rebuild — it recomputes every per-day, per-event-type count from the durable event log and reconciles the stored metrics.

**Prevention.** Provision the database for HA (managed failover or a replica), and keep `ProjectionReplayHorizonDays` and `LegalHoldMinimumDays` set so the event log stays rebuildable after a point-in-time restore.

## Redis / backplane failure

**Detection.** Real-time updates (agent state, queue counts, supervisor dashboard) stop propagating across nodes; distributed-lock acquisition fails. SignalR backplane liveness is delegated to the backplane provider's own health check, not the Contact Center checks.

**Impact.** Redis is used for two distinct things in a multi-node deployment: the SignalR backplane (`CrestApps.OrchardCore.SignalR.Redis`) and distributed locks (`OrchardCore.Redis.Lock`) that guard routing and provider-ingress critical sections. A backplane outage degrades cross-node real-time fan-out; a lock outage stops the background sweeps that require mutual exclusion.

**Response.**

1. Confirm whether the backplane, the lock service, or both are affected.
2. **Backplane only:** each node still serves its own connected clients correctly; only cross-node fan-out is degraded. This is a degradation, not an outage — routing correctness does not depend on the backplane. Restore Redis; no replay is required because real-time messages are transient.
3. **Lock service down:** the callback-promotion and reconciliation sweeps use owner tokens, fence tokens, and time-boxed leases, so an expired or unavailable lock cannot cause double work — an overlapping pass is rejected by the fence/lease, and a customer is not called back twice. Forward progress pauses until locking recovers, but no corruption occurs.
4. Restore Redis and confirm the backplane feature and `OrchardCore.Redis.Lock` are both healthy.

**Prevention.** Run Redis in HA. Never enable the backplane without `OrchardCore.Redis.Lock`; a backplane without distributed locking is an unsupported topology.

## Provider (telephony / channel) failure

**Detection.** `contactcenter-provider-ingress` reports a growing backlog; provider webhook processing lags; a provider's outbound command stream stalls. A stuck provider stream or an expired listener lease surfaces as a growing ingress backlog.

**Impact.** Inbound provider events are accepted into the durable provider webhook inbox and processed asynchronously; outbound provider commands are queued in a fenced, leased command store. A provider outage therefore does not lose events — it delays them.

**Response.**

1. Identify the failing provider and whether the problem is inbound (webhook inbox backlog) or outbound (command lease not advancing).
2. **Inbound backlog:** confirm the provider is actually delivering webhooks. Accepted-but-unprocessed messages drain automatically once the app tier recovers; the inbox is idempotent, so provider retries of the same event are de-duplicated.
3. **Outbound stall:** provider commands carry a fence token and a lease. If a node died mid-command, the lease expires and another node re-leases and retries with the same fence token, so a superseded command cannot overwrite a newer one. Check `contactcenter.outbox.dead_lettered{reason="retry_exhausted"}` for commands that exhausted retries and need manual attention.
4. Once the provider recovers, watch the ingress backlog and dead-letter counter return to baseline.

**Prevention.** Alert on the provider-ingress health state and on `dead_lettered` growth. Keep provider credentials and endpoints in configuration/secret storage so a provider failover does not require a code change.

### Asterisk single-active-process listener ownership

**Constraint.** The Asterisk real-time voice listener claims ownership of each ARI `(BaseUrl, ApplicationName)` pair in **process-local state on the node that starts it** — there is no distributed lock coordinating ownership across nodes. This is correct only under a single-active-process deployment: exactly one application node may run the listener for a given Asterisk ARI application at a time. On startup, each node logs the number of Asterisk listeners it is starting and this constraint at information level.

**Requirement.** Do not run two nodes that both start the Asterisk listener against the **same** Asterisk server and Stasis application concurrently. Overlapping nodes would each open a WebSocket to the same ARI application and cross-deliver Stasis events, double-processing calls.

**Deployment.** Because ownership is process-local, telephony listeners must use a **non-overlapping** cutover rather than a side-by-side rolling or blue-green swap:

1. Stop the old node's listener (drain, then terminate the tenant/shell so `ReleaseGeneration` runs) before the new node starts its listener against the same ARI application.
2. Only one overlapping node may enable the listener for a given `(BaseUrl, ApplicationName)` pair. Configuring a unique ARI application per tenant only prevents cross-tenant collisions **within a single process** — it does not make two nodes safe, because the same tenant configuration runs on both nodes and both would subscribe to the same application. To run overlapping application nodes safely, give each listening node a **distinct** ARI application (with matching dialplan segregation on the PBX) or front the listeners with an external single-writer mechanism.
3. A blank or misconfigured ARI application is denied at the claim path (the listener does not start) and logged as a warning, so an unconfigured provider never silently competes for events.

## Node failure

**Detection.** A node stops passing `/api/contact-center/health/ready`; the load balancer removes it; connected agents reconnect elsewhere.

**Impact.** Contact Center nodes are stateless beyond in-flight requests — all durable state is in SQL, all cross-node coordination is in Redis. A single node loss does not lose committed work: outbox messages, inbox messages, and leased commands held by the dead node time out and are re-leased by survivors.

**Response.**

1. Confirm the load balancer has evicted the node (readiness probe on `/api/contact-center/health/ready`).
2. Let leases held by the dead node expire; survivors re-acquire them via fence tokens and continue. No manual intervention is required for correctness.
3. Replace the node. New nodes pick up the backplane and lock service automatically once their features are enabled.

**Prevention.** Run at least two nodes behind a load balancer probing `/api/contact-center/health/ready` so a single failure is transparent to agents and providers.

## Network partition

**Detection.** Nodes cannot reach SQL, Redis, or providers; health checks flap; cross-node real-time updates stop.

**Impact.** A partition looks like a combination of the failures above. The system is designed to fail safe rather than double-act: fence tokens and leases prevent split-brain double execution, and the outbox/inbox prevent event loss.

**Response.**

1. Determine which dependency is partitioned (SQL, Redis, provider) and follow the matching runbook.
2. Do not force-release locks or manually replay the outbox during a partition — fencing already prevents double work, and manual replay can defeat idempotency assumptions.
3. When the partition heals, verify the outbox and provider-ingress backlogs drain and the redelivered/dead-lettered counters stabilize.

**Prevention.** Co-locate nodes, SQL, and Redis within a single region and availability-zone-redundant network. Multi-region active-active is an unsupported topology for this release.

## Rolling deployment

Contact Center supports zero-downtime rolling deployments because every shipped schema migration is additive (see the expand-migrate-contract policy in [Production support](production-support.md)).

1. Confirm the release contains only additive migrations. If a release declares a downtime requirement, use a maintenance window instead of a rolling deploy.
2. Drain and replace nodes one (or one batch) at a time. Each replaced node runs the additive migration; old nodes keep running against the expanded schema because new columns are defaulted or nullable.
3. Wait for each replaced node to report `/api/contact-center/health/ready` healthy before draining the next.
4. Leases and outbox/inbox messages held by a draining node expire and are re-acquired by the nodes that remain, so in-flight work is not lost.
5. After the last node is replaced, confirm the outbox backlog is drained and no health check is degraded.

## Blue-green deployment

1. Stand up the green environment against the **same** SQL database and Redis instance as blue, with the additive migration applied.
2. Because migrations are additive, blue keeps operating correctly while green runs the expanded schema.
3. Warm green and verify `/api/contact-center/health/ready` plus a synthetic routing and disposition flow.
4. Cut the load balancer from blue to green. In-flight leases and outbox/inbox messages are keyed in the shared database and are picked up by green.
5. Keep blue on standby until green is confirmed stable, then decommission blue. Defer any contract-phase (destructive) migration to a later release, after blue is retired and no node reads the old schema shape.
