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

Phase 1 is built. Each item is unit-tested; the live call checks are still to do.

- The meters: the capture probe is rebuilt on the track actually being sent, and the microphone level is averaged over
  each sampling window.
- Stored records: every leg's end-of-call quality becomes a `CallQualityRecord`, one per source (soft phone or
  provider). A new leg index on call sessions ties it to its interaction, queue and agent, and a leg the contact
  center did not route, such as an extension call, is recorded against the agent alone.
- Telnyx statistics: `call_quality_stats` is read from the hangup, logged, and recorded, including the hidden
  customer leg of an outbound call.
- The report: **Call quality** in the Contact Center reports. It shows ratings, the likely cause of poor calls,
  results by agent and by network path, and each poor call with its measurements.
- The alert: when three of an agent's last five calls in an hour rate poor, a `CallQualityAlertRaised` event is
  published, at most once per agent per hour. Workflows can trigger on it, and the live dashboard shows it to
  supervisors with the likely cause.

Phase 2 is built and unit-tested; indexing the log by aggregate (item 5) is still to do.

- One door: every writer of an agent's state goes through `IAgentStateTransitionService`, which sets the state,
  stamps when it changed, and records `AgentStateChanged`. It never dates a change before the one it follows. The
  presence events writers already published are unchanged.
- Writers: sign-in, sign-out (with its reason, such as signing out of the site), a state set by the agent or a
  workflow (recorded as a workflow actor), reserved, accepted, every release with its reason (expired, rejected,
  canceled, compensated), wrap-up started, work completed, wrap-up timed out, a deferred request taking effect,
  provider reconciliation, and the stale-session sign-off, which is dated by the last heartbeat.
- Reason codes: a reason is matched to a configured code by identifier or by name, and recorded by identifier
  with the name it had at the time. The agent screens still post the name.
- Sessions: `AgentConnected`, `AgentDisconnected` (with the connections left open) and `AgentHeartbeatLost` (with
  the last heartbeat) are recorded. Heartbeats themselves are not.

Phases 3 and 4 are next.
