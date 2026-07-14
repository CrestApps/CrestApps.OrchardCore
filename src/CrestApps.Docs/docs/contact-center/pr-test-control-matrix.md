---
sidebar_label: PR-to-test control matrix
sidebar_position: 22
title: Contact Center PR-to-test control matrix
description: Ownership, test id, CI job, execution context, invariant, and retained evidence for every current Contact Center P0/P1 production-readiness gate.
---

The versioned machine-readable contract is `.github/contact-center/pr-test-control-matrix.v1.json`. It maps every P0 and P1 finding from the repository-tracked Contact Center plan's independent production-readiness review to one accountable gate. Release remains blocked until every P0 gate, and every P1 gate in scope for the release profile, passes with retained CI evidence.

Each gate resolves:

- A **category** with an owning DRI role and approver roles.
- A stable **test id** and a **CI job** (`implemented`, `partial`, or `planned`).
- The **provider, database, and topology** context the gate must be proven under.
- A **falsifiable invariant** - a concrete behavior that a test can prove true or false.
- A **retained evidence location** - an existing test/CI artifact path, or a `planned:` path that the owning remediation phase (R1-R8) must populate.

It is expected and acceptable for most CI jobs and evidence paths to be `planned` this early in the remediation program; every P0/P1 finding must still be represented so no gate can be silently dropped.

## Gate categories

| Prefix | Category | DRI role | Approver roles |
| --- | --- | --- | --- |
| `C` | Correctness | Contact Center principal engineer | Orchard Core architecture owner, Quality approver |
| `D` | Data | Contact Center data owner | Database approver, SRE approver |
| `F` | Feature and package graph | Contact Center principal engineer | Orchard Core architecture owner, Product owner |
| `O` | Operations | Contact Center SRE owner | SRE approver, Release engineering approver |
| `S` | Security | Contact Center security owner | Security approver, Privacy or legal approver |
| `T` | Test and topology | Contact Center quality owner | Quality approver, Release engineering approver |
| `V` | Voice/provider | Contact Center voice provider owner | Security approver, Quality approver |

## Current gate count

The matrix currently tracks 41 gates across every P0/P1 finding in the 2026-07-13 independent production-readiness review: `C001`-`C008` (correctness), `D001`-`D009` (data), `F001`-`F006` (feature/package graph), `O001`-`O006` (operations), `S001`-`S005` (security), `T001`-`T003` (test/topology), and `V001`-`V004` (voice/provider).

A handful of gates already have `implemented` or `partial` evidence because the underlying remediation shipped ahead of this matrix (tenant-qualified real-time identity, manager-owned queue/campaign entitlements, development-host containment, Asterisk credential-log redaction, webhook body limits, the declared recording/monitoring prohibitions in the support matrix, and the static feature-dependency architecture ledger). Every other gate remains `planned` until its owning remediation phase (R1-R8) lands the behavior and its automated evidence.

The S001 in-process R0a proof now runs two active shell identities through the same hub context and verifies that user and supervisor notifications resolve to different tenant-qualified destinations. A separate R0b production-backplane run is still required before multi-node isolation is approved.

The F001, F002, and T001 in-process R0a proof, `ContactCenterFeatureDependencyArchitectureTests`, statically parses the Contact Center, SignalR, Telephony, and Omnichannel Managements manifests plus every Contact Center `StartupBase` class and checks them against the machine-readable ledger `.github/contact-center/feature-dependency-violations.v1.json`. For recognized generic registrations it resolves constructor dependencies against both manifest and `[RequireFeatures]` closures, fails on a new unrecorded mismatch, and pins the three currently known P0 findings without refactoring production feature boundaries (deferred to R2): the base feature's declared coupling to Omnichannel Managements (FDV001), Voice's declared coupling to the concrete Telephony Soft Phone feature (FDV002), and Queues' undeclared runtime requirement on SignalR's `HubRouteManager` (FDV003). This is a static dependency-closure characterization only; factory/non-generic registrations and the runtime proof that every legal feature combination actually enables, migrates, and resolves every service on a live Orchard tenant remain `planned:contact-center-feature-activation-matrix` dependencies.

The C001/C002 R0a characterization names the current capability-truth failure directly: recording reports success without an executable provider recording operation, and monitoring reports success from a capability flag without invoking a mode-specific provider contract. These tests intentionally preserve the observed unsafe behavior until R4 makes the corresponding NotSupported/provider-execution tests pass.

The D001/C003 R0a characterizations pin the current capacity failures before R3 changes the lifecycle model. The last agent-session disconnect does not change an `Available` profile, assignment can reserve work without consulting any live session, and `Interaction` has wrap-up start/completion timestamps but no persisted `WrapUpDeadlineUtc` for a server-side sweep. R3 must invert these tests so canonical availability excludes disconnected agents and after-call recovery deterministically releases capacity without a connected browser.

The D004 R0a shared-database characterization runs two independent service providers and YesSql sessions against one SQLite database. It synchronizes both reads while the queue item is still waiting, deliberately permits overlapping lock holders to expose the missing database invariant, commits the two sessions, and proves that two distinct pending reservations persist for the same queue item and activity. R3 must invert this test with database-enforced compare-and-set and unique-active constraints; distributed locking remains only a contention optimization.

The D002 R0a provider-command characterizations pin both sides of the current orphan risk. A provider can return a successful call id before reservation acceptance fails, after which the interaction is marked failed without retaining that call id for compensation. A provider timeout is treated as a definitive failure, the reservation is canceled, and the queue item is removed even though provider execution may have succeeded. R3 must invert these tests by persisting accepted state and stable command intent before execution, recording `OutcomeUnknown` after lost responses, reconciling before retry, and issuing idempotent compensation when required.

The D003 R0a provider-event characterizations pin three independent ordering and identity gaps. Two concurrent publishers can both observe a missing idempotency key and persist and enqueue the same logical event. An equal-timestamp event can regress a connected call back to ringing because there is no provider sequence or high-water mark. A provider alias can replace the stored provider name instead of resolving to a stable canonical key. R3 must invert these tests with a durable unique provider inbox, monotonic sequence handling, canonical provider identity, and unique provider-call ownership.

The C004 R0a outbox characterizations reproduce rolling-version and poison-work failures. Reordering two handler registrations causes a handler that already completed under its old assembly-qualified-name-and-index checkpoint to execute again after the checkpoint is persisted and reloaded. A failure in the first due message also stops the batch before later event payloads are loaded or dispatched. R3 must invert these tests with explicit stable versioned handler ids and per-message failure isolation so deployment and poison work cannot duplicate or starve delivery.

## Contract tests

`ContactCenterPrTestControlMatrixTests` in `tests/CrestApps.OrchardCore.Tests/Modules/ContactCenter` fails the build if:

- The matrix does not contain exactly 41 gates, or the per-category gate counts drift from the table above.
- Any gate id is duplicated, or any of the 41 current P0/P1 gate ids is missing, has the wrong severity, or has drifted from its authoritative title in the production-readiness review.
- A gate's category has no DRI role or no approver roles.
- A gate's execution context omits providers, databases, or topologies.
- A gate has no plan-finding citation, title, falsifiable invariant, test id, CI job id/workflow, or retained evidence location.
- No P0 gate has at least `partial` CI enforcement, which would indicate the remediation program has not started closing any commercial release blocker.

`ContactCenterFeatureDependencyArchitectureTests` in the same folder fails the build if:

- A Contact Center feature's manifest declares a new, unrecorded dependency on a feature outside the Contact Center family, or the ledger records a manifest dependency that no longer exists.
- A recognized Contact Center `StartupBase` registration requires a service not guaranteed by its manifest or `[RequireFeatures]` transitive closure, and that finding is not already recorded in the ledger, or the ledger records a finding that no longer reproduces.
- A ledger violation references a control-matrix gate id that does not exist.
- A Contact Center feature's transitive manifest-dependency closure reaches an unresolvable dependency, cycles back to the feature itself, or drifts from the pinned closure recorded in the ledger.

See [Production support](production-support.md) and [Service objectives](service-objectives.md) for the related finite support matrix and measurable service-level contracts.
