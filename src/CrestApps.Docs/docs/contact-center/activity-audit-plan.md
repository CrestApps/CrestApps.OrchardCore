---
title: Call quality and activity audit plan
sidebar_label: Activity audit plan
---

# Call quality and activity audit plan

Two things are needed together. The first is tracing sound quality back to its cause in production. The second is an
account of every agent and call state precise enough to pay people from: sign in and out, breaks, ready and not
ready, reserved and ringing, talking, holding, wrap-up, transfers, abandons. Every fraction of a second has to be
accounted for, and a report has to be able to say what an agent was and was not doing at any moment.

## Where things stand

There is already a durable, append-only event log: `InteractionEvent`. It deduplicates retried and redelivered
webhooks by idempotency key and is kept forever by default, and the agent workforce and payroll reports are built from
it. The log is the right foundation. What is wrong is how much reaches it:

- **Agent states.** Reserved, the move to Busy, and every release publish no presence payload, so workforce reports
  count call time as the state before it. Provider reconciliation releases write nothing. Connecting and disconnecting
  write nothing, and the stale-session sweep records the sign-off 90 to 150 seconds after it really happened. Reason
  codes are stored by display name, so renaming one rewrites history. Every event's actor is the agent, even when the
  system or a workflow made the change.
- **Call states.** Offer and queue events carry no interaction id, so they cannot be tied to a call. Nothing is written
  when an offer rings an agent, when a dial starts or fails, when an agent leg answers or fails, when a call is put on
  hold or taken off it, when it is transferred or conferenced, for extension calls, for AI calls, when a call is
  handed to an agent, or when a caller abandons after a handoff. Hold time is never recorded, so talk time includes
  hold.
- **Time.** Provider events are stamped from the webhook signature header, which is whole seconds taken when the
  webhook was signed, instead of from the event's own millisecond `occurred_at`. Most domain events are stamped from
  the server clock at processing time. Call reports do not read the log at all. They compute everything from a few
  timestamps on the interaction, several of which are overwritten when a call is offered again.
- **Quality.** The soft phone reports rich samples and a summary per call, but they are only logged: nothing is
  stored, nothing ties them to a call, and the customer's side is not measured. Telnyx's per-leg quality statistics
  are not read. The outgoing and microphone meters read zero after a mid-call microphone change, because the probe is
  rebuilt against the track that is about to be stopped.

## Principles

1. **One log, append-only.** Every state change becomes an `InteractionEvent` with a typed payload. Nothing is updated
   in place to record history.
2. **Two times on every event.** The time it happened (from the provider when the provider knows it, at full
   precision) and the time it was recorded. Reports use the first; audits use both.
3. **Exactly once.** Every event carries an idempotency key derived from what it describes, so retries, redeliveries
   and replays never produce a second record.
4. **Every writer goes through one door.** Agent state changes go through a single transition method that publishes
   the event, so a new code path cannot change state without recording it.
5. **Reports reconcile.** A timecard's states add up to exactly the time between sign-in and sign-out, and a check says
   so for each agent and day.

## Phase 1: call quality

1. **Fix the meters.** Rebuild the outgoing-level probe after the new microphone track is committed, and measure the
   microphone over each window rather than one instantaneous reading.
2. **Store a quality record per call leg.** The soft phone's end-of-call summary is stored and tied to the interaction
   and the agent through the provider call-control id, which is how a browser leg is known to the call.
3. **Record Telnyx's per-leg statistics** from the hangup event, which covers the customer's side and legs the
   browser never sees.
4. **Call quality report.** Poor calls by agent, device, network path and time, with the call and its cause-relevant
   details.
5. **Alert on repeated poor calls.** When an agent's recent calls keep rating poor, publish an event supervisors are
   told about in real time and workflows can act on, naming the likely cause.

## The shared contract

Phases 2 to 4 are built against one contract, so the writers and the reports agree on every field:

- **One door.** Every agent and call state change is recorded through `IContactCenterAuditRecorder`. It dates the
  event by when the change happened, stamps the time it was recorded (`InteractionEvent.RecordedUtc`), names the
  actor (`InteractionEvent.ActorType`: agent, supervisor, system, workflow, provider, customer, AI agent), and derives
  an idempotency key from what the change is, to the tick.
- **Actor and subject.** Every event names who caused it, and `ActorType` is never left unspecified. An agent or
  supervisor is named by user id, the provider by its technical name, and the platform by `system`. The agent a
  change is about is its subject, not its actor. The subject is carried by the aggregate (`AgentProfile` events)
  or by the payload's `AgentId` and `UserId` (offer, reservation, routing and call payloads), never in `ActorId`.
  Routing, reservation, expiry, timeout release and reconciliation are the platform. Accepting and declining an
  offer are the agent. A call-stream event is the provider. The presence event that goes with a state change
  names the same actor as the state change and is dated by the same instant. Workflows get the subject as
  `AgentId` and `AgentUserId` beside `ActorId` and `ActorType`.
- **Agent state.** `AgentStateChanged` carries `AgentStateChangedEventData`: previous, current and requested state,
  the reason code by id with its name at the time, the source (sign-in, reserved, accepted, released, wrap-up
  started, work completed, wrap-up timed out, reconciled, session expired, request applied), the interaction and
  reservation, and the time it took effect. It is separate from `AgentPresenceChanged` so recording a transition
  never offers work or re-broadcasts presence. `AgentConnected`, `AgentDisconnected` and `AgentHeartbeatLost` carry
  `AgentSessionEventData`.
- **Offers.** `OfferPresented`, `OfferAccepted`, `OfferDeclined`, `OfferExpired`, `OfferMissed` and `OfferCancelled`
  carry `OfferLifecycleEventData`, always with the interaction, and the ring time once settled.
- **Calls.** `CallQueued`, `CallDequeued`, `DialStarted`, `DialFailed`, `AgentLegAnswered`, `AgentLegFailed`,
  `CallAbandoned`, the consult events, the AI call events, the extension call events, and the existing call events,
  all carry `CallLifecycleEventData`: the interaction, session, leg and role, agent, queue, the state change, the raw
  hangup cause, how long the state that ended lasted, and the provider's own time.

## Phase 2: agent state audit

1. One transition method for every presence change, publishing the full payload, with a typed actor (agent,
   supervisor, system, workflow) and source.
2. Record Reserved, Busy, every release and its reason, reconciliation, wrap-up started and ended (and whether a
   timeout ended it), and the real last-heartbeat time on a stale sign-off.
3. Record connect, disconnect and heartbeat loss, and sign-out with its reason.
4. Store reason codes by id, with the name as it was at the time.
5. Index the log by aggregate so a workforce report reads only the window it needs.

## Phase 3: call state audit

1. Tie every offer and queue event to its interaction, and record ringing, accepted, declined, missed and cancelled
   offers.
2. Record interaction creation and routing decisions, dials started and failed, agent legs answered and failed,
   hold and resume with duration, transfers, conference joins and leaves, voicemail, abandons, and the raw hangup
   cause.
3. Record AI calls: answered, the answering-machine verdict, conversation end, and handoff to an agent.
4. Record extension calls.
5. Stamp provider events from their own `occurred_at`.

## Phase 4: reports built on the log

1. Talk time that excludes hold, ring time, true queue wait, and abandons counted as abandons.
2. An agent activity timeline: every state, in order, with durations, for any agent and period.
3. A payroll timecard that reconciles, with the check that proves it.

## Status

All four phases are built, merged and covered by the unit and feature-activation suites. The agent-state and report
paths have also been exercised on the running site, and the call paths wait on a live call.

- **Phase 1, call quality.** Verified live: stored records, Telnyx statistics, the report, and the alert, including
  the supervisor dashboard banner.
- **Phase 2, agent state.** Every transition goes through `IAgentStateTransitionService` and is recorded as
  `AgentStateChanged`. On the running site, a break with a reason code and the return to Available were recorded
  with the reason code's id and name, the agent as actor, the change's own time and a separate recorded time.
  Connections are recorded, and ones a restart left behind are pruned.
- **Phase 3, call state.** Offers, queue visits, dials, agent legs, hold with its duration, transfers, consults,
  abandons, hangup causes, AI calls and extension calls are recorded against their interaction and dated by the
  provider's time. This waits on a live call.
- **Phase 4, reports.** The workforce reports read the new transitions one period at a time through a new
  aggregate index. New reports: the reconciled payroll timecard, call handling (talk time without hold, ring,
  queue wait, abandons) and the agent activity timeline. All 83 reports render on the running site.
- **Found and fixed on the running site.**
  - Orchard's document serializer stored timestamps at whole seconds. The audit timestamps now keep every tick.
  - Agent profiles made on first sign-in had no user name, so reports named agents as unknown.
  - Two connections opening together lost the session version check, so one was aborted.
  - Connection ids outlived server restarts.
  - Routing, reservation and presence events left their actor unspecified and put the agent's id where the
    actor goes. They now name the platform, the agent or the provider, and carry the agent as the subject.
  - Accepted offers were recorded against the interaction, so the offer reports never saw an offer accepted.
    They are recorded against the reservation now, and the reports read the older ones too.
  - A conversation the AI handed to an agent was recorded as ending when the caller hung up on the agent,
    minutes after the AI's session ended. It is dated by the end of the session now, which is also when a
    turn-based call's handoff is recorded. The hangup reports the same moment again, and the first report is
    kept.
- **Answering faster.** The agent's leg is dialled while the offer rings and joined on accept. Hold music stops
  at the bridge, and every client is told at once. This waits on a live call.
