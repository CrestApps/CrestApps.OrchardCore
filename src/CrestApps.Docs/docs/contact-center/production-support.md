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
- Production requires one region, two to four application nodes, a shared relational database, the `CrestApps.OrchardCore.SignalR.Redis` feature, and the `OrchardCore.Redis.Lock` feature so real-time messages and Contact Center idempotency/orchestration locks are distributed across nodes.
- Single-node production, multi-node operation without the backplane or Redis distributed locking, and multi-region active-active operation are unsupported.

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

When the `OrchardCore.HealthChecks` feature is enabled it exposes the standard `/health/live` endpoint that aggregates the tenant-registered checks below. The Contact Center feature registers each check with the `contactcenter` and `ready` tags so a readiness probe can select them:

| Check | Signal | Degraded | Unhealthy |
| --- | --- | --- | --- |
| `contactcenter-storage` | A cheap store query proving the tenant database and Contact Center collection are reachable. | — | Query throws. |
| `contactcenter-outbox` | Dead-lettered count and overdue (past-due pending/claimed) backlog. The overdue backlog is the scheduler-lag signal: a sustained non-zero value means the dispatch background task is not keeping up. | Dead-letters or overdue backlog reach the degraded threshold. | Either reaches the unhealthy threshold, or the store is unreadable. |
| `contactcenter-provider-ingress` | Provider webhook inbox dead-letter and overdue backlog. A stuck provider stream or an expired listener lease surfaces here as a growing ingress backlog. | Same thresholds as the outbox. | Same thresholds as the outbox. |

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

SignalR backplane health is owned by the backplane provider rather than the Contact Center module: when a Redis backplane is configured, enable the Redis/backplane connectivity health check so its liveness is aggregated by the same `/health/live` endpoint. On a single node the in-memory backplane needs no separate check.

## Multi-node real-time backplane

The Contact Center real-time hub is backplane-agnostic. It is hosted through `HubRouteManager.MapHub<ContactCenterHub>` and addresses connections through tenant-qualified `TenantSignalRGroupName` groups, so the same code path serves both single-node and multi-node deployments without change. What makes it correct across nodes is the shared backplane, not the hub.

The supported multi-node real-time topology is:

- Enable `CrestApps.OrchardCore.SignalR.Redis` on every tenant that must exchange real-time messages across application nodes. It wires the SignalR Redis backplane (`AddStackExchangeRedis`) using the `OrchardCore_Redis` connection settings and a dedicated SignalR connection, and it namespaces the backplane channel with both `InstancePrefix` and the immutable shell name so two nodes serving one tenant share a channel while different tenants never do. See [SignalR module — Redis backplane](../modules/signalr.md#redis-backplane) for configuration.
- Enable `OrchardCore.Redis.Lock` as well. The SignalR backplane distributes real-time messages, but Contact Center routing, provider webhook inbox acceptance, and other distributed critical sections require the Redis distributed lock independently of the backplane. A backplane without distributed locking is an unsupported configuration.
- Use a deployment-unique `InstancePrefix` (application, environment, region) whenever Redis infrastructure is shared, so tenants with the same shell name in different deployments cannot merge backplane channels.

Single-node production uses the default in-memory backplane and requires neither feature. Multi-node operation without the backplane, or without Redis distributed locking, and multi-region active-active operation are unsupported.

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
- Elasticsearch in routing, assignment, provider ingest, or another correctness path.
- Any feature, provider, database, or topology combination not listed in the versioned matrix.

Unsupported controls are hidden and rejected server-side. Supervisor engagement modes are returned to the dashboard only when the active provider advertises the mode and implements the executable monitoring contract; recording and Contact Center transfer likewise fail closed without their executable contracts. Provider failure or an unknown outcome never writes successful recording, monitoring, or transfer state. Telephony soft-phone commands also repeat capability enforcement on the server. Enabling an implementation that has not passed the profile's release gates does not make that capability supported.

Bidirectional media is excluded more strongly: the legacy capability flag has been removed, the Contact Center and Asterisk media features are dependency-only and hidden from direct feature selection, and neither GA-Core tenant profile enables the media resolver or a media provider. The Asterisk RTP/UDP implementation remains development-only until R9 certifies a secure private-network boundary, packet loss/reordering/jitter behavior, capacity, failover, and node affinity.
