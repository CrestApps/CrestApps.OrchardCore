---
sidebar_label: Voice Routing Architecture
sidebar_position: 3
title: Inbound and Outbound Voice Routing Architecture
description: Technical deep dive into Contact Center inbound and outbound voice routing, provider-truth synchronization, and restart reconciliation.
---

# Voice Routing Architecture

This guide explains how the Contact Center routes **inbound** and **outbound** voice work, how it stays synchronized with the telephony server, and how it recovers when the Orchard Core application or tenant restarts.

The key rule is constant across every flow:

- the **provider/server is the source of truth for live call state**
- Contact Center owns **routing, reservation, assignment, and call-session orchestration**
- the soft phone mirrors the **server-projected state** instead of inventing its own truth

## Network and protocol requirements for real-time voice

Real-time voice depends on both **provider-to-Orchard** and **browser-to-Orchard** connectivity. In production, prefer encrypted transports everywhere, and do not assume the hosting platform allows outbound sockets by default.

| Path | Typical protocol(s) | Direction | Why it is needed |
| --- | --- | --- | --- |
| Browser soft phone ↔ Orchard app | `https` + `wss` | Bidirectional | The soft phone loads over HTTPS and receives live SignalR call-state updates over secure WebSockets. |
| Browser soft phone ↔ Orchard app fallback | `https` | Bidirectional | SignalR may fall back to SSE or long polling when WebSockets are unavailable, so normal HTTPS traffic must also remain allowed. |
| DialPad → Orchard webhook | `https` | Inbound to Orchard | DialPad posts signed call events to Orchard webhook endpoints. |
| Orchard → DialPad REST API | `https` | Outbound from Orchard | Orchard may query DialPad for current call truth or execute provider API operations. |
| Orchard → Asterisk ARI REST API | `http` / `https` | Outbound from Orchard | Orchard uses ARI HTTP(S) for dial, hangup, hold, mute, and per-call state lookup. |
| Orchard → Asterisk ARI event stream | `ws` / `wss` | Outbound from Orchard | Orchard keeps a live ARI socket open so server-side call changes reach the app immediately. |

### Production guidance

1. Use **`https`** for every public webhook or browser endpoint.
2. Use **`wss`** for every production WebSocket connection.
3. Allow plain **`http`** or **`ws`** only for trusted local development or lab environments where TLS termination is handled elsewhere.
4. If a reverse proxy or firewall sits in front of Orchard, it must allow **WebSocket upgrade** requests for SignalR and provider stream listeners.
5. If a reverse proxy or firewall sits between Orchard and Asterisk, it must allow Orchard's long-lived outbound `ws`/`wss` ARI event-stream connection in addition to normal ARI HTTP(S) requests.
6. If the app runs on a host that restricts outbound traffic by default, such as some Azure topologies or locked-down App Service / container-network deployments, explicitly allow Orchard's outbound `https`, `ws`, or `wss` connections to provider APIs and live event streams or the app will not receive real-time provider state.

## The moving parts

| Layer | Owns |
| --- | --- |
| Omnichannel CRM | Contacts, activities, campaigns, subject flows, dispositions |
| Contact Center | Queues, routing, reservations, assignment, interactions, call sessions, supervisor/agent events |
| Telephony | Provider resolution, soft-phone hub, call-control execution, provider call-state lookup |
| Provider | Live call media, native device state, provider webhooks, provider APIs |

## Inbound routing flow

### 1. A provider event reaches Orchard

Inbound voice can enter the Contact Center through one of two server-side paths:

1. A provider or simulator posts a normalized `InboundVoiceEvent` to `POST /api/contact-center/voice/inbound`.
2. A provider-specific webhook adapter or controller translates the provider payload into Contact Center events first.

Examples:

- Generic provider webhook path: `POST /api/contact-center/voice/webhook/{provider}`
- Generic normalized inbound path: `POST /api/contact-center/voice/inbound`
- DialPad built-in path: `POST /api/dialpad/webhook/call`

The provider never pushes state directly to the browser. It always comes into Orchard first.

### 2. Contact Center creates the CRM work item and interaction

`VoiceContactCenterCallRouter` takes the inbound event and:

1. acquires a distributed lock scoped to provider name + provider call id
2. checks for an existing interaction using the same provider-scoped identity
3. resolves the dialed number (`ToAddress`) to the configured phone channel endpoint
4. resolves the matching subject flow and optional CRM contact
5. creates the `OmnichannelActivity`
6. creates the `Interaction`
7. resolves the entry-point plan and target queue

At this stage:

- the **Activity** is the CRM work item
- the **Interaction** is the communication attempt record
- the provider call id is stored on the interaction so later provider truth can find it again

### 3. The activity is queued

If the entry point is open and queueing is allowed, Contact Center enqueues the activity into the resolved `ActivityQueue`.

The queue still owns routing. The provider does not decide which Orchard agent gets the work.

### 4. Assignment selects the next agent

`ActivityAssignmentService` serializes assignment with a **per-queue distributed lock** so multiple nodes or concurrent background tasks cannot assign the same queue item twice.

Inside that lock, it:

1. confirms the queue is enabled and open
2. selects the highest-priority waiting queue item
3. evaluates currently available agents through the routing pipeline
4. creates a pending reservation for the winning agent

This is the single-writer boundary for queue assignment.

### 5. The ringing offer is projected to the agent

`VoiceContactCenterCallRouter.OfferNextAsync()` turns the pending reservation into a ringing offer:

1. loads the reserved agent and linked interaction
2. refreshes the interaction from **provider truth** before offering it
3. if the provider confirms the call no longer exists, removes it from the queue and releases the reservation and agent (via `ProviderVoiceOfferSynchronizationService.ReconcileEndedOfferAsync`), then moves on to the next queued call instead of offering a dead call
4. moves a still-live interaction to `Ringing`
5. builds a telephony `TelephonyCall`
6. dispatches it through `IIncomingCallDispatcher`

Because a queued call is validated against provider truth at offer time, a "zombie" interaction whose provider channel has already disappeared can never be offered to an agent. This prevents the agent from being reserved for a call they can neither answer nor hang up, which would otherwise leave them stuck and unable to receive new inbound calls.

The Telephony module then:

- sends the incoming call to the agent's soft-phone SignalR connections
- persists the telephony interaction history used by the **Recent** tab and reconnect restore

### 6. The agent accepts through one authoritative server command

The workspace and the soft-phone incoming modal both call the same authoritative accept endpoint:

- `POST /Admin/contact-center/voice/offer/accept`

`ContactCenterCallCommandService.AcceptInboundOfferAsync()` then:

1. validates that the reservation is still pending and still belongs to the current agent
2. refreshes the interaction from **provider truth** before accepting
3. rejects the accept immediately if the provider already ended the call
4. accepts the reservation
5. connects media if the provider uses a server-side ACD model
6. if the media connect or answer fails, re-checks provider truth: when the provider confirms the call is gone, the offer is reconciled (removed from the queue and the agent released via `ReconcileEndedOfferAsync`) instead of leaving the accepted reservation stuck; only a still-live call is re-offered
7. leaves device-native providers in `Ringing` until the provider later reports `Connected`
8. creates or updates the `CallSession`
9. publishes Contact Center events such as `OfferAccepted`

This is why the UI does not get to decide that the call is connected just because the user clicked **Accept**.

### 7. Provider truth finishes the state transition

After the provider actually changes call state, the provider webhook or provider call-state lookup drives the next authoritative transition:

- `Ringing` → `Connected`
- `Connected` → `OnHold`
- `OnHold` → `Connected`
- `Connected` → `Ended`
- and so on

`ProviderVoiceEventService` projects those transitions into:

- the durable `Interaction`
- the durable `CallSession`
- Contact Center domain events
- the soft phone through server-side projections

If a persisted interaction references a provider name that is no longer registered, restart and healing reconciliation retry the provider call id through the tenant's current default provider. A confirmed missing call is terminalized and its queue/agent state is released; a call the provider still reports as active remains assigned and is never requeued merely because the agent resets queue membership.

When the browser receives a terminal event for a different call id than the call currently displayed, it immediately asks the server for the provider-authoritative active call. This preserves a genuinely newer call while clearing a stale browser call that no longer exists.

## Outbound routing flow

### 1. A dialer profile starts a cycle

`DialerService` runs automated outbound work only for automated modes such as **Power** or **Progressive**. It first confirms that a Contact Center voice provider can route outbound calls for the dialer profile.

### 2. The dialer strategy reserves work

The selected `IDialerStrategy` picks how many attempts to launch for the cycle. Each attempt reserves:

1. the next eligible queue item/activity
2. an available agent

The reserved activity is still the CRM work item; the reservation only gives the dialer a temporary right to place the attempt.

### 3. Compliance is checked before every attempt

`DialerAttemptService` runs `IDialerEligibilityService` before dialing.

That check enforces rules such as:

- destination exists
- maximum attempts has not been reached
- retry cool-down has elapsed
- do-not-call and national registry suppression
- configured calling window

If the attempt is suppressed:

- the reservation is canceled
- the activity status is updated when appropriate
- a `DialSuppressed` event is published for auditability

### 4. Contact Center creates the outbound interaction

If the attempt is eligible, Contact Center creates a new outbound `Interaction` before the provider dial occurs.

This means the routing/orchestration record exists before live provider events begin to arrive.

### 5. The provider dials

`VoiceContactCenterCallRouter.RouteOutboundAsync()` resolves the configured `IContactCenterVoiceProvider` and calls its dial method.

For the current built-in DialPad provider:

- Contact Center owns the outbound attempt
- DialPad executes the actual dial through the Telephony provider
- the provider returns a provider call id

If the provider does not return a call id, the attempt is treated as a failure because Contact Center cannot reconcile a call it cannot identify later.

### 6. Reservation acceptance and ringing state are persisted

If the provider dial succeeds:

1. the reservation is accepted
2. the interaction moves to `Ringing`
3. the provider call id is saved on the interaction
4. the activity moves into `Dialing`

If the reservation can no longer be accepted by the time the provider succeeds, Contact Center fails the attempt and tears down the temporary state rather than letting the call continue without valid routing ownership.

### 7. Provider truth drives answer, bridge, and completion

The next transitions come from provider truth:

- provider reports answer/connected
- Contact Center updates the interaction and `CallSession`
- for server-side ACD providers, Contact Center can bridge the live call to the agent
- provider reports terminal state
- Contact Center ends the interaction/session and moves the agent into the normal completion path

## How Contact Center stays synchronized with the server

Synchronization is built around **normalized provider truth** plus **reconciliation**.

### Normalized provider events

Providers translate their native events into `ProviderVoiceEvent`.

That contract carries the authoritative server-side facts Contact Center cares about, including:

- provider name and provider call id
- normalized call state
- addresses
- mute state
- recording state
- conference state
- idempotency key

`ProviderVoiceEventService` ingests those events idempotently and updates the durable interaction/session projection. Provider name and call id are queried together first so identical call ids from two providers cannot collide. If an interaction was stamped with a provider identity that is no longer registered, the service can fall back to the provider call id, canonicalize the stored provider identity to the live event source, and continue terminal projection instead of silently losing the event. The fallback is rejected when the stored provider is still active, preserving provider-scoped ownership when two backends can produce the same call id. The service rejects stale events, never permits a nonterminal event to reopen a terminal call id, and gives every semantic event derived from one provider delivery its own idempotency key while keeping the base key on `CallSessionUpdated` for replay detection.

When an answered inbound call reaches a terminal provider state, Contact Center moves the agent into `WrapUp` and marks the assigned queue item `Completed`. The accepted reservation and CRM activity remain as audit and after-call-work records until the normal disposition/completion flow releases the agent to the requested ready state. Pre-answer terminal calls still follow the abandon path, which removes the queue item, cancels the reservation, releases the CRM assignment, and restores the agent immediately.

### Durable event delivery

Every Contact Center domain event is persisted together with a pending outbox message before handler fan-out begins. Successful handlers are checkpointed individually, so a retry after a partial failure runs only the handlers that did not complete. Pending messages survive tenant/application restarts, retry with exponential backoff, and dead-letter after the configured attempt limit instead of silently disappearing between event persistence and real-time/workflow projection.

### Provider call-state lookup

When a provider implements `ITelephonyCallStateProvider`, Contact Center can actively ask:

> "What is the current truth for provider call `<id>` right now?"

That lookup is used for:

1. **pre-accept validation** so an ended call cannot still be accepted
2. **tenant-startup reconciliation** after a restart
3. **periodic safety reconciliation** in case a live event was missed or delayed

### Ended-offer reconciliation

If provider truth says a ringing call ended before it was actually answered, `ProviderVoiceOfferSynchronizationService` clears the stale routing state:

- queue item
- reservation
- agent active reservation/presence
- activity assignment metadata

That prevents abandoned or already-ended calls from being re-offered as ghost work.

Ended-offer reconciliation only runs when a real non-terminal → terminal transition is observed. When a reconciliation sweep discovers a call that already disappeared on the provider **before any call session was ever recorded**, `ProviderVoiceEventService` seeds the newly created session with the interaction's pre-event (non-terminal) state rather than the incoming terminal state. This preserves the non-terminal → terminal transition so the `CallEnded` event is still published and the ended-offer cleanup runs; without the seed the session would be created already-terminal and the cleanup would silently never fire, leaving the interaction stuck in the queue.

### Soft-phone projection stays server-driven

The soft phone sends intents such as:

- accept
- decline
- hold
- resume
- mute
- hang up

But it does not become the system of record for live call state.

The durable truth is:

1. provider event or provider lookup
2. Contact Center interaction and call-session update
3. Telephony/Contact Center server projection
4. UI refresh from the resulting server state

## What happens during an application or tenant restart

The current design assumes that a short restart can happen during active traffic and must not permanently desynchronize routing.

### Tenant activation reconciliation

When the tenant activates, `ContactCenterVoiceTenantEvents` immediately runs a reconciliation pass across active provider-backed interactions.

For each active interaction, Contact Center:

1. resolves the telephony provider
2. asks for the current provider call state
3. rebuilds a normalized provider event from that lookup
4. re-ingests it through the same `ProviderVoiceEventService` pipeline

If the provider says the call no longer exists, Contact Center treats it as terminal and clears stale local state.

### Periodic reconciliation

`ProviderCallStateReconciliationBackgroundTask` runs every minute as a safety net.

This catches cases where:

- an app restart happened between two provider events
- a provider event was delayed
- a live stream or webhook delivery was missed

Bulk reconciliation is serialized by a distributed lock. A provider live-stream reconnect requests a provider-scoped pass, so reconnecting one Asterisk endpoint does not repeatedly query unrelated providers or overlap another full reconciliation sweep.

### Re-offer and reconnect recovery

When an agent becomes available again or reconnects, Contact Center can re-check waiting voice work and offer it again. Before it does, the healer/reconciliation path clears impossible leftovers so stale reservations do not block future offers.

## Why the current implementation is resilient

The current voice flow stays consistent because it combines these protections:

1. **Per-queue, per-agent, and per-reservation distributed locks** prevent double assignment and accept/expiry races.
2. **Provider-scoped inbound locks and lookups** prevent duplicate work and cross-provider call-id collisions.
3. **Reservations** make offers explicit and auditable.
4. **Provider call ids** let Contact Center correlate server truth back to local interactions.
5. **Ordered, terminal-safe provider-event ingestion** prevents stale or duplicate deliveries from corrupting state.
6. **Durable per-handler outbox delivery** prevents events from disappearing across handler failures or restarts.
7. **Pre-accept provider refresh** stops agents from accepting already-ended calls.
8. **Ended-offer reconciliation** clears stale queue and agent state immediately.
9. **Tenant-startup, reconnect, and periodic reconciliation** repair drift after restarts or missed events.
10. **Server-driven soft-phone projection** keeps the browser as a mirror instead of a source of truth.

## Current limitations and important notes

- `InboundVoiceEvent.ToAddress` must be present for generic inbound routing because the router needs the dialed service address to resolve the entry point or queue.
- If multiple enabled queues have no explicit inbound mapping, the generic fallback queue resolution intentionally does not guess between them.
- DialPad currently uses the **agent-device-native** delivery model. Contact Center does not bridge media for it; the provider rings the agent's registered device and later tells Contact Center what really happened.
- DialPad webhook subscriptions are currently created and monitored in the DialPad administration portal; Orchard validates deliveries but does not automatically register or health-check the provider subscription.
- Asterisk and other server-side ACD providers can use server-driven answer/bridge flows instead.
- Reconciliation currently repairs **known local provider-backed interactions**. It does not yet bootstrap a completely unknown live provider call that never got a local interaction before the restart window.

## Related guides

- [Contact Center overview](index.md)
- [Agents, queues, and dialer](agents-queues-dialer.md)
- [Agent desktop and supervisor dashboard](agent-desktop.md)
- [Telephony soft phone](../telephony/index.md)
- [Custom telephony and Contact Center providers](../telephony/custom-providers.md)
