---
sidebar_label: Production readiness plan
sidebar_position: 30
title: Contact Center and SMS Workspace — Production Readiness Plan
description: Expert review of the Contact Center, Telephony, Telnyx, SMS Workspace, Omnichannel Management and SMS Automation projects, with a prioritized, test-first remediation and feature plan to reach production quality.
---

# Contact Center and SMS Workspace — Production Readiness Plan

This is the master plan produced by an in-depth review of the following projects, evaluated as an ASP.NET Core
and Orchard Core codebase and against what state-of-the-art contact centers (Genesys Cloud, Amazon Connect,
Twilio Flex, Five9, NICE CXone) ship as table stakes:

| Area | Projects reviewed | Size (C#) |
| --- | --- | --- |
| Contact Center | `CrestApps.OrchardCore.ContactCenter`, `.ContactCenter.Core`, `.ContactCenter.Abstractions` | ~74k lines, 20 startups, 15 background tasks |
| Telephony | `CrestApps.OrchardCore.Telephony`, `.Telephony.Core`, `.Telephony.Abstractions` | ~13k lines + 6.3k lines of `soft-phone.js` |
| Telnyx | `CrestApps.OrchardCore.Telnyx`, `.Telnyx.Core` | ~11.5k lines |
| SMS Workspace | `CrestApps.OrchardCore.Sms.Workspace`, `.Sms.Workspace.Core`, `.Sms.Workspace.Abstractions` | ~6.5k lines |
| Omnichannel | `CrestApps.OrchardCore.Omnichannel.Managements`, `.Omnichannel.Core`, `.Omnichannel`, `.Omnichannel.Sms` | ~32k lines |
| Tests | `CrestApps.OrchardCore.Tests` (ContactCenter 1,313 tests, Telephony 644, Omnichannel 184, Telnyx 12), DistributedTests (20), FeatureActivationTests (52), PlaywrightTests (30) | builds clean, 0 errors |

The plan is split into one page per workstream so each can be executed and tracked on its own:

| Workstream | Page | Scope |
| --- | --- | --- |
| A + B | [Routing and AI-to-agent handoff](production-readiness-routing.md) | ACD/queueing/skills/overflow/callbacks/IVR, offer pipeline, AI-to-agent escalation for voice and SMS |
| C | [SMS Workspace and SMS automation](../omnichannel/production-readiness-sms-workspace.md) | Inbox security, inbound idempotency, routed distribution, delivery tracking, compliance, UI |
| D | [Soft phone and Telnyx](../telephony/production-readiness-soft-phone-telnyx.md) | Dial safety, transfers, credentials, webhook/media paths, AI voice agent, JS architecture |
| E | [Architecture, feature split, code quality and tests](production-readiness-code-quality.md) | DI patterns, feature boundaries, duplication, god classes, memory, test strategy |

Every item on those pages follows the same contract: **evidence** (file and behavior observed), **why it blocks
production**, **target design**, **tests to write first**, **implementation steps**, and **acceptance criteria**.
Nothing in this plan changes code; it is the specification for the work.

## Executive assessment

The voice core is unusually mature for a CMS-hosted contact center. It has a durable event outbox, a provider
webhook inbox with idempotency and backpressure, a property-tested call state machine, distributed-lock-serialized
assignment and reservations, health checks, retention, query-plan budgets, and architecture tests that pin feature
ownership. That work should be preserved, not rewritten.

What is **not** production ready falls into five buckets:

1. **Security and tenancy defects that are cheap to fix and must ship first.** The SMS Workspace has an
   insecure-direct-object-reference on every conversation action (any portal user can open, close, or claim any
   thread by id), and its delivery-receipt notifier broadcasts to `Clients.All`, which crosses tenants. The soft
   phone keypad and transfer field still bypass the emergency and premium destination policy (documented, but not
   acceptable for GA).
2. **Correctness gaps under concurrency and retries in the SMS path.** Inbound SMS has no per-thread lock and no
   provider-message-id idempotency, so two simultaneous texts from a new number create two conversations, and a
   provider retry duplicates messages. A failed automated (AI) activity permanently blocks a human thread for that
   number.
3. **Feature-split violations.** SMS Workspace advertises a dependency only on the Contact Center Agent Services
   feature, but its routed-distribution strategy resolves `IActivityQueueManager`, which is registered only by
   the Work Distribution feature, so enabling SMS Workspace without Work Distribution breaks inbound routing at
   DI resolution time. The module also references the full `Omnichannel.Managements` assembly. Automated voice
   orchestration lives inside the Telnyx provider module rather than a provider-neutral automation feature.
4. **Missing contact-center capabilities that customers expect.** No IVR or DTMF menus, no queued callback, no
   in-queue announcements or estimated wait, single-hop overflow evaluated once per minute, boolean skills without
   proficiency or relaxation, no cross-queue arbitration for agents in several queues, no attended transfer or
   transfer-to-queue from the agent surface, no first-response SLA on SMS, Predictive dialing exposed in the enum
   but blocked, a stored SMS auto-reply setting that is never sent, and a live-agent tool whose instructions name a
   tool that does not exist (`transfer_to_agent` vs `transferToLiveAgent`).
5. **Code-quality debt that will slow every later change.** Service-locator resolution in 48 files, optional
   dependencies expressed as `IEnumerable<T>.FirstOrDefault()` in 22 constructors, static mutable state
   (`AsyncLocal` handoff context, a process-wide generation registry), hard-coded lock timings that contradict the
   documented "timings are configuration" rule, verbatim duplication between the SMS and voice AI handlers and
   between four Telnyx HTTP clients, and several god classes (1.5k-line controller, 1.4k-line hub, 1.4k-line
   event handler, 6.3k-line JavaScript file). Telnyx has 12 tests for 11.5k lines while Asterisk has 40+ test
   files.

### Readiness scorecard

| Area | Score (1–5) | Blocking issues |
| --- | --- | --- |
| Voice routing core (queues, reservations, offers, state machine) | 4 | Cross-queue arbitration, N+1 candidate queries, per-second client-driven scan, constants instead of options |
| Inbound voice experience (entry points, IVR, callbacks, overflow) | 2 | No IVR/DTMF, no queued callback, no announcements/EWT, 60-second overflow granularity |
| Outbound dialer | 3 | Predictive blocked but exposed; compliance evidence (abandon rate, safe-harbor) must be surfaced in UI/reports |
| AI-to-agent handoff | 3 | Tool-name mismatch, ambient `AsyncLocal` context, voice handoff loses source/direction and carries no agent context, SMS handoff ignores routed mode and business hours |
| SMS Workspace | 2 | IDOR, cross-tenant broadcast, no inbound idempotency, DI failure without Work Distribution, unpaged inbox, dead auto-reply setting |
| SMS automation | 3 | Static process-wide generation registry, Failed activities block human threads, Twilio fire-and-forget without dedupe |
| Soft phone + Telnyx | 3 | Emergency/premium bypass on the phone, transfer bypasses catalog, 12 tests for Telnyx, monolithic JS, in-memory media registry |
| Supervision and reporting | 3 | No SLA views for SMS, no handoff KPIs on dashboards, no real-time queue EWT |
| Architecture and tests | 3 | See workstream E |

## How to execute this plan

### Test-first, always

Manual regression of a contact center is expensive, so every item is sequenced as **characterization tests →
red tests for the new contract → implementation → green**. The repository already has the right harnesses:

- `Modules/ContactCenter/Integration/DialerModeIntegrationHarness` for end-to-end pacing scenarios on SQLite.
- `Modules/ContactCenter/StateMachine/CallStateMachineHarness` for provider event sequences.
- `AgentWorkspaceRoundTripBudgetTests` and `*QueryPlanBudgetTests` for round-trip and query-plan budgets.
- `ContactCenterFeatureDependencyArchitectureTests` and `ContactCenterFeatureLifecycleTests` for feature ownership.
- `FeatureActivationTests` for tenant-level enable/disable matrices.
- `Telephony.PlaywrightTests` for the soft phone UI and WebRTC audio proofs.

Each workstream page names the exact new test classes to add. The rule for "is a test useful" used throughout:
a test must fail if the behavior regresses, must not depend on private implementation order unless the order is
the contract, and must not duplicate an architecture rule already enforced elsewhere.

### Sequencing

| Phase | Goal | Items (see workstream pages) |
| --- | --- | --- |
| P0 — Stop the bleeding (1–2 weeks) | Security and tenancy | C1, C2, C3, D1, D2, E1 (feature-split fix for SMS Workspace), B1 (tool name) |
| P1 — Correctness under load (2–3 weeks) | Idempotency, concurrency, options | C4, C5, C6, A3, A4, A5, B2, B3, E2, E3 |
| P2 — Contact-center feature parity (4–6 weeks) | IVR, callbacks, overflow, skills, transfers, SMS SLA | A1, A2, A6, A7, A8, A9, C7, C8, C9, D3, D4 |
| P3 — Structural cleanup (ongoing, 3–4 weeks) | Feature boundaries, duplication, god classes, JS modules, Telnyx tests | E4–E12, D5, D6, D7 |
| P4 — Release evidence | Run the acceptance procedure in `production-support.md` plus the new SMS and handoff gates | All acceptance criteria green, FeatureActivationTests and PlaywrightTests in PR CI |

### Definition of done for "production ready"

1. All P0–P2 items closed with their acceptance tests in `CrestApps.OrchardCore.Tests` and running in `pr_ci.yml`.
2. `FeatureActivationTests` and `Telephony.PlaywrightTests` run on every PR (today they run only in `release_ci.yml`).
3. No `Clients.All`, no `IServiceProvider` injection outside the documented composition roots, no
   `IEnumerable<T>.FirstOrDefault()` optional-dependency pattern (enforced by a new architecture test).
4. Every feature can be enabled in isolation with only its declared dependencies (enforced by extending
   `ContactCenterFeatureActivationTests` to SMS Workspace and SMS Automation).
5. The documented limitations in `voice-routing.md` ("Current limitations and important notes") that are marked as
   safety issues (emergency dialing, transfer catalog bypass) are removed because they are fixed.
6. Public API baselines regenerated and approved for every touched assembly.

## Cross-cutting principles adopted by every workstream

- **One authority per decision.** Routing, assignment, and reservation already have single authorities; the plan
  extends the same rule to SMS (one `ISmsConversationRouter` used by inbound, handoff, and reassignment) and to
  transfers (one `IContactCenterTransferService` used by the soft phone and the workspace).
- **Explicit optionality.** A feature that may be absent is represented by a null-object default registration
  (`TryAddScoped`), a feature-gated startup, or an `IOptions` switch, never by scanning `IEnumerable<T>` or
  calling `GetService` at runtime.
- **Configuration, not constants.** Every timing that affects a customer or an agent (lock waits, ring windows,
  reclaim sizes, pickup grace, poll intervals) is an `IOptions` value with validation and a documented default.
- **Idempotent by key.** Every provider-originated write carries a provider key and is deduplicated on it
  (voice already does this through the webhook inbox; SMS will).
- **Budgets are tests.** Round trips per request and query plans for hot paths are pinned by tests, following the
  existing `*BudgetTests` pattern.
