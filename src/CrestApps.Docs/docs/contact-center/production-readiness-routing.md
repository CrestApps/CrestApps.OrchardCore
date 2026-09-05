---
sidebar_label: Readiness — routing and handoff
sidebar_position: 31
title: Production readiness — call routing, queueing and AI-to-agent handoff
description: Findings and the test-first remediation plan for Contact Center voice routing (ACD), queues, skills, overflow, callbacks, IVR, the offer pipeline, and the AI-to-agent handoff for voice and SMS.
---

# Production readiness — routing and AI-to-agent handoff

Part of the [Production Readiness Plan](production-readiness-plan.md). Workstream **A** covers the automatic
call distribution (ACD) pipeline; workstream **B** covers the AI-to-agent handoff that reuses it.

## What exists today (verified)

The inbound pipeline is `TelnyxWebhookEndpoint` → durable `IProviderWebhookInbox` → `TelnyxWebhookService` →
`ContactCenterTelnyxInboundCallRouter` → `InboundVoiceCallProcessor.RouteInboundAsync` → `IActivityQueueService.EnqueueAsync`
→ `VoiceQueueOfferService.OfferNextAsync` → `ActivityAssignmentService.AssignNextAsync` (per-queue distributed lock) →
`ActivityRoutingService` (strategy chain) → `ActivityReservationService.ReserveAsync` → agent offer over the
Contact Center hub. Reservation expiry, overflow, callback promotion, orphan recovery and provider reconciliation
run as one-minute background tasks.

Routing strategies are `RequiredSkillsRoutingStrategy` (order 10, filter), `CapacityRoutingStrategy` (20, filter),
`StickyAgentRoutingStrategy` (30, boost), and one of `LongestIdle`, `RoundRobin`, `LeastBusy` (100, rank) selected
by `ActivityQueue.RoutingStrategy`. Queue policy includes default priority, SLA aging, reservation timeout,
unanswered-offer action, business hours with after-hours action, a single overflow queue with an after-seconds
threshold, and required skills. Entry points map a dialed number to a queue or a named agent (personal line) with
closed actions (hold, overflow, voicemail, reject).

This is a solid skills-and-presence ACD. The gaps below are relative to what customers evaluating a contact
center compare it against.

## Workstream A — ACD, queues and inbound experience

### A1. Cross-queue arbitration for multi-queue agents (High)

**Evidence.** `QueuedVoiceWorkOfferService.OfferForProfileAsync` iterates `agent.QueueIds` in stored order and calls
`OfferNextAsync(queueId)` per queue until the agent is reserved. An agent signed into Sales and Support is always
served from whichever queue happens to be first in their profile list, regardless of which queue holds the oldest
or highest-priority call. `AssignNextAsync` is strictly per queue; there is no "best item across the agent's queues"
selection. `AgentProfile` has no queue priority or weight.

**Why it matters.** Every production ACD offers the longest-waiting or highest-priority contact across all queues
an agent serves (Genesys "bullseye"/queue priority, Amazon Connect routing-profile queue priority and delay,
Flex TaskRouter priorities). Without it, SLA aging on one queue is invisible to agents parked on another.

**Target design.**

- Add `AgentQueueMembership` weighting: `QueueId`, `Priority` (int, lower first), `DelaySeconds` (do not offer from
  this queue until the item has waited this long), stored on `AgentProfile` (replacing the bare `QueueIds` list) with
  a migration that maps existing ids to priority 0 / delay 0.
- Introduce `IAgentWorkSelector` (Core) with `SelectNextForAgentAsync(agentId)`: gathers the head waiting item of
  every queue the agent serves (single grouped query, reuse `IDX_QueueItemIndex_WaitingByQueue`), computes the
  effective priority with `QueueItemPrioritizer.GetEffectivePriority`, applies membership priority/delay, and returns
  one queue id to run `AssignNextAsync` against. `QueuedVoiceWorkOfferService` and
  `OfferQueuedVoiceWorkOnAvailabilityHandler` call the selector instead of looping.
- Keep `AssignNextAsync(queueId)` as the only reservation path so locking semantics do not change.

**Tests first.**

- `AgentWorkSelectorTests`: (a) two queues, older item in the second queue → second queue chosen; (b) membership
  priority overrides age; (c) delay prevents selection until elapsed; (d) SLA-aged item beats a newer high-priority
  item when aging is enabled; (e) campaign queues driven by paced dialing are excluded (existing rule).
- Extend `AgentWorkspaceRoundTripBudgetTests` with a budget for the selector (one grouped query for N queues).
- `QueuedVoiceWorkOfferServiceTests`: replaces the loop-order assertions with selector-driven assertions.

**Implementation steps.** Add the model and migration; implement the selector; refactor the two callers; expose
priority/delay on the agent edit screen (`AgentProfileDisplayDriver`) and the queue sign-in dialog; document in
`agents-queues-dialer.md`.

**Acceptance.** With two queues and one agent, the oldest call is always offered first; a supervisor can raise a
queue's priority for an agent and see the order change without a restart.

### A2. Skills with proficiency and time-based relaxation (High)

**Evidence.** `AgentProfile.Skills` and `ActivityQueue.RequiredSkills` are string tags (`SkillTag`). Matching is
all-or-nothing in `RequiredSkillsRoutingStrategy`. There is no proficiency, no preferred-versus-required distinction,
and no relaxation as wait time grows.

**Target design.**

- `AgentSkill` value: `SkillId`, `Proficiency` (1–5). `QueueSkillRequirement`: `SkillId`, `MinimumProficiency`,
  `Required` (bool), `RelaxAfterSeconds` (nullable). Store on the existing models with a migration that converts tags
  to proficiency 3 / required / no relaxation.
- `RequiredSkillsRoutingStrategy` filters on required skills whose relaxation window has not elapsed for the item;
  a new `PreferredSkillsRoutingStrategy` (order 40) adds score for preferred skills and for proficiency above minimum.
- Routing decision reasons (already recorded in `ActivityRoutingDecisionEventData`) include which requirements were
  relaxed, so supervisors can see why an under-skilled agent received a call.

**Tests first.** `RequiredSkillsRoutingStrategyTests` (proficiency threshold, relaxation by wait time, legacy tag
compatibility), `PreferredSkillsRoutingStrategyTests`, `SkillTagMigrationTests` (tags → proficiency), and an
integration scenario in `ActivityRoutingServiceTests` that shows a relaxed requirement after the window.

**Acceptance.** A queue can require "Spanish ≥ 3" and prefer "Billing"; after the configured wait the requirement
relaxes and the decision log explains it.

### A3. Remove N+1 queries and reuse availability counts in strategies (Medium)

**Evidence.** `AgentAvailabilityService.GetForQueueAsync` already batches active-interaction counts
(`CountActiveByAgentIdsAsync`), but `ActivityAssignmentService` passes only `AgentProfile` instances to the routing
service, so `CapacityRoutingStrategy` and `LeastBusyRoutingStrategy` call `CountActiveByAgentAsync` once per candidate
(two round trips per candidate per offer). `LeastLoadedSmsRoutingStrategy` has the same shape for SMS.

**Target design.** `ActivityRoutingCandidate` carries the `AgentAvailability` (active count, last heartbeat, idle
since) it was built from; strategies read from the candidate. `IActivityRoutingService.SelectAgentAsync` takes
`IEnumerable<AgentAvailability>`.

**Tests first.** A round-trip budget test asserting that routing a queue with N candidates issues a constant number
of queries; strategy unit tests updated to construct candidates with counts.

**Acceptance.** Budget test green on SQLite and PostgreSQL (extend the operations-gates workflow as done for the
agent-workspace poll).

### A4. Idle-time semantics for Longest Idle and Round Robin (Medium)

**Evidence.** `LongestIdleRoutingStrategy` orders by `AgentProfile.PresenceChangedUtc`; an agent who has taken ten
calls without toggling presence looks "idle since 9 am". `RoundRobinRoutingStrategy` uses `LastAssignedUtc`, which is
set on reservation, not on completed work.

**Target design.** Add `AgentProfile.IdleSinceUtc`, set when the agent transitions to Available with no active
interactions and when `CompleteWork`/wrap-up ends; `ContactCenterWorkStateManager` and the presence manager own the
write. Longest Idle sorts on it; Round Robin sorts on the last **completed** assignment.

**Tests first.** `AgentPresenceManagerServiceTests` and `ContactCenterWorkStateManagerTests` for the write points;
strategy tests for the ordering.

### A5. Make assignment and reservation timings options, and de-duplicate the client-driven scan (Medium)

**Evidence.** `ActivityAssignmentService` (10 s lock timeout / 30 s expiration) and `ActivityReservationService`
(10 s / 30 s / 50 ms reclaim wait) hard-code timings although `production-support.md` states "timings are
configuration, not constants". `QueuedVoiceWorkOfferService` documents that "the soft phone re-runs this scan roughly
once a second"; each run loads and heals the agent, saves changes, scans the direct-routing queue, and runs
`OfferNextAsync` per queue, each of which acquires the queue lock and performs a reclaim pass. Server push already
exists (`OfferQueuedVoiceWorkOnAvailabilityHandler`, `ReofferVoiceWorkHandler`).

**Target design.** Move the timings into `ContactCenterCoordinationOptions` with validation (the class already
carries inbound lock timings). Make the client scan a **fallback**: trigger on connect, reconnect, presence change,
and offer completion, with a configurable long interval (default 30 s) instead of continuous polling; server-side,
debounce per agent with a short lease so concurrent syncs collapse.

**Tests first.** `ContactCenterOptionsValidationTests` for the new options; `QueuedVoiceWorkOfferServiceTests` for
the debounce (second call within the lease returns without scanning); a Playwright assertion that the soft phone
issues at most one sync per trigger.

### A6. Queue limits, in-queue treatment and queued callback (High)

**Evidence.** No maximum wait or queue size; no in-queue announcements, position, or estimated wait; no hold music
orchestration; no "press 1 to keep your place" callback. `CallbackService` exists but is only used for after-hours
handoff and dialer callbacks. Overflow is a single hop (`OverflowQueueId`) and is evaluated by
`ReservationExpiryBackgroundTask` once per minute, so a 20-second overflow threshold behaves like 60–80 seconds.

**Target design.**

- `ActivityQueue` gains `MaxWaitSeconds` with `MaxWaitAction` (Voicemail, Callback, Overflow, Transfer to external
  catalog entry, Hang up with message), `MaxQueueSize` with `QueueFullAction`, and an ordered `OverflowTargets`
  list (queue id + after-seconds) replacing the single overflow field (migration keeps existing values).
- `IQueueTreatmentProvider` in Contact Center Abstractions with a Telnyx implementation using `speak`, `playback_start`
  (hold music/announcements) and `gather` (DTMF) on the waiting caller leg. Treatment steps are a small ordered
  policy on the queue: welcome, periodic announcement every N seconds with optional position/estimated wait, and
  a callback offer with a DTMF key.
- `IEstimatedWaitTimeCalculator` using the metric store: average handle time × position ÷ available agents, with a
  floor and a cap, exposed to the supervisor dashboard and to treatment.
- Queued callback: DTMF selection removes the item, schedules a `CallbackRequest` at the head of the queue with
  the original enqueue time (place-in-line preserved), and the existing promotion task dials the customer and
  offers the connected call to an agent using the outbound bridge.
- Overflow evaluation moves from the minute sweep to a due-time scheduler: `QueueItem.NextTreatmentDueUtc` and
  `OverflowDueUtc` computed on enqueue, evaluated by a task that runs every 5–10 seconds or by the offer path when
  it touches the item.

**Tests first.** `QueueTreatmentPolicyTests` (step ordering, announcement cadence, DTMF mapping),
`EstimatedWaitTimeCalculatorTests`, `QueuedCallbackTests` (place-in-line, promotion, offer), `OverflowSchedulerTests`
(multi-hop, no loops via `OverflowHistory`, precision within 10 s), and Telnyx `QueueTreatmentProviderTests` using a
recorded HTTP handler. Extend `ContactCenterFeatureDependencyArchitectureTests` so the treatment provider lives
under the Voice feature.

**Acceptance.** A caller in a queue hears music, hears their position every 30 s, presses 1 for a callback, and is
called back and connected without losing their place; an item overflows within 10 s of its threshold.

### A7. IVR and DTMF menus on entry points (High)

**Evidence.** `ContactCenterEntryPoint` maps a number to one queue or agent. There is no menu, no language
selection, no business-hours prompt, no caller identification prompt. `EntryPointRoutingPlanner` is a pure
function over the entry point and open/closed state.

**Target design.** Add an optional `IvrFlow` to the entry point: a small declarative tree (prompt text or media,
digit → action) where actions are route to queue, route to agent, voicemail, external transfer catalog entry,
repeat, or sub-menu, with a max-retries fallback. Execution is a state machine driven by provider `gather` events
(`call.gather.ended` for Telnyx) persisted on the interaction's technical metadata, so it survives restarts and is
idempotent per event delivery. Keep the existing "no IVR" path as a flow with a single implicit action so nothing
changes for current tenants. Provide an Orchard Workflows activity bridge later; do not start with Workflows because
the timing-sensitive gather loop must not depend on the workflow store.

**Tests first.** `IvrFlowStateMachineTests` (property-based like `CallStateMachinePropertyTests`: duplicate and
out-of-order gather events converge), `EntryPointRoutingPlannerTests` extended with menu outcomes, and a
`TelnyxIvrProviderTests` mapping of `gather` commands.

### A8. Caller-based priority and VIP handling (Medium)

**Evidence.** Priority comes only from the entry point or the queue default. Contacts resolved through
`IInboundContactLookup` are attached to the activity but do not influence priority.

**Target design.** `IInboundPriorityContributor` chain (contact tag or field, campaign, repeat caller within N hours,
callback returning) that adjusts the enqueue priority; decisions recorded in the routing decision event.

**Tests first.** Contributor unit tests and an inbound processor test that a VIP contact enqueues as Urgent.

### A9. Transfers and consults from the agent surface (High)

**Evidence.** `ContactCenterTransferService.TransferAsync` and `TransferDestinationResolver` exist and enforce the
external catalog, but `voice-routing.md` states the catalog "does not currently constrain any transfer an agent can
actually initiate" because the soft phone's transfer field goes straight to the provider. `ConsultCall` models exist;
no attended-transfer or transfer-to-queue control is exposed to agents through Contact Center.

**Target design.** All agent-initiated transfers flow through `IContactCenterTransferService` when Contact Center
Voice is enabled: blind and attended (consult, then complete or cancel), to an agent, a queue (re-enqueue with
transfer history), or an approved external destination. The soft phone transfer UI lists agents, queues and catalog
entries and never accepts a raw number when the catalog is enforced. Telephony's `DefaultTelephonyService.TransferAsync`
delegates to an `ITransferPolicy` that Contact Center replaces.

**Tests first.** `ContactCenterTransferServiceTests` for each target type and the attended lifecycle;
`TelephonyCallControlBoundaryTests` extended to prove the soft phone cannot bypass the policy; a Playwright flow for
consult-then-complete.

### A10. Predictive dialing: implement or hide (Medium)

**Evidence.** `DialerMode.Predictive` is in the enum and on the profile editor; `DialerStrategyResolver` returns
null for it ("blocked"). `DefaultDialerAbandonmentPolicyService` already computes abandonment statistics.

**Target design.** Either implement `PredictiveDialerStrategy` (pace = available agents × over-dial ratio bounded by
the abandonment policy, with the mandatory safe-harbor message on abandoned calls and the 2-second connect rule
surfaced in reports) or remove Predictive from the editor and validate it away in the recipe step. The plan
recommends hiding it in P1 and implementing it in P3 with the abandonment policy as a hard gate.

**Tests first.** Extend `DialerModeIntegrationTests` with a predictive scenario asserting the abandon-rate ceiling
throttles pacing.

### A11. Entry point resolver chain semantics (Low)

**Evidence.** `InboundVoiceCallProcessor` uses `_entryPointResolvers.FirstOrDefault()`, so a second registered
resolver is silently ignored. Either it is a single service (register one) or a chain (first non-null plan wins).

**Target design.** Make it a chain ordered by `Order`, first plan wins; add an architecture test that every
`IEnumerable<T>` injected into Core services is iterated, not `FirstOrDefault`-ed (see workstream E).

## Workstream B — AI-to-agent handoff

The shared contract is `IOmnichannelHandoffService` with `VoiceAgentHandoffService` (Contact Center) and
`SmsAgentHandoffService` (SMS Workspace); the AI signals a handoff through `TransferToAgentTool`, which writes into
the static `OmnichannelHandoffTurnContext` (`AsyncLocal`), read by the SMS and voice conversation handlers after the
completion.

### B1. Tool name mismatch (High, trivial)

**Evidence.** `OmnichannelHandoffHelper.BuildHandoffInstructions` tells the model to "call the transfer_to_agent
tool"; the registered tool name is `OmnichannelHandoffHelper.TransferToAgentToolName = "transferToLiveAgent"`. Models
usually recover, but weaker or stricter models will not call a tool that does not exist.

**Tests first.** `OmnichannelHandoffHelperTests`: the instructions contain the exact registered tool name.

### B2. Replace the ambient AsyncLocal handoff context with an explicit, scoped decision (High)

**Evidence.** `OmnichannelHandoffTurnContext` is static `AsyncLocal` state written by the tool and read after the
completion returns. It is invisible in signatures, not testable in isolation without the static scope, and fragile
under any completion pipeline that changes execution context (parallel tool calls, background continuation).

**Target design.** A scoped `IOmnichannelHandoffTurn` service registered per conversation turn and passed to the tool
through `AIFunctionArguments.Services` (already available to `TransferToAgentTool.InvokeCoreAsync`). The SMS and voice
handlers create the turn through a factory, run the completion, and read the decision from the same instance.
Delete the static class.

**Tests first.** `TransferToAgentToolTests` rewritten against the scoped service; handler tests assert the decision
flows without the static.

### B3. Voice handoff correctness and agent context (High)

**Evidence (`VoiceAgentHandoffService`).**
- Rewrites `activity.Source = Inbound` and creates the interaction with `Direction = Inbound` for an **outbound**
  AI call, so campaign and direction reporting lose the origin.
- Optional services (`IBusinessHoursGate`, `ICallbackService`) are resolved through `IServiceProvider`.
- Idempotency relies on the activity status read at the start; two redelivered `speak.ended` events processed on two
  nodes can both pass the guard before either persists. There is no lock keyed on the activity or provider call.
- `request.Summary` and `request.Transcript` are ignored for voice; the agent receives a call with no context,
  while the SMS path imports the transcript and summary.
- After-hours callback is scheduled but nothing in the service speaks to the caller; the AI handler must be
  verified to announce the outcome.

**Target design.**
- Preserve `Source`/`Direction`; add `ActivityKind.Call` + `AiEscalated` (already set) as the reporting discriminator.
- Serialize with the existing inbound lock key (`ContactCenterInboundVoice:{provider}:{callId}`) through
  `IDistributedLock`.
- Store handoff context on the interaction (`HandoffSummary`, `HandoffReason`, transcript reference to the AI session)
  and surface it in the agent bar/workspace screen-pop and in the soft phone incoming-offer panel.
- Inject optional services via null-object registrations owned by their features (see E2).
- Return a `HandoffDisposition` the AI handler must speak (routed, waiting with position, callback scheduled) and add
  a test that each disposition has a spoken outcome.

**Tests first.** New `VoiceAgentHandoffServiceTests` (none exist today): outbound origin preserved, concurrent
redelivery yields one enqueue, context stored, after-hours path schedules one callback and concludes the activity,
closed queue without calendar routes normally.

### B4. SMS handoff must honor routing mode, business hours and queue existence (High)

**Evidence (`SmsAgentHandoffService`).** Always sets `AssignmentStatus = Unassigned` under the queue owner; it does
not check that the queue exists, does not consult `SmsEndpointRoutingSettings.DistributionMode`, does not push-assign
in Routed mode, does not gate on business hours, and duplicates transcript import and preview building.

**Target design.** Route the handed-off conversation through the same router that inbound messages use (see C7,
`ISmsConversationRouter`), with a `HandoffContext` so the router can prefer the queue named by the flow; apply the
after-hours policy (auto-reply text and hold in pool). Emit the same `SmsAssignmentNotification` the inbox expects.

**Tests first.** `SmsAgentHandoffServiceTests` extended: routed queue push-assigns to least-loaded agent; missing
queue fails clearly; closed hours holds and auto-replies; existing thread keeps history.

### B5. Handoff KPIs and supervisor visibility (Medium)

**Evidence.** `HandoffContainmentAggregator` computes containment; nothing exposes time-to-pickup, abandonment
after handoff, or handoff reasons in the supervisor dashboard or report catalog.

**Target design.** Add `HandoffRequested`, `HandoffRouted`, `HandoffPickedUp`, `HandoffAbandoned` interaction events
with reason codes; a report provider "AI escalations" (volume, reasons, median pickup, abandonment) and dashboard
tiles. Reuse the metric delta store.

**Tests first.** Report provider tests following `ContactCenterReportingServiceTests`; event upcaster tests for the new
event types.

### B6. Failed automated activities must not block human threads (High, small)

**Evidence.** `SmsInboundProcessor.ProcessAsync` returns null (drops the message for the human inbox) when an
automated activity exists whose status is not Completed or Cancelled. `ActivityStatus` also has `Failed` and
`Purged`; a failed AI activity therefore blocks every later text from that number forever.

**Target design.** Treat any terminal status as not owning the number; expose `ActivityStatusExtensions.IsTerminal`
in Omnichannel Core and use it in both places (the AI handler has its own terminal list).

**Tests first.** `SmsInboundProcessorTests`: failed and purged automated activities do not suppress a human thread.
