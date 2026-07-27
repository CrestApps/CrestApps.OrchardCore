---
sidebar_label: Production support
sidebar_position: 20
title: Contact Center production support
description: Finite production support matrix, initial capacity tier, and prohibited Contact Center deployment combinations.
---

The Contact Center commercial release remains blocked until remediation phases R0 through R8 and their release evidence pass. The versioned machine-readable contract is `.github/contact-center/support-matrix.v1.json`; unlisted combinations are unsupported.

The measurable availability, latency, dependency, recovery, and ownership gates are defined in [Service objectives](service-objectives.md). Every P0/P1 production-readiness finding is tracked to a DRI, approver, test id, CI job, and retained evidence in the [PR-to-test control matrix](pr-test-control-matrix.md). Step-by-step responses for dependency and node failures and the supported deployment strategies are in the [Failure runbooks](runbooks.md).

## Initial GA-Core profiles

The first release targets two provider-specific tenant profiles. A tenant selects one profile and one voice provider; mixing both provider profiles in one tenant is unsupported.

| Profile | Provider | Included voice scope |
| --- | --- | --- |
| `ga-core-asterisk` | Asterisk | Inbound voice plus Manual and Preview dialing |
| `ga-core-dialpad` | DialPad | Inbound voice, Manual and Preview dialing, and call transfer |

The feature identifiers in the matrix describe the implemented R2 feature graph. A dedicated Linux gate now creates fresh Orchard tenants from the Blank recipe for both profiles, enables only the profile seeds plus their declared dependency closure, runs migrations, resolves key services and background tasks, disables and re-enables the idle provider adapter, and verifies Asterisk and DialPad tenants can coexist without provider leakage. The gate retains TRX evidence from `.github/workflows/contact_center_feature_activation_matrix.yml`. Commercial readiness remains blocked on later remediation and certification phases.

## Feature lifecycle contract

Feature-owned background tasks, SignalR hubs, provider listeners, provider adapters, media providers, and shell singletons have a versioned lifecycle contract in `.github/contact-center/feature-lifecycle-contracts.v1.json`. Before Orchard disables a feature, Contact Center invokes every matching lifecycle participant in two phases: quiesce all participants first, then drain. Orchard logs non-fatal feature-event exceptions and continues descriptor mutation, so a drain timeout is a bounded best-effort signal rather than a veto. Admission remains closed during teardown, and durable ownership/fencing protects work that outlives the bounded drain.

R3 adds tenant-shell admission leases for base Contact Center outbox dispatch, Dialer callbacks, Automated Dialer pacing, Voice ingress/routing/reconciliation/provider commands, Contact Center Real-Time connections, Asterisk and DialPad Contact Center provider adapters, and Asterisk Contact Center media sessions. Quiescing atomically rejects new work. Already admitted work may settle, and disable waits for its leases up to the configured timeout. Contact Center hub connections are aborted so disconnect cleanup releases their leases; open media sessions retain a lease until cleanup succeeds. Pending provider commands and claimed inbox/outbox rows remain durable and continue to use owner/fence validation rather than being redelivered blindly. A command rejected before provider contact because the provider feature is quiescing returns from `Sent` to delayed `Pending` instead of compensating business work or becoming an unknown outcome. Outbox rows persist the handler ids expected when the message was created, so temporarily disabled feature handlers cannot disappear and cause false completion or consume the poison-message dead-letter budget.

Configure the tenant drain timeout under `CrestApps_ContactCenter:FeatureLifecycle:DrainTimeoutSeconds`. The default is `30` seconds; startup validation accepts values from `1` through `300` seconds. The gate is tenant-shell-local. Multi-node correctness continues to rely on Orchard shell invalidation plus the relational command, inbox, and outbox ownership/fencing boundaries; node-crash and rolling-deployment certification remains part of R8.

## Database and topology

- PostgreSQL 16.x is the only initial production database target.
- SQLite is for local development, demonstrations, and tests only.
- The supported production topology is `single-node-distributed`: one region, exactly one application node, a shared relational database, the `CrestApps.OrchardCore.SignalR.Redis` feature, and the `OrchardCore.Redis.Lock` feature. The node count is one, but the distributed contract is mandatory rather than optional, because a single node already meets the distributed failure modes: a rolling restart overlaps two instances, and an Orchard shell reload tears down and rebuilds the shell in-process on every feature toggle.
- Multi-node operation is **not** production-supported in this release. Two to four nodes remain the architectural direction and the code path is backplane- and lock-agnostic, but multi-node capacity certification has not been earned, so `single-region-multi-node` is declared non-production in the matrix. Scaling out later is configuration and certification, not a rewrite.
- Production without the backplane or Redis distributed locking, and multi-region active-active operation, are unsupported.

### The declared topology is enforced at startup

The support matrix above is not advisory. Each tenant declares the topology it intends to run, and the tenant refuses to admit Contact Center work unless the running deployment actually satisfies that declaration:

```json
{
  "CrestApps_ContactCenter": {
    "Topology": {
      "ProfileId": "single-node-distributed"
    }
  }
}
```

- When the declared profile is `single-node-distributed`, activation verifies the tenant is on the `Postgres` database provider and that the `OrchardCore.Redis`, `OrchardCore.Redis.Lock`, and `CrestApps.OrchardCore.SignalR.Redis` features are enabled. It also verifies that the distributed lock the container actually resolves is not the process-local implementation, because a feature can be enabled while the container still hands out the local lock, and the lock that is injected is the one that decides whether two overlapping processes can enter the same critical section.
- Every unmet requirement is reported at once. Fixing one requirement per deployment would make each intermediate deployment another unsupported production release.
- An unrecognized profile identifier is a validation failure rather than a fallback to the development profile, so a typo cannot silently downgrade a production deployment to the profile that requires nothing.
- Omitting `ProfileId` is normal for development, tests, and demonstrations and imposes no requirements. It is a validation failure when the host environment is `Production`, because otherwise the entire check could be bypassed by setting nothing.
- Declaring a non-production profile such as `single-node-development` imposes no infrastructure requirements. It is a statement that the deployment is not claiming production support.

A tenant that does not satisfy its declared topology logs a critical message naming each unmet requirement, refuses every Contact Center work admission, and reports **unready** on the tenant readiness probe. Validation runs once per shell activation, because every input to the verdict — declared profile, database provider, enabled features, resolved lock — can only change by rebuilding the shell. Until the verdict is recorded the tenant is treated as inadmissible; starting admissible and tightening afterwards would open a window in which an unverified deployment accepts work, and that window is exactly when a shell reload is in progress.

The topology profiles the product enforces are a shipped mirror of the governance matrix in `.github/contact-center/support-matrix.v1.json`, which is not deployed with the product. A contract test asserts the two are identical, so the running application cannot enforce a second, more permissive definition of what "production" means.

Queue and reservation correctness does not depend on Redis lock exclusivity. YesSql document versions provide compare-and-set updates, and portable unique claim keys enforce active queue-item and reservation ownership in the relational database. Upgrade migrations reject missing identifiers or duplicate legacy active claims with explicit repair guidance instead of failing later with an opaque unique-index error. SQLite regression tests force overlapping lock holders and synchronized stale reads and retain exactly one reservation; production certification still requires the planned database matrix to repeat the invariant on PostgreSQL and any subsequently supported database.

### Database portability

Contact Center persistence is engine-portable by construction, so the same migrations and queries run unchanged on every YesSql-supported relational engine (SQLite, SQL Server, PostgreSQL, and MySQL):

- **Enumerations are stored as their string names, never as ordinals**, so reordering or inserting an enum value never silently remaps existing rows, and status filters read the same on every engine.
- **Every raw-SQL migration quotes all identifiers through the active `ISqlDialect`** (`QuoteForTableName`, `QuoteForColumnName`, `FormatIndexName`) and honors `PrefixIndex`; no migration hardcodes an engine-specific quote character, table prefix, or index-naming rule. Unique-index creation is centralized in the single dialect-aware `ContactCenterMigrationSql` helper, and a unit test pins that the generated `CREATE UNIQUE INDEX` statement is produced entirely from the dialect.
- **All literal values in backfill and preflight statements are passed as bound command parameters**, never string-concatenated, so they are engine-quoting- and injection-safe.
- **Backfill and duplicate-detection statements use only ANSI SQL** (`UPDATE`/`CASE`/`IN`/`GROUP BY`/`HAVING`/`COUNT`) that every supported engine implements identically.
- **Case-insensitive matching is normalized in application code** (for example, queue membership keys are lower-cased before they are stored and queried) rather than relying on a database's default collation, so routing behaves the same regardless of the engine's collation configuration.

Because no local environment can host every engine, per-engine validation of the full migration and query surface is a CI and deployment-certification responsibility; the guarantees above keep that validation a verification step rather than a porting exercise.

Provider stream correctness uses the supported Redis distributed-lock topology. Every canonical provider-call stream is serialized before interaction, call-session, event-log, and outbox changes are read or written, and the YesSql transaction commits before the lock is released. This makes duplicate Asterisk listeners and concurrent DialPad delivery processing harmless across supported nodes without requiring a renewable long-lived socket lease. Lifecycle rank cannot move backward, an established provider sequence high-water cannot be advanced by an unsequenced event, and terminal state remains final.

PBX mutations use a tenant-scoped server execution boundary instead of the SignalR connection or HTTP request cancellation token. The default 10-second command deadline is configurable with `CrestApps_Telephony:Commands:Timeout` and accepts values from one second through two minutes. Deadline or host-shutdown cancellation produces an unknown provider outcome rather than a safe-to-retry success or failure. Durable provider commands persist that ambiguity as `OutcomeUnknown`; synchronous Telephony operations return an unknown result. After the provider confirms success, local interaction, transfer, recording, monitoring, and event persistence uses a non-request, non-expiring token so a browser disconnect or exhausted provider deadline cannot discard the confirmed projection. This outer command deadline intentionally supersedes longer provider-specific retry budgets.

## Observability and health

The Contact Center module exposes a stable OpenTelemetry contract and operational health checks so operators can wire dashboards, alerts, and orchestrator probes without depending on private types.

### Telemetry contract

`ContactCenterDiagnostics` publishes a single `Meter` and a single `ActivitySource`, both named `CrestApps.OrchardCore.ContactCenter`. These names are a public integration surface and change only through a documented migration. Register them with any OpenTelemetry exporter:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("CrestApps.OrchardCore.ContactCenter"))
    .WithTracing(tracing => tracing.AddSource("CrestApps.OrchardCore.ContactCenter"));
```

Current instruments:

| Instrument | Kind | Meaning |
| --- | --- | --- |
| `contactcenter.outbox.redelivered` | Counter | Domain events successfully redelivered from the durable outbox. |
| `contactcenter.outbox.dead_lettered` | Counter | Domain events dead-lettered after exhausting their retry budget, tagged by `reason`. |

### Health checks

Three probes answer three different questions. They are separate on purpose, and the separation is a reliability requirement rather than a stylistic one.

| Route | Scope | Contract | Selects | Wire to |
| --- | --- | --- | --- | --- |
| `/health/process` | Host | Liveness. Returns `Healthy` whenever the process can serve the request. It consults nothing, by design. | Nothing. | The orchestrator's liveness probe / restart policy. |
| `/api/contact-center/health/ready` | Tenant | Readiness. Reports whether *this node* should receive traffic for this tenant. | Checks tagged `contactcenter-ready`. | The load balancer and the orchestrator's readiness probe. |
| `/api/contact-center/health/dependencies` | Tenant | Dependency report. Per-check status for the tenant's dependencies. | Checks tagged `contactcenter-dependency`. | Dashboards and alerting. **Never** an orchestrator probe. |

The liveness and readiness routes allow anonymous requests so an orchestrator can reach them, and both return only the aggregate status word — never per-check detail — so they disclose nothing useful to an unauthenticated caller. The dependency route discloses which dependency degraded, so it requires the `MonitorContactCenter` permission.

#### Why liveness is served by the host, not a tenant

Liveness answers exactly one question: *should this process be restarted*. No tenant-scoped route can answer it. A route mapped inside a tenant shell returns **404** whenever that tenant is disabled, renamed, given a different request URL prefix, or fails to start — and an orchestrator reads 404 as a probe failure. A healthy process would be restarted forever because of a tenant-level problem, and the restart would not fix the tenant.

`/health/process` is therefore answered by host middleware installed ahead of the Orchard Core pipeline:

```csharp
builder.Services
    .AddContactCenterProcessLiveness()
    .AddOrchardCms();

var app = builder.Build();

app.UseContactCenterProcessLiveness();

app.UseOrchardCore();
```

It must be registered before `UseOrchardCore`, and it must be middleware rather than a mapped endpoint: endpoints registered on a `WebApplication` are executed by terminal middleware appended *after* everything the application added, so an endpoint would be evaluated only after Orchard Core had already handled the request. Only `GET` and `HEAD` are answered; every other verb falls through to the normal pipeline.

:::warning
The path is `/health/process`, not `/health/live`, and that is deliberate. `/health/live` is the default route of the `OrchardCore.HealthChecks` module. Because this middleware short-circuits *before routing*, taking that path would silently shadow that module's endpoint for every tenant in the process — including tenants that never enable Contact Center — and answer an unconditional `200 Healthy` in its place. A health endpoint that can only ever report success is worse than none at all. `AddContactCenterProcessLiveness` also registers a startup validator that reads **every configured tenant's** health-check route — tenant configuration is not host configuration, so a tenant could otherwise claim the path unseen — and fails the host with an explanatory error naming the tenant. A tenant created *after* startup is not covered by that validator; tenants that enable Contact Center are covered by the shell-level guard, and for tenants that do not, treat the liveness path as reserved.

To move the probe, pass the path to `AddContactCenterProcessLiveness("/probe/alive")`. `UseContactCenterProcessLiveness` deliberately takes no path: the path is validated against every tenant at registration, so accepting it again at the pipeline would let the probe answer on a path no tenant was ever checked against — the exact collision the validator exists to prevent. Registering it twice on different paths fails startup for the same reason.
:::

The readiness tag is namespaced (`contactcenter-ready`, not `ready`) so that a check contributed by another module — which conventionally uses the bare `ready` tag — can never silently join the Contact Center readiness verdict.

#### Why readiness does not check dependencies

This is the single most important thing to understand about these probes, and it is the opposite of what most health check examples show.

Readiness removes a node from the load balancer. A dependency shared by every node — the database, Redis, the provider, the outbox backlog — is observed identically by every node. If readiness consulted it, every node would fail readiness at the same instant, the load balancer would have no healthy target, and a *degraded* dependency would become a *total* outage. The system would take itself down in response to a problem it could otherwise have served through, and it would stay down because no node can pass a probe that depends on something still broken.

Readiness must therefore only reflect conditions that genuinely differ between nodes. Two apply on every deployment:

- The node has not finished starting. During a rolling deployment a new instance must not receive calls before its shells are initialized.
- The node is shutting down. Reporting unready on `SIGTERM` is what lets the load balancer evict the node *before* the process stops accepting connections. Without it, every deployment drops in-flight calls.

A third, the [node serving gate](#optional-the-node-serving-gate), is available where a node's *own* ability to reach a shared dependency can fail independently of its peers. It is opt-in because it re-introduces fleet-wide risk when the dependency itself is down.

Dependency health is still observed — that is what the dependency probe and the metrics are for — but it is an **alerting** signal that pages a human, never a **routing** signal that drains capacity.

The topology check is the one deliberate exception to the "readiness must differ between nodes" rule, and the distinction is between a *live dependency* and a *static verdict*. A dependency probe is transient and self-healing, so draining every node on it turns a recoverable blip into a total outage. A topology violation is fixed configuration that no amount of waiting repairs, there is no degraded-but-serviceable state to preserve, and continuing to serve on an uncertified deployment is precisely the failure being prevented. Draining is the intended outcome, not collateral damage. The narrower invariant still holds without exception: readiness never consults a dependency check.

:::tip
The rule generalizes: an orchestrator probe may only consult state that differs between instances, or a static support verdict that cannot self-heal. If two healthy instances would always answer identically *and* the condition can recover on its own, the check belongs on the dependency probe.
:::

#### The tenant probes inherit the tenant prefix

The readiness and dependency routes are mapped inside the Contact Center feature, so they exist on every tenant that enables it and they inherit that tenant's request URL prefix. A tenant reachable at `/support` answers on `/support/api/contact-center/health/ready`; a tenant with no prefix answers on `/api/contact-center/health/ready`.

:::warning
Probing an unprefixed tenant route when Contact Center runs on a prefixed tenant reaches a shell that does not map these routes, and returns **404**. An orchestrator treats 404 as a probe failure. Always include the tenant prefix, and verify the probe returns 200 before relying on it. `/health/process` is the only probe that is prefix-independent, because it is served by the host.
:::

With the default configuration readiness is node-local, so probing a single Contact Center tenant is sufficient and correct — every tenant on the node reports the same node state. This is what makes a single Kubernetes `readinessProbe` correct even when the pod hosts several tenants. If you enable the node serving gate below, readiness becomes genuinely per tenant and each tenant you care about must be probed.

Scrape the **dependency** probe per tenant, since dependency health genuinely is per tenant.

#### Optional: the node serving gate

Readiness being purely lifetime-based is deliberately shallow, and that shallowness has a cost. "Shared dependency" does not mean "fails identically on every node": a node with an exhausted connection pool, a stale DNS entry, an expired TLS trust store, or exhausted outbound ports fails every store call while its peers are perfectly healthy. Nothing in the default readiness contract notices, so that node keeps taking its share of traffic and failing all of it.

The node serving gate closes that hole. It runs a cheap store probe on readiness and drains the node after `ConsecutiveFailuresBeforeUnready` consecutive failures, returning it after `ConsecutiveSuccessesBeforeReady` consecutive successes. The hysteresis is what makes it usable: a single transient failure never costs capacity, and a node does not flap back into rotation on one lucky call.

It is **disabled by default**, because when the store itself is down every node observes the same failure and the gate drains the whole fleet — the exact failure mode the readiness split exists to prevent.

:::warning
Enable it only when your load balancer fails open once too few targets remain healthy — for example an Envoy or Istio panic threshold, which by default routes to all hosts when fewer than 50% are healthy. On a plain Kubernetes `Service`, all-pods-NotReady means zero endpoints and a total outage. If you are unsure, leave it disabled and rely on the dependency probe and alerting instead.
:::

```json
{
  "CrestApps_ContactCenter": {
    "HealthChecks": {
      "EnableNodeServingGate": true,
      "ConsecutiveFailuresBeforeUnready": 3,
      "ConsecutiveSuccessesBeforeReady": 2
    }
  }
}
```

When disabled the check performs no I/O and reports healthy immediately, so readiness stays free.

#### Which checks a tenant registers

The dependency report contains only what the tenant's enabled features registered. `contactcenter-provider-ingress` needs the provider webhook inbox, which the Voice feature owns, so it is registered by the Voice feature and is absent on tenants that do not enable Voice. Do not assert a fixed check count in monitoring.

| Check | Probe | Registered by | Signal | Degraded | Unhealthy |
| --- | --- | --- | --- | --- | --- |
| `contactcenter-topology` | Readiness | Contact Center | Whether this deployment satisfies the [topology it declared](#the-declared-topology-is-enforced-at-startup). A static verdict established once per shell activation; performs no I/O. | — | Validation has not run yet, or a declared requirement is unmet. |
| `contactcenter-node` | Readiness | Contact Center | Node-local lifetime: startup complete and not shutting down. Consults no dependency and performs no I/O. | — | Startup incomplete, or shutdown in progress. |
| `contactcenter-node-serving` | Readiness | Contact Center | Opt-in node serving gate (see above). Disabled by default, in which case it performs no I/O and is always healthy. | — | Enabled, and this node failed `ConsecutiveFailuresBeforeUnready` consecutive store probes. |
| `contactcenter-storage` | Dependency | Contact Center | A cheap store query proving the tenant database and Contact Center collection are reachable. | — | Query throws. |
| `contactcenter-outbox` | Dependency | Contact Center | Dead-lettered count and overdue (past-due pending/claimed) backlog. The overdue backlog is the scheduler-lag signal: a sustained non-zero value means the dispatch background task is not keeping up. | Dead-letters or overdue backlog reach the degraded threshold. | Either reaches the unhealthy threshold, or the store is unreadable. |
| `contactcenter-provider-ingress` | Dependency | Contact Center Voice | Provider webhook inbox dead-letter and overdue backlog. A stuck provider stream or an expired listener lease surfaces here as a growing ingress backlog. | Same thresholds as the outbox. | Same thresholds as the outbox. |

With the serving gate disabled, readiness performs no I/O at all and is safe to scrape at orchestrator frequency; with it enabled, readiness costs one store query per scrape. The dependency probe runs two queries for each queue check plus one for storage — five queries on a Voice-enabled tenant — so scrape it on the order of seconds, not milliseconds.

:::warning
Do not point a liveness or readiness probe at the `OrchardCore.HealthChecks` module's endpoint. That module maps a single route with no registration predicate, so it aggregates every check contributed by every enabled module regardless of tag — including the dependency checks. Despite its default `/health/live` route it is neither a liveness nor a readiness signal, and wiring a probe to it reintroduces exactly the fleet-wide-drain and restart-loop failures described above.

Because documentation cannot stop a deployment chart from wiring that route, the Contact Center module fails tenant startup when `OrchardCore.HealthChecks` is enabled and its route still claims liveness. Resolve it by moving the shared endpoint off a liveness name:

```json
{
  "OrchardCore_HealthChecks": {
    "Url": "/health/aggregate"
  }
}
```

If you have deliberately accepted the aggregate-on-liveness-route behavior, acknowledge it explicitly instead:

```json
{
  "CrestApps_ContactCenter": {
    "HealthChecks": {
      "AllowUnsafeSharedEndpointRoute": true
    }
  }
}
```
:::

Thresholds are configured under `CrestApps_ContactCenter:HealthChecks` and are normalized so an unhealthy bound can never fall below its degraded bound:

```json
{
  "CrestApps_ContactCenter": {
    "HealthChecks": {
      "DeadLetterDegradedThreshold": 1,
      "DeadLetterUnhealthyThreshold": 25,
      "OverdueBacklogDegradedThreshold": 50,
      "OverdueBacklogUnhealthyThreshold": 500
    }
  }
}
```

SignalR backplane health is owned by the backplane provider rather than the Contact Center module. When a Redis backplane is configured, register the Redis/backplane connectivity check with the `contactcenter-dependency` tag so it appears on the dependency probe and can be alerted on.

:::danger
Never tag a Redis, database, provider, or backplane connectivity check `contactcenter-ready`. Redis is shared by every node, so every node would fail readiness at the same instant, the load balancer would be left with no target, and a degraded backplane would become a total outage that cannot self-heal. Shared dependencies are alerting signals, not routing signals.
:::

On a single node the in-memory backplane needs no separate check.

## Multi-node real-time backplane

The Contact Center real-time hub is backplane-agnostic. It is hosted through `HubRouteManager.MapHub<ContactCenterHub>` and addresses connections through tenant-qualified `TenantSignalRGroupName` groups, so the same code path serves both single-node and multi-node deployments without change. What makes it correct across nodes is the shared backplane, not the hub.

The supported production real-time topology is:

- Enable `CrestApps.OrchardCore.SignalR.Redis` on every tenant that must exchange real-time messages. It wires the SignalR Redis backplane (`AddStackExchangeRedis`) using the `OrchardCore_Redis` connection settings and a dedicated SignalR connection, and it namespaces the backplane channel with both `InstancePrefix` and the immutable shell name so two nodes serving one tenant share a channel while different tenants never do. See [SignalR module — Redis backplane](../modules/signalr.md#redis-backplane) for configuration.
- Enable `OrchardCore.Redis.Lock` as well. The SignalR backplane distributes real-time messages, but Contact Center routing, provider webhook inbox acceptance, and other distributed critical sections require the Redis distributed lock independently of the backplane. A backplane without distributed locking is an unsupported configuration.
- Use a deployment-unique `InstancePrefix` (application, environment, region) whenever Redis infrastructure is shared, so tenants with the same shell name in different deployments cannot merge backplane channels.

Both features are required in production even on the single supported node. The in-memory backplane and local lock are development-only: a rolling restart overlaps two instances, and an Orchard shell reload rebuilds the shell in-process, so process-local state is not a safe substitute for the distributed contract. Production without the backplane, without Redis distributed locking, on more than one node, or in a multi-region active-active configuration is unsupported.

## Retention, legal holds, and replay horizon

The durable interaction event log is the source of truth from which projections (for example the daily metrics projection) are rebuilt. Purging it therefore bounds how far back a projection can be replayed, so retention is aligned with the replay horizon and legal holds rather than deleting events purely by age.

Retention is configured under `CrestApps_ContactCenter:Retention`:

| Setting | Meaning |
| --- | --- |
| `InteractionEventRetentionDays` | Days to retain interaction events before purging. `0` disables purging entirely (keep indefinitely). |
| `ProjectionReplayHorizonDays` | Minimum days the event log must remain rebuildable. Retention never purges events younger than this, guaranteeing projections can be rebuilt for at least this window. |
| `LegalHoldMinimumDays` | Legal-hold / regulatory floor. Events are never purged below this age regardless of the configured window. |

Both floors can only make retention more conservative: the effective purge cutoff keeps events for `max(InteractionEventRetentionDays, ProjectionReplayHorizonDays, LegalHoldMinimumDays)` days, so raising a floor extends retention and never causes an earlier purge. Purging stays disabled whenever `InteractionEventRetentionDays` is `0`.

Behavior guarantees:

- **Retained snapshot** — the daily metrics projection is a durable aggregate that survives event purge, so reporting figures remain available after the raw events are gone.
- **Post-purge rebuild** — after a purge, a projection rebuild (`RebuildAsync`) recomputes counts only from the events that remain; the replay-horizon floor guarantees that window is at least `ProjectionReplayHorizonDays`.
- **Legal hold** — set `LegalHoldMinimumDays` above the retention window to hold events for a case or regulatory obligation without changing the operational retention setting.

## Per-entity data governance

Every persisted Contact Center data category is classified in code by `ContactCenterDataGovernanceCatalog`, the single source of truth this table renders. Each category declares its privacy sensitivity, whether it references call recordings, what governs its retention, and how an erasure (right-to-be-forgotten) request is satisfied. The catalog is unit-tested for integrity — keys are unique, personal categories always declare a concrete erasure strategy, non-personal categories never anonymize, and any recording-bearing category is always classified as personal — so a new persisted entity cannot ship without an explicit classification.

| Data category | Sensitivity | Recording ref | Retention basis | Erasure |
| --- | --- | --- | --- | --- |
| Interaction event log | Personal | No | `InteractionEventRetentionDays`, floored by replay-horizon and legal-hold | Retention expiry |
| Interaction | Sensitive personal | Yes | Life of the interaction record | Anonymize (+ external recording erasure) |
| Call session | Sensitive personal | Yes | Life of the call-session record | Anonymize (+ external recording erasure) |
| Callback request | Personal | No | Until promoted or expired | Anonymize |
| Agent session | Personal | No | Adherence/staffing reporting window | Anonymize |
| Agent profile | Personal | No | Agent account lifecycle | Anonymize |
| Event outbox message | Personal | No | Short-lived; deleted on dispatch | Retention expiry |
| Provider webhook inbox message | Personal | No | Short-lived; deleted on processing | Retention expiry |
| Provider command | Non-personal | No | Short-lived; deleted on completion | Retention expiry |
| Queue item | Non-personal | No | Transient; removed when work leaves the queue | Cascade with interaction |
| Activity reservation | Non-personal | No | Transient; removed on accept/decline/expiry | Retention expiry |
| Event metric | Non-personal | No | Durable aggregate snapshot | Not applicable |
| Projection checkpoint | Non-personal | No | Operational; updated in place | Not applicable |
| Processed-event ledger | Non-personal | No | Idempotency window | Retention expiry |
| Routing and dialing configuration | Non-personal | No | Administrator-managed | Not applicable |

**Erasure strategies.** *Retention expiry* removes the record automatically when it ages past its window (no per-subject action). *Anonymize* clears the personal fields — the customer/caller addresses and free-text notes — while keeping the record so aggregate metrics and audit history survive. *Cascade with interaction* erases the record together with its parent interaction. *External store* delegates erasure to the system that holds the payload. *Not applicable* means the category holds no personal data.

**Call recordings.** Recordings are never stored inside Contact Center. The `Interaction` and `CallSession` entities hold only a `RecordingReference` (an opaque pointer) and a `RecordingState`; the media itself lives in the telephony provider or a configured media store. Consequently:

- **Access audit** — recording playback and download must be brokered by, and audited in, the system that holds the media. Contact Center exposes the reference under the same permission and content-access-control checks as the owning interaction; every access decision is logged through the operational log with the identifier taxonomy (recordings are treated as sensitive personal data). Wiring a specific media store's access log is a deployment integration.
- **Recording erasure** — anonymizing an interaction or call session clears the personal fields it holds and issues a delegated erasure request to the external store for the referenced media; Contact Center does not assume it can delete provider-held media directly.

**Backup and restore.** All durable Contact Center state lives in the tenant SQL database (see the [failure runbooks](runbooks.md)); back it up with the engine's native, point-in-time-capable mechanism. Because the interaction event log is the projection-rebuild source, keep `ProjectionReplayHorizonDays` and `LegalHoldMinimumDays` set so a point-in-time restore retains enough history to rebuild projections — after a restore, run the metrics projection rebuild to reconcile any drift. Provider-held recordings are backed up by their owning store, not by the Contact Center database backup, so a full restore must coordinate the database restore with the media store's own retention and restore policy.

## Upgrade and migration safety

Contact Center follows an expand → migrate → contract policy so a rolling or blue-green deployment never runs an old and a new node against a schema either cannot use:

- **Expand** — a release only adds schema. New columns are additive and ship with a default (or are nullable), so an old node keeps writing valid rows while the new node populates the new column.
- **Migrate** — backfill and any new unique constraint run inside the upgrade migration against the module's own index tables. Unique-constraint creation is preceded by a portable preflight that detects pre-existing duplicate active claims and fails with explicit repair guidance instead of silently corrupting data or throwing an opaque unique-index error later.
- **Contract** — destructive changes (dropping or renaming a column or table, narrowing a type, or removing a default) are deferred to a later release, after every node is known to no longer read the old shape.

Audit of the shipped Contact Center migrations: every migration is additive — `CreateMapIndexTable`, `AddColumn` with a default or nullable value, and guarded `CreateIndex`/`CreateUniqueIndex`. There are no `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, or `AlterColumn` operations, so no shipped upgrade requires downtime. Any future backward-incompatible change must either be restructured into the expand/migrate/contract phases above or explicitly declare a downtime requirement in its release notes.

**How the contract phase is enforced.** The policy above is a build gate, not a convention. Every Orchard data migration in the repository is parsed, and three oracles look for a destructive step: schema-builder calls such as `DropColumn`, `DropIndex`, `DropTable`, `RenameColumn`, `RenameTable`, and `AlterColumn`; raw SQL passed as an argument to any synchronous or asynchronous execution method; and raw SQL assigned to a command's text. The raw-SQL oracles reconstruct the statement across string concatenation, interpolation, single-assignment locals, and read-only query-builder composition, then classify it, so a statement built at runtime is judged on what it does rather than on whether a literal happens to match. Classification does not stop at the leading verb, because a destructive statement need not lead: a common table expression begins with `with` and a batch can hide a second statement after a semicolon, so a destructive verb anywhere in the statement is a finding. Quoted values are removed before that scan so a literal that merely reads like a verb is not mistaken for one, and a statement that can execute another statement — `EXEC`, `sp_executesql`, or a procedural `DO`/`BEGIN` block, wherever it appears — is treated as unreadable rather than as safe, because the gate can see the wrapper but not what it runs. A statement the gate cannot read is itself a finding: it must either be written so the verb is visible or be recorded, per call site, with what it does and why it cannot be destructive. Such a recorded approval is pinned to a fingerprint of the type that declares it, so changing what the statement builds invalidates the approval and forces a fresh review.

Every destructive step needs a register entry that authorizes one operation against one named object, and an entry that matches no step or several steps fails, so an authorization cannot go stale or quietly widen. Justifications are checked rather than trusted: a contract-phase removal must name a strictly older release as the one that introduced the object, which makes expand and contract landing in the same release impossible; and a claim that an object never reached a customer must name the database object it is about, which is then searched for in the source of every stable release tag. The claim fails if the object is present in any released tree, if it cannot be bound to the object the entry authorizes, or if the released source cannot be read. The claim also has to be bound to the object the entry actually operates on. A schema operation names its object directly, so the claim must equal it. Raw SQL is read at the operand position — the identifier that follows `drop table`, `alter table`, `delete from`, and the like — rather than anywhere in the statement, so an object named in a trailing comment cannot stand in for the one being dropped. Reconstruction is what makes that position readable: it resolves constants, interpolation holes, table quoting, schema qualification, and index-table naming conventions. Every operand in the statement must be the claimed object, not merely the first, so a batch that drops an authorized table and then a second, unauthorized one is rejected instead of being covered by a single claim. An operand the gate cannot read is a finding rather than a pass. Without that binding, changing the constant that names the dropped table would leave the statement classification, the authorization, and the claim all unchanged while dropping something else entirely. Checking the claim against the shipped source is what turns it into evidence: a version number, or a commit the author chooses, is an assertion the gate cannot verify. `UninstallAsync` is exempt, and only `UninstallAsync`, because feature uninstall is not an upgrade path.

The gate's scope is Orchard data migrations. Destructive DDL executed from a background task, a recipe, a feature event handler, or an ordinary service is outside it, and a prerelease-to-prerelease upgrade is outside it as well: the never-released justification is evaluated against stable releases only, because upgrading from a preview or release candidate is not a supported path.

## Tier-1 capacity target

R8 must prove the entire envelope rather than extrapolating from a smaller test:

| Limit | Per tenant | Per deployment |
| --- | ---: | ---: |
| Concurrent signed-in agents | 100 | 250 |
| Concurrent voice interactions | 50 | 100 |
| New interactions per second | 10 | N/A |
| Tenants | N/A | 5 |

These are acceptance ceilings for the first certified tier, not architectural maximums. Higher tiers require separate load, soak, failure, and dependency-limit evidence.

## Distributed harness dependency ledger

R0 records the distributed evidence that cannot be produced honestly by an in-process unit fixture in `.github/contact-center/r0b-harness-dependency-ledger.v1.json`. The ledger does not certify any scenario. It prevents later phases from silently replacing production topology proof with mocks or single-process approximations.

| Scenario | Implementation phase | Certification phase |
| --- | --- | --- |
| Redis backplane with two Orchard shells | R1 | R8 |
| Duplicate/reordered provider stream across two processes | R3 | R4 |
| Provider-listener lease loss and ownership transfer | Alternative not used; duplicate listeners are safe | R4 |
| Application-node failure during active work | R3 | R8 |
| Redis network partition | R7 | R8 |
| Database network partition | R7 | R8 |
| N/N-1 rolling-version deployment | R7 | R8 |

Each ledger entry resolves the applicable control-matrix ids, current unit evidence, concrete blockers, required infrastructure, and retained evidence directory. R2 builds the minimum two-process Orchard harness; the owning remediation phase adds the missing production behavior; R8 runs the complete release certification.

## Provider webhook ingress

Inbound provider webhooks are split by channel by design.

- **Voice provider webhooks** (generic Contact Center and DialPad) use the full ingress-control stack: body/header limits, tenant-local rate and concurrency limiting, delivery freshness and replay rejection, and a durable at-least-once inbox that returns `2xx` only after the delivery is committed. Processing is decoupled from the request lifecycle, so a client disconnect after commit never drops or double-executes a delivery.
- **Non-voice provider webhooks** (Twilio SMS, Twilio EventGrid, and Azure EventGrid) are authenticated at the edge — Twilio requests are verified against the account `AuthToken` HMAC signature and rejected with `403` on mismatch; Azure EventGrid requests are authenticated and bounded by a request-body cap — but they do not yet use the durable inbox. They are outside the GA-Core voice scope.

Bringing the non-voice webhooks to full parity is a tracked R9 item. Because the durable inbox is intentionally coupled to Contact Center orchestration (its scope executor, provider-identity canonicalization, and persisted inbox index), parity is delivered by first promoting the reusable ingress primitives to a channel-neutral shared home at or below Omnichannel, then migrating both voice and non-voice consumers onto it — an expand-migrate-contract refactor sequenced only when a second (non-voice) channel is actually built.

## Prohibited capabilities and combinations

- Power, Progressive, and Predictive dialing.
- Recording, monitor, whisper, barge, take-over, and bidirectional media.
- More than one voice provider profile in one tenant.
- Production on SQLite.
- Production on a single application node without Redis distributed locking and a Redis SignalR backplane.
- Production on more than one application node, until multi-node capacity certification is earned.
- Elasticsearch in routing, assignment, provider ingest, or another correctness path.
- Any feature, provider, database, or topology combination not listed in the versioned matrix.

Unsupported controls are hidden and rejected server-side. Supervisor engagement modes are returned to the dashboard only when the active provider advertises the mode and implements the executable monitoring contract; recording and Contact Center transfer likewise fail closed without their executable contracts. Provider failure or an unknown outcome never writes successful recording, monitoring, or transfer state. Telephony soft-phone commands also repeat capability enforcement on the server. Enabling an implementation that has not passed the profile's release gates does not make that capability supported.

Bidirectional media is excluded more strongly: the legacy capability flag has been removed, the Contact Center and Asterisk media features are dependency-only and hidden from direct feature selection, and neither GA-Core tenant profile enables the media resolver or a media provider. The Asterisk RTP/UDP implementation remains development-only until R9 certifies a secure private-network boundary, packet loss/reordering/jitter behavior, capacity, failover, and node affinity.

Search independence is enforced rather than asserted, and it is enforced against all three mechanisms that can introduce a search dependency. A build gate reads the PE metadata of every shipped Contact Center, Telephony, Asterisk, and DialPad assembly and walks the transitive reference closure, failing if it reaches an Elasticsearch or OpenSearch client, so no supported deployment can be made to require a search cluster. A direct-reference gate additionally rejects any search or indexing API referenced by those assemblies themselves, so a correctness path cannot be written against search in the first place. Because an Orchard feature dependency is a string in a manifest and creates a runtime dependency with no assembly reference at all, a third gate rejects any Contact Center feature that declares a dependency on a search-backed feature. A fourth starts each supported profile in a real tenant and fails if the resulting enabled-feature set contains a search engine. A fifth executes the correctness paths themselves — routing selection, assignment through a real reservation, outbox dispatch, and provider ingest through the normalized voice-event seam every PBX adapter funnels into — against persisted state inside a real supported-profile tenant, and fails if any outbound HTTP request is issued, which catches a regression that reached a cluster through an ordinary HTTP client without referencing a search assembly or enabling a search feature. Search-backed capability belongs in a separate opt-in module that a supported topology leaves disabled.
