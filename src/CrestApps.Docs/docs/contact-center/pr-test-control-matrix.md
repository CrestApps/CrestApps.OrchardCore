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

A handful of gates already have `implemented` or `partial` evidence because the underlying remediation shipped ahead of this matrix (tenant-qualified real-time identity, manager-owned queue/campaign entitlements, development-host containment, Asterisk credential-log redaction, centralized operational PII redaction, webhook body limits, the declared recording/monitoring prohibitions in the support matrix, and the static feature-dependency architecture ledger). Every other gate remains `planned` until its owning remediation phase (R1-R8) lands the behavior and its automated evidence.

The S001 in-process R0a proof now runs two active shell identities through the same hub context and verifies that user and supervisor notifications resolve to different tenant-qualified destinations. A separate R0b production-backplane run is still required before multi-node isolation is approved.

The F001, F002, and T001 in-process proof, `ContactCenterFeatureDependencyArchitectureTests`, statically parses the Contact Center, SignalR, Telephony, and Omnichannel Managements manifests plus every Contact Center `StartupBase` class and checks them against the machine-readable ledger `.github/contact-center/feature-dependency-violations.v1.json`. R2 has removed FDV001 through FDV003: the base feature now depends only on headless Omnichannel infrastructure with a separate Administration feature; server-side Voice depends on provider-agnostic Telephony; and `Voice.SoftPhone` exclusively owns the soft-phone display driver, resources, endpoints, and call-state projection under declared RealTime, Voice, and Telephony Soft Phone dependencies. For recognized generic registrations the proof resolves constructor dependencies against both manifest and `[RequireFeatures]` closures and now enforces zero known feature-dependency violations. This is a static dependency-closure characterization only; factory/non-generic registrations and the runtime proof that every legal feature combination actually enables, migrates, and resolves every service on a live Orchard tenant remain `planned:contact-center-feature-activation-matrix` dependencies.

The C001/C002 R0a characterization names the current capability-truth failure directly: recording reports success without an executable provider recording operation, and monitoring reports success from a capability flag without invoking a mode-specific provider contract. These tests intentionally preserve the observed unsafe behavior until R4 makes the corresponding NotSupported/provider-execution tests pass.

The D001/C003 R0a capacity gaps are closed by the Availability-owned canonical projection and server-owned recovery policy. Routing now joins profile presence and entitlement with session queue opt-in, online connection state, heartbeat freshness, and active-interaction capacity; the final reservation transition repeats that check under its activity/agent locks. The last disconnect and a stale heartbeat are immediately ineligible even when the administrative profile still says `Available`. A tenant background task also completes expired or orphaned interaction wrap-up timing and restores the agent's pending/default presence without requiring a connected browser or completing the CRM disposition.

The D004 R0a shared-database characterization runs two independent service providers and YesSql sessions against one SQLite database. It synchronizes both reads while the queue item is still waiting, deliberately permits overlapping lock holders to expose the missing database invariant, commits the two sessions, and proves that two distinct pending reservations persist for the same queue item and activity. R3 must invert this test with database-enforced compare-and-set and unique-active constraints; distributed locking remains only a contention optimization.

The D002 provider-command tests now prove reservation acceptance and stable command-intent persistence complete before provider execution, failed acceptance never invokes the provider, command-intent commit failure compensates in a fresh Orchard scope, and definitive provider failures release accepted work. A provider timeout is still treated as a definitive failure and removes the work even though execution may have succeeded. The next R3 command-state increment must record `OutcomeUnknown` after lost responses, reconcile before retry, and fence idempotent compensation.

The D003 R0a provider-event characterizations pin three independent ordering and identity gaps. Two concurrent publishers can both observe a missing idempotency key and persist and enqueue the same logical event. An equal-timestamp event can regress a connected call back to ringing because there is no provider sequence or high-water mark. A provider alias can replace the stored provider name instead of resolving to a stable canonical key. R3 must invert these tests with a durable unique provider inbox, monotonic sequence handling, canonical provider identity, and unique provider-call ownership.

The C004 R0a outbox characterizations reproduce rolling-version and poison-work failures. Reordering two handler registrations causes a handler that already completed under its old assembly-qualified-name-and-index checkpoint to execute again after the checkpoint is persisted and reloaded. A failure in the first due message also stops the batch before later event payloads are loaded or dispatched. R3 must invert these tests with explicit stable versioned handler ids and per-message failure isolation so deployment and poison work cannot duplicate or starve delivery.

The C005 R0a inbound-attribution characterization supplies two valid contacts for the same caller and proves the router immediately persists the first lookup result as the activity contact while never loading the second. The current activity contract exposes no unresolved attribution workflow. R5 must invert this test with explicit resolution workflow and prevent contact-bound subject actions until an operator or deterministic policy resolves the match.

The O003 R0a operational-log gap is closed. The shared `OperationalLogFieldKind`/`OperationalLogIdentifierCategory`/`OperationalLogRedactor` classification API in `CrestApps.OrchardCore.Abstractions` pseudonymizes stable identifiers (user, agent, session, call, interaction, activity, reservation, queue, and event ids) with a process-local keyed HMAC for correlation within one process lifetime, and fully redacts customer/E.164 addresses, secrets/token-shaped values, free-form request descriptions, complete metadata dictionaries, provider response/error bodies, and exception messages/inner-exception data while retaining exception type and a bounded, control-sanitized stack-frame summary. It is migrated across the Telephony hub and interaction reconciliation service, the Asterisk provider/listener/dispatcher, DialPad, the SMS Omnichannel event handler, and the Contact Center Core presence, provider-event, provider call-state synchronization, dialer attempt, outbox, queue/reservation assignment, agent-session, and background-task services, while the existing `SanitizeLogValue` control-character stripping keeps protecting every log line from CR/LF forging. `OperationalLogRedactorTests` and the inverted `ContactCenterOperationalLogPrivacyTests` execute the Telephony hub's request formatters and the redactor directly with sentinel E.164 addresses, user/agent/call ids, secrets, nested exceptions, and an exception with an attacker-controlled `StackTrace` override, scan every migrated source file, and reject any raw exception passed to a logger in the covered projects.

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
