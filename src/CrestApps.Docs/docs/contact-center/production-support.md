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
- **Every identifier in a hand-written statement is quoted through `SchemaBuilder.Dialect`**, never with a literal quote character. A hardcoded double quote delimits an identifier on some engines and a string literal on others, so a filter that quotes its own column name becomes a comparison between a constant and a set of values on the engines that read it as a literal: it matches nothing, succeeds, and leaves the rows it was meant to touch untouched with nothing to indicate it did nothing. A gate fails the build when a migration that writes raw SQL contains one.
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

## Configuration validation

Every operator-supplied option in the Contact Center, Telephony and provider modules is validated, and an invalid value stops the tenant instead of being discovered later by whichever code path happens to read it first.

Validation runs when the tenant activates, which is what the first request to that tenant triggers. Declaring `ValidateOnStart()` alone is not sufficient here: it records its rules against `IStartupValidator`, which the .NET generic host invokes only against the root container, while Orchard Core builds a service container per tenant. `ValidateTenantOptionsOnActivation()` closes that gap by invoking the validator during tenant activation, so a tenant carrying an invalid configuration fails to activate and never serves a request. A misconfigured tenant does not degrade other tenants on the same host.

Validated settings:

| Section | Rule |
| --- | --- |
| `CrestApps_ContactCenter:Retention` | Every window and floor is non-negative, and so are the purge batch size and the per-entity batch budget, where zero means "use the default". |
| `CrestApps_ContactCenter:HealthChecks` | Every threshold is at least one, and each unhealthy bound is at or above its degraded bound. |
| `CrestApps_ContactCenter:Topology` | A declared profile identifier resolves to a known topology profile, so a typo is refused rather than silently falling back to a weaker topology. |
| `CrestApps_ContactCenter:Coordination` | Lock waits are positive and each lease expiry exceeds its acquisition timeout. |
| `CrestApps_Telephony:Commands` | The command timeout is between one second and two minutes. |
| `CrestApps_Telephony:Coordination` | Lock waits and the new-interaction grace period are positive, and the lease expiry exceeds its acquisition timeout. |
| `CrestApps:Asterisk:Default` | Numeric settings are sane whenever the configuration-backed provider is enabled. |
| `CrestApps:Asterisk:Coordination` | Lock and HTTP timings are positive, the lease expiry exceeds its acquisition timeout, and the total request budget exceeds a single attempt. |

Each lease expiry must exceed its acquisition timeout because otherwise the lease can lapse while a peer is still waiting to take it, and two nodes then act on the same call, credential or reconciliation sweep at once.

### Development credentials are refused in production

This repository publishes working credentials so the Aspire stack runs without an operator inventing their own — including the Coturn static authentication secret and the Asterisk ARI password. Outside a development environment those values authenticate nobody, because anyone who has read this repository holds them.

When `ASPNETCORE_ENVIRONMENT` is `Production`, a tenant refuses to activate if `CrestApps:Asterisk:Default` supplies a known development value for `Password`, `TurnSharedSecret`, `UserName` or `PjsipRealtimeConnectionString`. The same values keep working in development, so the workflow they were published for is unaffected. Obvious placeholders that were never replaced, such as `changeme` or `<your-secret>`, are refused on the same terms.

The register of known development values stores SHA-256 digests rather than the secrets themselves, and a test walks the tracked configuration files in this repository to assert that every credential they publish is recognised — so adding a new sample credential without registering it fails the build rather than shipping an unguarded value.

Where a credential is only used to derive a client-visible artifact, the guard degrades rather than fails the tenant. A TURN relay credential derived from a published shared secret is withheld in production and the soft phone receives STUN-only ICE servers, which reduces connectivity through restrictive networks but never hands out a forgeable relay credential.

### Timings are configuration, not constants

Distributed-lock waits, lease expiries, the inbound reclamation threshold and the Asterisk HTTP request budget are deployment characteristics: a node under heavier load, or one further from its database or from Asterisk, needs different values than a developer laptop. They are settable under the `Coordination` sections above and validated on the same terms as everything else.

## Retention, legal holds, and replay horizon

Every table that grows with traffic is aged out by a retention policy. A table without one is not a small oversight: it is the table that eventually fills the disk, and it is invisible until it does. Retention therefore covers the whole database rather than the event log alone, and a table that is deliberately *not* aged out has to say so.

Each entity declares a policy that answers three questions: which timestamp the record is aged from, what makes the record finished, and which floors hold it beyond its configured window.

Retention is configured under `CrestApps_ContactCenter:Retention`. Every window defaults to `0`, which means "keep indefinitely":

| Setting | Entity |
| --- | --- |
| `InteractionEventRetentionDays` | Interaction events |
| `InteractionRetentionDays` | Interactions |
| `CallSessionRetentionDays` | Call sessions |
| `QueueItemRetentionDays` | Queue items |
| `ActivityReservationRetentionDays` | Activity reservations |
| `OutboxMessageRetentionDays` | Event outbox messages |
| `WebhookInboxMessageRetentionDays` | Provider webhook inbox messages |
| `ProviderCommandRetentionDays` | Provider commands |
| `AgentSessionRetentionDays` | Agent sessions |
| `CallbackRequestRetentionDays` | Callback requests |
| `EventMetricRetentionDays` | Daily event metrics |
| `ProcessedEventRetentionDays` | Processed-event markers |
| `WorkStateRetentionDays` | Routing work state |

### Records are aged from when they settled

A record is aged from the moment it reached a final state, never from when it arrived, when it was last retried, or when it was due:

| Entity | Aged from | Finished when |
| --- | --- | --- |
| Interaction event | `OccurredUtc` | Always — an event is an immutable fact |
| Interaction | `EndedUtc` | The conversation ended |
| Call session | `EndedUtc` | The call ended |
| Queue item | `DequeuedUtc` | Completed or removed |
| Activity reservation | `ModifiedUtc` | Rejected, expired or canceled |
| Routing work state | `ModifiedUtc` | Never — see below |
| Event outbox message | `CreatedUtc` | Completed or dead-lettered |
| Provider webhook inbox message | `ProcessedUtc` | Completed or dead-lettered |
| Provider command | `CompletedUtc` | Confirmed, compensated or failed |
| Agent session | `LastHeartbeatUtc` | The session stopped reporting |
| Event metric | `Date` | Always — a closed day is final |
| Processed-event marker | `ProcessedUtc` | Always — the event was handled |
| Callback request | `ModifiedUtc` | Completed, canceled or failed |

Ageing from arrival time would delete exactly the work that waited longest — the calls that sat in queue for an hour, the commands that retried for a day — which are the records an operator most needs when explaining what went wrong. Ageing from a *scheduled* time is worse still: a callback booked three weeks out carries a future timestamp, and a command in backoff carries a retry time that keeps moving.

The status condition is what keeps a live record alive. Age alone never makes an in-flight record safe to delete, so a queue item that is still waiting, a command whose outcome is still unknown, or a conversation that has not ended survives regardless of how old it is.

### Floors extend retention, never shorten it

| Setting | Meaning |
| --- | --- |
| `ProjectionReplayHorizonDays` | Minimum days the event log must remain rebuildable. Applies to the interaction event log, the only table projections replay from. |
| `LegalHoldMinimumDays` | Legal-hold / regulatory floor. Applies to communication history: interaction events, interactions, call sessions and callback requests. |
| `ProcessedEventDeliveryEnvelopeDays` | How long a provider may still redeliver an event. Applies to processed-event markers, which suppress a redelivery. |

An accepted reservation is deliberately absent from that list. Accepting is the live claim an agent holds: it is the state that keeps the unique activity claim in place and that tells the agent what they are working on. Deleting one would release the claim so the same work could be reserved twice, and would strip the agent's own assignment out from under them. A reservation only settles when it is rejected, expires or is canceled.

Routing work state is the one entity with no finished state at all, because whether the work is over is owned by the CRM activity rather than by the routing document. It is aged from its last mutation alone. That is safe only because the document is reconstructible: a work state that no longer exists is recreated on next access and re-seeded from the activity projection, which re-adopts the assignment status, reservation and attempt counts. This is the same adoption path a tenant that predates the document takes, so a purged work state is recoverable rather than lost. Without a policy it was the one table that grew by a row for every activity ever routed and was never deleted by anything.

A settlement column is only useful if something writes it. Reservations and callbacks are aged from their last modification, and nothing was stamping that field on the transitions that settle them, so both policies would have matched no row at all — the entity would have reported itself drained every cycle while its table grew. The reservation service now stamps the modification time on every release and cancellation, and the dialer stamps it when a callback is promoted to an activity. A callback counts as settled once it has been scheduled, because from that point the promoted activity is the durable record of the work; the remaining outcome statuses are kept in the policy for completeness. A gate asserts that every settlement column an active policy reads is stamped by a named statement in a named method on each path that settles the record. Naming the method matters: a type usually has several settlement methods, and a file-wide search is satisfied by any one of them, so deleting the stamp from the single path that ends in a status no other method produces would leave the build green while every record settled that way became immortal.

Where a settlement time is added to an existing index by an upgrade, that upgrade also backfills it. Adding a column to an index does not re-project the documents that already exist, and a settled record is never written again, so every row that predated the upgrade would keep a null settlement time and be rejected by its own policy forever. The backfill dates those rows from the upgrade — later than the truth, so it can only ever delay a purge, never bring one forward — and is restricted to rows already in a settled status so nothing in flight is given a false completion time.

Every purge predicate is backed by an index that leads with the timestamp it selects on. The drain loop asks for a batch at a time with no ordering, so an unindexed predicate turns each terminating batch into a full scan of the very table retention exists to bound — worst on the largest tables, and on every cycle once the table has reached steady state. A gate asserts the covering index exists for each policy.

The effective cutoff for an entity keeps records for `max(window, applicable floors)` days, so raising a floor extends retention and never causes an earlier purge. Purging stays disabled for an entity whose window is `0`, which is the default — an unconfigured tenant deletes nothing.

Floors are scoped rather than global. Applying legal hold to delivery bookkeeping would hold outbox rows for years without holding anything a regulator asked for, and applying it to processed-event markers would trade a disk problem for a much larger one. The redelivery floor exists for the opposite reason: purging a deduplication marker while its event can still be redelivered makes the redelivery look new, and the side effect runs a second time. A completed webhook inbox row is such a marker: its payload is cleared at settlement and the row is kept only so a repeated provider delivery is recognised as a duplicate, so it is aged from settlement rather than from receipt — settlement lags receipt by the whole retry envelope, so ageing from receipt would silently shorten the guarantee. Its floor is the seven-day duplicate-detection horizon the inbox itself enforces, not a configurable envelope, because the inbox already sweeps its own settled rows at that horizon on every dispatch pass. Two purges targeting one table means the shorter of the two decides the real window, so the sweep and the retention policy are reconciled in both directions: the policy may never delete inside the seven-day horizon, and the sweep never deletes before `WebhookInboxMessageRetentionDays` — otherwise raising that setting would change nothing and the operator would be configuring a value the sweep silently overruled. Leaving the setting at `0` keeps the seven-day sweep, because an inbox row exists only to bound a duplicate window that is already bounded.

### Purging drains, and says so when it cannot

Each cycle drains an entity in batches until the table is empty rather than deleting a fixed number of rows and stopping. The default batch size (`PurgeBatchSize`) and per-cycle budget (`MaxPurgeBatchesPerCycle`) allow five million rows per cycle, so a database that has accumulated a large backlog returns to steady state within one cycle. The session is committed between batches so a large drain never accumulates one unbounded transaction.

| Setting | Meaning |
| --- | --- |
| `PurgeBatchSize` | Rows deleted per batch. |
| `MaxPurgeBatchesPerCycle` | Batches per entity per cycle, bounding a single cycle's work. |

The budget is spent per entity rather than shared across the cycle. A shared budget would let whichever policy runs first consume all of it whenever its table is large, and every entity behind it would never be purged at all while the cycle still reported success.

If the budget runs out, the cycle logs a warning naming the entities that still have work rather than completing quietly — an operator who has outgrown the budget finds out from a log line instead of from a full disk. One entity failing does not stop the others: a single unhealthy table would otherwise keep every other table growing. A batch that fails partway through has already staged some of its deletes into a session every entity shares, so those deletes are committed and counted against the entity that produced them before the cycle moves on; left staged they would be flushed by whichever entity ran next, committed under that entity's transaction and attributed to nobody.

### Tables that are deliberately never purged

Configuration and reference data — queues, queue groups, skills, entry points, dialer profiles, business-hours calendars, agent profiles, agent queue memberships and reason codes — are bounded by tenant setup rather than by traffic, so ageing them out would delete a working configuration. Projection checkpoints hold one row per handler, and deleting one replays that projection from the beginning.

Each exemption is recorded with its reason and is checked by a gate, so a new table is either covered by a policy or exempted on purpose. Adding an index without doing either fails the build.

Each policy is registered by the feature that owns its data — queue items and reservations by **Queues**, callbacks by **Dialer**, provider commands and webhook inbox messages by **Voice**, agent sessions by **Availability** — so a tenant purges exactly the tables it actually writes to, and enabling a feature brings its retention with it.

Behavior guarantees:

- **Retained snapshot** — the daily metrics projection is a durable aggregate that survives event purge, so reporting figures remain available after the raw events are gone.
- **Post-purge rebuild** — after a purge, a projection rebuild (`RebuildAsync`) recomputes counts only from the events that remain; the replay-horizon floor guarantees that window is at least `ProjectionReplayHorizonDays`.
- **Legal hold** — set `LegalHoldMinimumDays` above the retention window to hold events for a case or regulatory obligation without changing the operational retention setting.

## Per-entity data governance

Every persisted Contact Center data category is classified in code by `ContactCenterDataGovernanceCatalog`, the single source of truth this table renders. Each category declares its privacy sensitivity, whether it references call recordings, what governs its retention, and how an erasure (right-to-be-forgotten) request is satisfied. The catalog is unit-tested for integrity — keys are unique, personal categories always declare a concrete erasure strategy, non-personal categories never anonymize, and any recording-bearing category is always classified as personal — so a new persisted entity cannot ship without an explicit classification.

| Data category | Sensitivity | Recording ref | Retention basis | Erasure |
| --- | --- | --- | --- | --- |
| Interaction event log | Personal | No | `InteractionEventRetentionDays`, floored by replay-horizon and legal-hold | Retention expiry |
| Interaction | Sensitive personal | Yes | `InteractionRetentionDays`, floored by legal-hold, once ended | Anonymize (+ external recording erasure) |
| Call session | Sensitive personal | Yes | `CallSessionRetentionDays`, floored by legal-hold, once ended | Anonymize (+ external recording erasure) |
| Callback request | Personal | No | `CallbackRequestRetentionDays`, floored by legal-hold, once resolved | Anonymize |
| Agent session | Personal | No | `AgentSessionRetentionDays`, from last heartbeat | Anonymize |
| Agent profile | Personal | No | Agent account lifecycle | Anonymize |
| Event outbox message | Personal | No | `OutboxMessageRetentionDays`, once completed or dead-lettered | Retention expiry |
| Provider webhook inbox message | Personal | No | `WebhookInboxMessageRetentionDays`, once completed or dead-lettered, floored by the seven-day duplicate-detection horizon | Retention expiry |
| Provider command | Non-personal | No | `ProviderCommandRetentionDays`, once settled | Retention expiry |
| Queue item | Non-personal | No | `QueueItemRetentionDays`, once dequeued | Cascade with interaction |
| Activity reservation | Non-personal | No | `ActivityReservationRetentionDays`, once rejected, expired or canceled | Retention expiry |
| Routing work state | Non-personal | No | `WorkStateRetentionDays`, from last mutation; recreated and re-seeded on next access | Retention expiry |
| Event metric | Non-personal | No | `EventMetricRetentionDays` | Not applicable |
| Projection checkpoint | Non-personal | No | Operational; updated in place | Not applicable |
| Processed-event ledger | Non-personal | No | `ProcessedEventRetentionDays`, floored by the redelivery envelope | Retention expiry |
| Routing and dialing configuration | Non-personal | No | Administrator-managed | Not applicable |

**Erasure strategies.** *Retention expiry* removes the record automatically when it ages past its window (no per-subject action). *Anonymize* clears the personal fields — the customer/caller addresses and free-text notes — while keeping the record so aggregate metrics and audit history survive. *Cascade with interaction* erases the record together with its parent interaction. *External store* delegates erasure to the system that holds the payload. *Not applicable* means the category holds no personal data.

**Call recordings.** Recordings are never stored inside Contact Center. The `Interaction` and `CallSession` entities hold only a `RecordingReference` (an opaque pointer) and a `RecordingState`; the media itself lives in the telephony provider or a configured media store. Consequently:

- **Access audit** — recording playback and download must be brokered by, and audited in, the system that holds the media. Contact Center exposes the reference under the same permission and content-access-control checks as the owning interaction; every access decision is logged through the operational log with the identifier taxonomy (recordings are treated as sensitive personal data). Wiring a specific media store's access log is a deployment integration.
- **Recording erasure** — anonymizing an interaction or call session clears the personal fields it holds and issues a delegated erasure request to the external store for the referenced media; Contact Center does not assume it can delete provider-held media directly.

**Backup and restore.** All durable Contact Center state lives in the tenant SQL database (see the [failure runbooks](runbooks.md)); back it up with the engine's native, point-in-time-capable mechanism. Because the interaction event log is the projection-rebuild source, keep `ProjectionReplayHorizonDays` and `LegalHoldMinimumDays` set so a point-in-time restore retains enough history to rebuild projections — after a restore, run the metrics projection rebuild to reconcile any drift. Provider-held recordings are backed up by their owning store, not by the Contact Center database backup, so a full restore must coordinate the database restore with the media store's own retention and restore policy.

## Preview data maintenance

Contact Center is a preview capability, so a tenant that piloted it accumulates operational data — interactions, activities, assignments, agent sessions, dialer records — that an operator must be able to take away and clear before a real rollout. Deleting rows by hand is not a substitute: the entity set spans twenty-one document types, and a table missed by hand leaves orphaned state that later reads treat as live work.

The `CrestApps.OrchardCore.ContactCenter.Maintenance` feature provides the procedure, as four steps under **Contact Center → Preview maintenance** (guarded by the `ManageContactCenterPreviewData` permission):

1. **Export** — writes every document in the Contact Center collection to a single JSON file and returns an *export receipt*.
2. **Quiesce** — stops each enabled feature from admitting new work and drains what is already in flight.
3. **Reset** — deletes the data, either `OperationalData` (preserving operator-authored configuration such as queues, skills, routing entry points, agent profiles, and business hours) or `All`.
4. **Verify** — recounts every data set and reports anything that survived.

The steps are mechanically coupled rather than merely documented in order. The receipt is a hash of the tenant name and the per-data-set document counts at the moment of export; a reset recomputes it from live state and refuses a receipt that no longer matches. A tenant that is still admitting work therefore keeps invalidating its own receipt, so **the reset cannot succeed unless the export is genuinely complete and the tenant is genuinely quiesced**. An operator cannot reach the destructive step having skipped the step that makes it recoverable.

Reset refuses unless every guard passes, and reports which one refused:

| Guard | Refusal |
| --- | --- |
| `CrestApps_ContactCenter:PreviewMaintenance:AllowReset` is `true` | `ResetNotAllowed` — the default, so no deployment can reset without explicitly opting in. |
| The environment is not production | `ProductionEnvironment` — overridable only by setting `RefuseResetInProduction` to `false`. |
| The typed confirmation matches the tenant name | `ConfirmationTokenMismatch` — an operator cannot wipe the tenant they are not looking at. |
| Every enabled feature is quiesced | `WorkNotQuiesced` |
| A receipt was supplied | `ExportReceiptMissing` |
| The receipt matches current counts | `ExportReceiptStale` |

Which data sets belong to which governance category, and which count as operator-authored configuration, is declared in one registry that a build gate compares against the persisted document types discovered by reflection. Adding a new persisted entity without classifying it fails the build rather than silently escaping both export and reset.

Reset is a preview-lifecycle tool, not a retention mechanism. Ordinary data minimization is handled by the retention windows and per-entity governance described above.

## Query-plan budgets

Retention bounds how large a table becomes. It does not bound how much of that table a query reads, and the two failures look nothing alike: a table that grows without limit eventually fills a disk and announces itself, while a query that reads a whole table returns exactly the right answer every time and simply gets slower in proportion to how much history the contact center has. Nothing in a functional test can see the difference, so the plan itself has to be asserted.

The statement under budget is the one routing runs before every offer: how much live work each candidate agent is already holding. Three things make it cheap, and each is enforced.

**The predicate has to be answerable from an index.** Asking for the interactions that are *not* finished — a chain of inequalities — cannot be satisfied by an index on the status column, because an index orders values and an inequality does not name the ones it wants. The statuses that occupy an agent are therefore declared as a set and tested with `IN`. Because that set is inclusive, a status added to the enum and not classified would silently fall out of it, an agent holding work in the new status would look idle, and they would be handed more: a build gate requires the declared sets to partition `InteractionStatus` exactly, so adding a status forces a decision about it.

**The index has to lead with the predicate.** The interaction index already carried a composite index leading with `DocumentId`, which is the YesSql join key and serves join-back and delete-by-document, but answers nothing about an agent. `IDX_InteractionIndex_ActiveByAgent (AgentId, Status, DocumentId)` is added alongside it rather than replacing anything, and it covers the count outright, so the number comes from the index without touching the table at all.

**The count has to happen in the database.** Loading one row per interaction and grouping them in memory makes the cost of a routing decision scale with how busy the contact center is, which is precisely when a routing decision must be cheapest. The count is a SQL `GROUP BY`, issued on the calling session's own transaction so that a caller who reserves an interaction and then asks for that agent's load in the same unit of work is not told the agent is free.

**How the budget is enforced.** Two gates run `EXPLAIN` against the statement — read from the same builder the store executes, so a plan cannot be proven for a query nobody runs — against a schema built by the shipped migrations, so it cannot be proven for indexes nobody deploys, and against enough seeded rows for a planner to have a real choice, since a planner reads a small table end to end because doing so is genuinely cheaper. The SQLite gate runs on every build and requires the plan to seek the covering index with both the agent and the status as seek constraints, rather than seeking by agent and then testing the status row by row. The PostgreSQL gate runs in the operations-gates workflow against a real PostgreSQL service and requires no sequential scan of the interaction index table. Neither gate rewrites the statement before measuring it, and the PostgreSQL job additionally executes the store method and asserts its counts, so a statement that plans well but cannot run is not mistaken for a passing budget. Reservation itself counts live work through the query pipeline rather than the hand-written statement, so a recording connection captures the SQL the pipeline emits for those queries and holds them to the same budget.

Both are needed, because the planners disagree and the results do not. PostgreSQL will choose to read a table end to end where SQLite seeks, so only the plan on the engine a deployment actually runs is evidence for that deployment.

### The agent-workspace poll

The second statement under budget belongs to the read that runs most often in the whole product: the workspace state every signed-in agent polls continuously. Its cost used to grow in three separate places, none of which changed a single byte of what the agent saw.

**Queue depth was asked once per queue.** An agent who covers eight queues issued eight counts a poll, and no index could answer any of them: the composite index leads with `DocumentId`, which serves join-back and delete-by-document and says nothing about a queue, and the retention index leads with `Status`, so the planner seeks that and then walks every waiting item in the tenant to find the ones in the queue being asked about. Depth is now one grouped statement covering all of an agent's queues at once, and `IDX_QueueItemIndex_WaitingByQueue (QueueId, Status, DocumentId)` is added alongside the existing indexes so it is answered from the index alone. Batching without the index would only have moved the growth: one statement that walks every waiting item in the contact center is no cheaper than forty that do.

**The work behind recent interactions was resolved one at a time**, so the wrap-up check cost a query per interaction the agent had recently ended. Those activities are now fetched in a single read.

**Recent interactions were read twice**, once for the active-interaction panel and once for the history panel, because each panel fetched what it needed for itself. The list is now read once and shared.

**Both failures are gated, because either alone leaves the other free.** The round trips a single poll issues are counted at the handler, so a caller that loops over the single-item APIs fails on the count rather than on its output — a plan budget cannot see this, since a well-planned statement run once per queue costs the same as one scan. The plan of the batched statement is then measured on SQLite on every build and on real PostgreSQL in the operations-gates workflow, with the seek required to be constrained by both the queue and the status: an index that leads with the queue and tests the status row by row still names the index in the plan while walking every item that queue has ever held. Neither gate is written as "no table scan", because losing the covering index does not produce one — the planner reverts to the `Status`-led retention index, which is a seek by name and unbounded work in fact — so both require that no other index answers this question. As with the routing statement, the plan is measured on the SQL the store actually sends, and the PostgreSQL case executes it as well as explaining it.

### Daily event counts

Counting an event is the most frequent write in the product, and it used to be the narrowest. Each day and event type had a single row, and recording an event read that row, added one and wrote it back. That makes one row the meeting point for every handler counting the same kind of event on the same day: two that create it concurrently collide on its unique constraint, and two that update it concurrently fail the optimistic-concurrency check, so one of them either loses its whole request or overwrites a count it never read. Both outcomes are produced by load, not by code, so neither shows up outside production.

A recorded count is now appended as its own contribution and never updated. Nothing serializes, because no two writers touch the same row. A background roller, holding the same distributed lock every other Contact Center background task holds, folds the contributions into the daily totals a batch at a time.

Moving the write is the easy half. Four things have to hold afterwards or the totals are quietly wrong:

- **The roller deletes exactly the contributions it read.** Deleting by predicate would also destroy anything appended between the read and the delete, and that event would be counted by nobody.
- **A fold adds to the total already stored.** Replacing it looks correct after the first fold of the day and discards every earlier hour.
- **A reader adds the contributions not yet folded, and stops once they are.** Otherwise a summary read a moment after the traffic it describes is behind by whatever the roller has not reached, or — if it keeps adding them after the fold — reports double.
- **Drift detection and rebuild count the contributions not yet folded — without folding them.** Ignoring them reports every one as a projection that has lost counts, and a real loss cannot be told apart from the roller not having run in the last minute. Folding them instead is worse: detecting drift is a read, and folding inside it commits the unit of work of whoever asked for the report, while folding before a rebuild reads the event log makes anything recorded in between counted by the recompute and folded again on top, inflating a business number permanently. Drift detection therefore adds the pending contributions to the stored totals in memory, and a rebuild subtracts them from the totals it writes so that folding them afterwards lands on the right number. **A rebuild is a repair that converges, not a snapshot that is exact.** It cannot read the event log, the contributions and the totals in one snapshot, so a rebuild run against live traffic leaves a residual, and the residual is not all in one direction. Reading the log before the contributions removes the case where an event recorded between the two reads is counted by the recompute and folded again on top of it. Two gaps remain that leave a day *high* rather than short. A contribution is not written in the unit of work that writes the event: the projection handler runs in a post-commit scope and is redelivered by the outbox, so an event is in the log for a window before its contribution exists at all, and a rebuild inside that window subtracts nothing for it while the later fold adds it a second time. Document identifiers are also allocated before the transaction that commits them, so a contribution can become visible below a position the walk has already passed and is missed for the same reason. Neither is silent — the next drift check reports the difference — and a rebuild run once the projection is settled, meaning the outbox drained and the roller caught up, writes exactly the log. Run a rebuild against a settled projection when the number has to be right immediately; run it against live traffic to repair, then re-run it once traffic has settled. Reading those pending contributions is itself a walk of the whole contribution table, and it resumes from a position rather than from an offset: the roller deletes the rows it folds from anywhere in that table, so an offset would step over rows that are still waiting once earlier ones are gone and the counts they carry would simply be absent, leaving the rebuild to write a total that is permanently short with nothing in the data to show it happened.
- **The roller belongs to the feature that does the counting.** Registered under a feature only some tenants enable, the totals of every other tenant are never folded, and retention purges the contributions before anything reads them — the counts are gone with no error anywhere.

The contribution table is drained rather than accumulated, so it is sized by one drain interval rather than by traffic, and it carries a retention policy for the contributions a roller could never fold. The drain is deliberately unordered. The document query groups by document identity, and no ordering over the contribution columns can satisfy that grouping, so asking for one makes the engine sort the entire backlog before it can return a single batch — draining one batch would then cost as much as everything waiting. The roller sums whatever it is handed and removes exactly those rows, so no order is needed. That also means the contribution table carries no index for the roller at all: the document identity index YesSql already maintains answers the drain, and an index the drain never uses would only be another cost paid on the append path this design exists to keep free. It does carry an index on the day, because adding the contributions not yet folded to a summary is a request-path read that asks for them by day, and leaving that to walk a table written to on every recorded event would make reading a summary cost more the busier the deployment is. That read is also bounded: a backlog larger than a single drain is one no reader can report exactly anyway, so it truncates rather than growing without limit, with the same transient shortfall a lagging roller already produces.

## Upgrade and migration safety

Contact Center follows an expand → migrate → contract policy so a rolling or blue-green deployment never runs an old and a new node against a schema either cannot use:

- **Expand** — a release only adds schema. New columns are additive and ship with a default (or are nullable), so an old node keeps writing valid rows while the new node populates the new column.
- **Migrate** — backfill and any new unique constraint run inside the upgrade migration against the module's own index tables. Unique-constraint creation is preceded by a portable preflight that detects pre-existing duplicate active claims and fails with explicit repair guidance instead of silently corrupting data or throwing an opaque unique-index error later.
- **Contract** — destructive changes (dropping or renaming a column or table, narrowing a type, or removing a default) are deferred to a later release, after every node is known to no longer read the old shape.

Audit of the shipped Contact Center migrations: every migration is additive — `CreateMapIndexTable`, `AddColumn` with a default or nullable value, and guarded `CreateIndex`/`CreateUniqueIndex`. There are no `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, or `AlterColumn` operations, so no shipped upgrade requires downtime. Any future backward-incompatible change must either be restructured into the expand/migrate/contract phases above or explicitly declare a downtime requirement in its release notes.

**How the contract phase is enforced.** The policy above is a build gate, not a convention. Every Orchard data migration in the repository is parsed, and three oracles look for a destructive step: schema-builder calls such as `DropColumn`, `DropIndex`, `DropTable`, `RenameColumn`, `RenameTable`, and `AlterColumn`; raw SQL passed as an argument to any synchronous or asynchronous execution method; and raw SQL assigned to a command's text. The raw-SQL oracles reconstruct the statement across string concatenation, interpolation, single-assignment locals, and read-only query-builder composition, then classify it, so a statement built at runtime is judged on what it does rather than on whether a literal happens to match. Classification does not stop at the leading verb, because a destructive statement need not lead: a common table expression begins with `with` and a batch can hide a second statement after a semicolon, so a destructive verb anywhere in the statement is a finding. Quoted values are removed before that scan so a literal that merely reads like a verb is not mistaken for one, and a statement that can execute another statement — `EXEC`, `sp_executesql`, or a procedural `DO`/`BEGIN` block, wherever it appears — is treated as unreadable rather than as safe, because the gate can see the wrapper but not what it runs. A statement the gate cannot read is itself a finding: it must either be written so the verb is visible or be recorded, per call site, with what it does and why it cannot be destructive. Such a recorded approval is pinned to a fingerprint of the type that declares it, so changing what the statement builds invalidates the approval and forces a fresh review.

Every destructive step needs a register entry that authorizes one operation against one named object, and an entry that matches no step or several steps fails, so an authorization cannot go stale or quietly widen. Justifications are checked rather than trusted: a contract-phase removal must name a strictly older release as the one that introduced the object, which makes expand and contract landing in the same release impossible; and a claim that an object never reached a customer must name the database object it is about, which is then searched for in the source of every stable release tag. The claim fails if the object is present in any released tree, if it cannot be bound to the object the entry authorizes, or if the released source cannot be read. The claim also has to be bound to the object the entry actually operates on. A schema operation names its object directly, so the claim must equal it. Raw SQL is read at the operand position — the identifier that follows `drop table`, `alter table`, `delete from`, and the like — rather than anywhere in the statement, so an object named in a trailing comment cannot stand in for the one being dropped. Reconstruction is what makes that position readable: it resolves constants, interpolation holes, table quoting, schema qualification, and index-table naming conventions. Every operand in the statement must be the claimed object, not merely the first, so a batch that drops an authorized table and then a second, unauthorized one is rejected instead of being covered by a single claim. An operand the gate cannot read is a finding rather than a pass. Without that binding, changing the constant that names the dropped table would leave the statement classification, the authorization, and the claim all unchanged while dropping something else entirely. Checking the claim against the shipped source is what turns it into evidence: a version number, or a commit the author chooses, is an assertion the gate cannot verify. `UninstallAsync` is exempt, and only `UninstallAsync`, because feature uninstall is not an upgrade path.

The gate's scope is Orchard data migrations. Destructive DDL executed from a background task, a recipe, a feature event handler, or an ordinary service is outside it, and a prerelease-to-prerelease upgrade is outside it as well: the never-released justification is evaluated against stable releases only, because upgrading from a preview or release candidate is not a supported path.

**Why the static gate is not sufficient on its own.** The contract-phase gate reads migrations without running them, so it can only reject what is visibly destructive. Adding a non-nullable column with no default is entirely additive, passes that gate, and still breaks every write a still-running previous version performs, because that version supplies no value for a column it does not know about. Only executing both write shapes against one real upgraded database can catch it, so the migrations are additionally exercised as a rolling upgrade. Two databases are built from the shipped migrations: one takes the fresh-installation path, and one is installed at the previous schema version and then upgraded. A previous-version writer and a current-version writer then both insert into the upgraded tables, and the previous version's projection is read back.

The same harness compares the two databases column by column and constraint by constraint, because a fresh installation and an upgraded installation of the same release must not be distinguishable. That comparison is what makes the previous-version write assertion trustworthy over time, and it has already found a real divergence: the reservation and queue-item claim keys were declared with an inline unique constraint and no default on the create path, but with a named unique index and an empty-string default on the upgrade path, so the same release produced two different schemas depending on when the tenant was installed. The create steps now build the constraints exactly the way the upgrade path builds them.

**An upgraded tenant must be indistinguishable from a fresh one, and the declaration is the only evidence.** A rolling-upgrade harness that compares values proves the two tenants agree today; it does not prove they agree on an engine nobody ran it against. YesSql writes an enum index property as an integer whatever the column is declared as, and SQLite applies type affinity in both directions — converting the integer on the way in and applying column affinity to comparisons — so a column that should be an integer and was created as text behaves correctly on SQLite indefinitely and fails only on an engine that does not coerce. A gate therefore compares the declarations themselves: the table and index declarations of a tenant upgraded through every reachable historical schema must be identical to those of a tenant created fresh. Which historical schemas are reachable is decided by walking the migration chain by return value, exactly as the host walks it, so a step that no released version can reach is not mistaken for one that runs.

**A type reconciliation is a rebuild, not an `ALTER`.** SQLite has no `ALTER COLUMN`, so correcting a column's declared type means adding a replacement, copying the values into it with a set-based statement, dropping the original, and renaming the replacement into its place — and dropping any index over the column first, because SQLite refuses to drop a column an index refers to, then recreating it. Those removals are destructive in the letter of the contract phase while being safe in substance, because the object exists under the same name before and after within one step. They are authorized as in-place rebuilds, and that authorization is machine-checked rather than trusted: the entry must name the operation that puts the object back, and that operation must really be present in the same migration step, so deleting the recreation fails the build. A removal written as SQL is reported by its leading verb rather than by an object name, so such an entry additionally names every object it takes away and each is looked for in the same step. Value translation accepts both a stored number and a stored member name and records anything else as unknown, so an unreadable legacy value is never silently rewritten to the enum's first member.

**The declaration gate proves the shape; only the engine proves the upgrade runs.** Comparing declarations on SQLite finds the divergence but cannot prove the correction is executable, because the same affinity that hid the divergence also makes the correcting statement run. Three defects survived the SQLite gate and were found only by executing the upgrade against a real PostgreSQL instance. The first is that the historical version the correction starts from is reachable in two different shapes — one whose enum columns are text and one whose enum columns are already correct — so the rebuild probes the column's declared type by reading an empty result set and returns without touching a column that is already right; running the text-to-number translation over an integer column coerces silently on SQLite and raises an undefined-operator error on PostgreSQL. The second is that PostgreSQL and SQLite name only the index in a drop, so the name resolves against the connection's search path rather than the table's schema: a tenant whose tables live in a named schema drops nothing, and because the statement carries `IF EXISTS` the miss is silent until the recreation reports the index already exists and activation fails. The migration therefore issues a schema-qualified drop first on exactly those engines. The third is that such a drop has to name the index the way the data layer named it rather than the way the migration spells it, because the data layer prefixes index names on the engines that share one index namespace across tables and shortens names the engine cannot hold; naming the migration's own spelling reproduces the same silent miss on any tenant that also sets a table prefix, so the gate configures a schema and a table prefix together. The upgrade is executed against PostgreSQL from both reachable shapes as a gate, since a migration that produces the right declarations on SQLite and cannot run on the production engine has fixed nothing.

**The reconciliation is resumable, because not every engine can roll a schema change back.** MySQL commits each schema change on its own, so an attempt that fails part-way leaves the completed changes in place while the migration version is never recorded, and the next activation runs the step again from the top. A step that is not resumable turns one interrupted upgrade into a tenant that can never activate. The replacement column is therefore added only when it is not already present; an attempt that stopped after the original column was dropped finishes by renaming the replacement rather than starting over and destroying the translated values it already holds; and the index drops are tolerant while the recreations are not, so a drop that genuinely fails is still reported by the recreation that follows it.

**Backfills are set-based because they run inside tenant startup.** An upgrade backfill executes in the transaction that gates the tenant's activation, so its cost is startup time. Reading a table into memory and issuing one statement per row is invisible on a developer database and fatal on a tenant with a million rows, which never finishes activating. Canonicalizing a value that comes from a finite set is done once per distinct value the table actually holds rather than once per row; derived keys are computed by the database from columns it already has, using the dialect's own string concatenation because `||`, `+`, and `concat()` are not interchangeable across the supported engines; and duplicate preflights are `GROUP BY … HAVING COUNT(*) > 1` over the same composed key the unique index will enforce, so a pair that collides only once composed is still caught. Where a preflight and its constraint differ they differ in the safe direction: a missing value is folded to an empty one, which is stricter than the engines that treat nulls as distinct, so an upgrade refuses with repair guidance rather than creating an index that hides the ambiguity. This is preferred to a batched background backfill, which would leave a window in which the unique constraint the backfill exists to enable cannot yet be created. A build gate runs each backfill against tenants that differ ten-fold in row count and requires the number of database round trips to be identical, because wall-clock in CI is dominated by the machine and would either flap or be set so loose it proves nothing.

**One rolling-upgrade hazard is an operator constraint rather than a gate.** When an upgrade adds a non-nullable column with a shared default and then places a unique constraint on it, only one previous-version write can succeed, because every previous-version node writes that same default. There is no portable fix inside a single release: filtered indexes are unavailable or incompatible across the supported engines, and a nullable column does not help because not every engine treats nulls as distinct in a unique index. The supported answer is to expand and contract across two releases — add the column nullable and unconstrained in one release, then backfill and constrain in the next, once every node is known to write it. Until then, treat a release that introduces a uniquely constrained claim key as requiring drained queues rather than a live rolling upgrade.

### Stored events are converted on read, not on write

A durable event log outlives the code that wrote it. Contact Center does not hand the published object to its handlers: post-commit dispatch, outbox redelivery, the provider-voice reader, and projection maintenance all reload the event from storage by identifier, so a handler always sees JSON that was serialized by whichever release wrote it, deserialized into the type the running release declares. That deserialization does not fail when a payload property is renamed, split, or re-united. It succeeds and substitutes a default, so an event redelivered from last month is acted on with an absent reason, a zero duration, or an empty identifier, and nothing reports a problem.

Every event therefore carries the schema version it was written at, and that version is read on the way out. The payload is converted one version step at a time until it reaches the version the running release understands. The conversion is applied at the event store rather than at each reader, because a reader that forgot to call it would not fail — it would return stale data as though it were current, which is the silence the mechanism exists to remove.

Three situations are refused rather than absorbed:

- **A version step with no conversion registered fails the read**, naming the step. Returning the payload unconverted is precisely the misreading being prevented.
- **An event stored at a version above the one the running release understands fails the read.** A node cannot convert forwards, and during a rolling upgrade the newer node is already writing that version, so an older node must refuse the record rather than guess at it.
- **An event with no recorded version is treated as the first version, not as already current.** Assuming current is the same silent default-substitution in a different place.

The conversion serves the reader and never rewrites the stored row. Rewriting history from whichever node happened to read it first would destroy the only record of what was actually published, and would make a rollback to the previous release unrecoverable.

Because the conversion lives at the event store, it only reaches a reader that goes through the event store. Reading the log directly from the session bypasses it and is invisible to the coverage gate, which knows only the store's own read paths, so a build gate refuses any code outside the store that queries the event log directly.

**Raising the schema version without a conversion fails the build.** A build gate requires the registered conversions to cover every version step from the first version to the current one, so the omission is caught at the moment it is introduced — which is the only moment it is visible, because at that moment every event already on disk becomes unreadable and nothing about a bumped constant looks wrong in review. A second gate seeds a stored event at an unreadable version and requires every read path on the event store, discovered by reflection rather than listed by hand, to refuse it, so a read path added later that bypasses the conversion fails without anyone remembering to extend the gate.

`InteractionEvent` is currently the only persisted Contact Center document that carries a schema version. The day another document needs one, the seam to reuse is the loading hook on the shared document catalog, which every catalog in the product already inherits.

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
