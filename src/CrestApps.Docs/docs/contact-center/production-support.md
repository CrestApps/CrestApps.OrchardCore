---
sidebar_label: Production support
sidebar_position: 20
title: Contact Center production support
description: Finite production support matrix, initial capacity tier, and prohibited Contact Center deployment combinations.
---

The Contact Center commercial release remains blocked until remediation phases R0 through R8 and their release evidence pass. The versioned machine-readable contract is `.github/contact-center/support-matrix.v1.json`; unlisted combinations are unsupported.

The measurable availability, latency, dependency, recovery, and ownership gates are defined in [Service objectives](service-objectives.md). Every P0/P1 production-readiness finding is tracked to a DRI, approver, test id, CI job, and retained evidence in the [PR-to-test control matrix](pr-test-control-matrix.md).

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

Provider stream correctness uses the supported Redis distributed-lock topology. Every canonical provider-call stream is serialized before interaction, call-session, event-log, and outbox changes are read or written, and the YesSql transaction commits before the lock is released. This makes duplicate Asterisk listeners and concurrent DialPad delivery processing harmless across supported nodes without requiring a renewable long-lived socket lease. Lifecycle rank cannot move backward, an established provider sequence high-water cannot be advanced by an unsequenced event, and terminal state remains final.

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

## Prohibited capabilities and combinations

- Power, Progressive, and Predictive dialing.
- Recording, monitor, whisper, barge, take-over, and bidirectional media.
- More than one voice provider profile in one tenant.
- Elasticsearch in routing, assignment, provider ingest, or another correctness path.
- Any feature, provider, database, or topology combination not listed in the versioned matrix.

Unsupported controls are hidden and rejected server-side. Supervisor engagement modes are returned to the dashboard only when the active provider advertises the mode and implements the executable monitoring contract; recording and Contact Center transfer likewise fail closed without their executable contracts. Provider failure or an unknown outcome never writes successful recording, monitoring, or transfer state. Telephony soft-phone commands also repeat capability enforcement on the server. Enabling an implementation that has not passed the profile's release gates does not make that capability supported.
