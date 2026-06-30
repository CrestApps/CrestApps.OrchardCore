---
sidebar_label: Contact Center
sidebar_position: 0
title: Contact Center
description: Provider-agnostic contact center orchestration for Orchard Core - interactions, queues, routing, presence, and outbound dialing on top of the Telephony and Omnichannel modules.
---

| | |
| --- | --- |
| **Feature Name** | Contact Center |
| **Feature ID** | `CrestApps.OrchardCore.ContactCenter` |

The **Contact Center** module set turns the CRM into a full contact center that agents and
supervisors operate without leaving Orchard Core. It extends the [Omnichannel](../omnichannel/index.md)
CRM instead of introducing a second work model, and it sits between the CRM and the
[Telephony](../telephony/index.md) soft phone: the CRM owns business work data, the Contact Center
owns orchestration, and Telephony owns media execution.

The CRM **Activity** remains the universal unit of work. Activities can be created before an owner
exists, then later reserved and assigned by a dialer, queue, or agent workflow. An **Interaction** is
communication history for a single attempt on that activity - for example a busy call attempt, a
no-answer attempt, or a connected call - and it never owns workflow or disposition.

## Layer boundaries

```text
Omnichannel CRM        Contacts, activities, campaigns, subjects, dispositions, subject actions
        │              universal work item, business context, disposition, workflow
        ▼
Contact Center         Activity queues/reservations, routing, presence, dialer, wrap-up, interactions, metrics
        │              call-control intents, assignment changes, normalized call/session events
        ▼
Telephony              Soft phone, provider resolver, provider call execution and call state
        │
        ▼
Telephony providers    Dial, answer, transfer, hold, resume, conference, hangup, provider webhooks
```

- **CRM (Omnichannel)** answers *who the customer is, which Activity is the work item, which Subject
  defines workflow, and which Disposition was selected*.
- **Contact Center** answers *which activity happens next, which queue owns it, which agent should
  reserve it, when a dial occurs, what communication history exists for the activity, and what
  real-time event the UI should see*.
- **Telephony** answers *how the configured provider executes a call action and what the provider's
  current call state is*.

The Contact Center never handles telephony media and never directly bypasses the CRM disposition
path. It orchestrates behavior, records communication history, and projects operational state.

## CRM activity extensions

Contact Center extends `OmnichannelActivity` with metadata needed by queues and dialers:

- Nullable ownership so preview, power, progressive, and predictive dialing can create activities
  before an agent is selected.
- Activity kind and extensible source metadata so the same Activity model can represent calls, SMS,
  email, meetings, tasks, callbacks, inbound work, workflow-created work, API-created work, and
  dialer inventory.
- Assignment and reservation metadata so multiple dialer or routing instances do not claim the same
  record concurrently.
- Activity batches can load either user-assigned manual work or unassigned dialer work. The batch
  creation dialog selects a source first, then display drivers render source-specific UI.

Dispositions are applied to Activities, not Interactions. Agent, provider, AI, workflow, and system
outcomes converge through the activity disposition service before Subject Actions or workflow
automation runs.

## Capabilities

The Contact Center is delivered as a set of feature-gated modules so tenants enable only the
capabilities they need, similar to how commercial platforms separate licensed capabilities:

- **Interaction management** - communication history for activity attempts.
- **Queues** - inbound, outbound, callback, and dynamic activity queues with priority, SLA, and overflow.
- **Routing** - skills-based, priority, sticky-agent, round robin, longest-idle, and business-hours
  routing with auditable routing decisions.
- **Agents and presence** - agent profiles, real-time presence and reason codes, skills, capacity,
  and queue membership.
- **Voice** - a voice channel adapter over the Telephony module that maps provider calls to
  interactions.
- **Dialer** - outbound manual, preview, power, and progressive dialing driven by CRM activities.
- **Wrap-up** - wrap-up timers, required activity dispositions, CRM activity completion, and
  post-communication automation.
- **Supervision** - live queue and agent monitoring with supervisor call-control intents.
- **Analytics** - queue, agent, and campaign metrics and historical reporting.

Inbound entry points and IVR, call recording, outbound compliance, quality management, an optional
workflow bridge, and AI assistance are additional capabilities on the roadmap.

## Real-time experience

The Contact Center publishes its own real-time event stream over SignalR for agent desktops,
supervisor dashboards, and queue monitors. It does not reuse the Telephony soft-phone hub for
routing, queue, or supervisor data; voice call state continues to flow through Telephony and is
projected into the interaction.

## Agent soft-phone work controls

Agents receive Contact Center work inside CRM-integrated surfaces while the Telephony soft phone
stays the home for availability and call-adjacent actions. When Contact Center is enabled, it adds a
**Work** tab to the floating soft phone where agents sign in to allowed queues and outbound campaigns
and sign out. Presence lives in a dropdown button on the soft-phone header so agents can change
availability without switching tabs. It supports available, request break, away, meeting, training, do
not disturb, after-hours unavailable, and offline states. This avoids a separate sign-in navigation
page and keeps availability changes next to call handling.

Break requests are approved by the routing system, not by another user. If nothing is currently being
routed to the agent, **Request break** is granted immediately as `Break`. If a reservation or route is
already in progress, the request stays pending, the in-flight assignment continues, and `Break` is
granted automatically when that work is released. Agents in request-break or break states are
ineligible for new routing decisions.

Future agent desktop surfaces handle activity offers, accept/reject actions, active CRM activity
context, injected Telephony call controls, interaction history, wrap-up, and required disposition.

Managers configure queue membership, campaign assignment, dialer mode, priority, capacity, and
compliance rules. Inbound queues, callback queues, preview dial queues, power/progressive/predictive
campaigns, and future channels all offer Activities through the same real-time agent-offer model.

The current soft-phone **Work** tab lets agents choose queues and campaigns. Campaigns come from the
Omnichannel Management **Interaction Center** campaign catalog. Routing skills come from
**Interaction Center → Skills**, but they are assigned by administrators/supervisors rather than
self-selected by agents. Skill, queue, and dialer profile admin screens use display drivers and
extensible summary/editor shapes so providers and future desktop panels can extend the model without
replacing the base UI.

## Voice provider integration

The Telephony module continues to own soft-phone call control and media execution. Contact Center
adds optional voice-provider abstractions for PBX providers that can participate in contact-center
orchestration beyond basic call control:

- Dial on behalf of a dialer.
- Assign an existing provider call to an agent after Contact Center chooses the assignment.
- Place or move provider calls in provider-side queues after Contact Center chooses the queue.
- Publish queue events and synchronize PBX presence when supported.

Contact Center remains the brain: the **Voice Contact Center Call Router** selects or receives the
Activity, queue, agent, campaign, dialer mode, and compliance gates, then sends provider-neutral
voice intents to the provider adapter.

Providers register `IContactCenterVoiceProvider` implementations and are resolved through
`IContactCenterVoiceProviderResolver`. The router uses those providers for outbound dialing and
keeps provider-side queue placement and call assignment behind the same voice boundary.

### Call delivery models

Every voice provider declares a **delivery model** so the orchestration layer knows whether it must
bridge media to the agent itself:

- `AgentDeviceNative` - the provider rings the agent's own registered device or soft-phone client (for
  example WebRTC). The live call already reaches the agent, so the Contact Center reserves, offers, and
  tracks the work, and the agent answers the media on their device. The DialPad provider uses this
  model.
- `ServerSideAcd` - the provider parks or queues the live call server-side. The Contact Center
  explicitly asks the provider to connect (bridge) the call to the selected agent through
  `ConnectToAgentAsync` once the offer is accepted (inbound) or the dialed call is answered (outbound).

Providers advertise `ContactCenterVoiceProviderCapabilities.AgentConnect` when they can bridge calls.
The agent desktop and supervisor UI hide or disable actions the active provider cannot perform, the
same way the Telephony soft phone gates controls on `TelephonyCapabilities`.

### Unified call commands

Accepting or declining an offered call is a single, authoritative server-side command rather than
several uncoordinated client actions. `IContactCenterCallCommandService` accepts the reservation,
connects the media to the agent (for `ServerSideAcd` providers), and advances the interaction and call
session together, so the orchestration state and the provider media state can never diverge. The
result tells the soft phone whether the agent's device still has to answer the media
(`RequiresDeviceAnswer` is `true` only for `AgentDeviceNative` providers). Declining rejects the
reservation, returns the work to its queue, and re-offers it to the next available agent.

### Call sessions and normalized provider events

A **call session** (`CallSession`) is the Contact Center's business-oriented projection of a voice
call. It maps a provider call to an interaction, agent, and queue and tracks the normalized call
lifecycle (`Planned`, `Dialing`, `Ringing`, `Connected`, `OnHold`, `Ending`, `Ended`, `Failed`,
`NoAnswer`, `Rejected`, `Canceled`, `Transferred`) plus talk and hold durations, without owning media
execution.

Providers and PBX webhooks feed call-state changes in as normalized `ProviderVoiceEvent` instances
through `IProviderVoiceEventService`. The service matches the event to the interaction and call session
by provider call identifier, advances their state and timestamps, bridges the agent for answered
outbound calls on `ServerSideAcd` providers, and publishes the corresponding Contact Center domain
events. Events that carry an already-seen idempotency key are ignored, so duplicate or out-of-order
webhook deliveries are safe.

## Inbound voice

> **Feature ID** `CrestApps.OrchardCore.ContactCenter.Voice`

The **Contact Center Voice** feature adds the Voice Contact Center Call Router for inbound and
outbound voice work. For inbound calls, it routes provider calls to an available agent and offers them
through the [Telephony](../telephony/index.md) soft-phone incoming-call modal. It depends on the
Queues feature and the Telephony soft phone.

When a normalized inbound call arrives, the feature:

1. Resolves the dialed number to an Omnichannel **channel endpoint**, then resolves the configured
   **subject flow** for that endpoint to obtain the subject content type and campaign.
2. Looks up the **contact** by the caller's phone number (matched against the contact's normalized
   primary cell and home numbers).
3. Creates an `OmnichannelActivity` (`Kind = Call`, `Source = Inbound`) with its **Subject** content
   item, and an `Interaction` (`Voice`, `Inbound`) linked to that activity.
4. Enqueues the activity into the inbound **queue** and reserves the longest-idle available agent who
   is signed in to that queue.
5. Offers the ringing call to that agent through `IIncomingCallDispatcher`, which raises the
   soft-phone modal.

When the agent accepts the offer, the [unified call command](#unified-call-commands) accepts the
reservation, connects the media to the agent for `ServerSideAcd` providers, and creates the
[call session](#call-sessions-and-normalized-provider-events) and marks the interaction connected in
one server-side step.

### Routing the dialed number to a queue

Each queue has an optional **inbound channel endpoint** (`InboundChannelEndpointId`). Calls received
on that endpoint are queued there. When no queue maps the endpoint and exactly one enabled queue has
no endpoint mapping, that queue is used as the default inbound queue, so a single-queue tenant works
without extra configuration.

### Matched customers in the modal

The feature contributes an `IIncomingCallContextProvider` to the Telephony modal. For a ringing
inbound call offered to an agent, it lists the customers matched by the caller's number - each card
links to the contact content item and offers an **Answer & open** shortcut - and wires the accept and
decline offer-lifecycle actions back to the reservation. The agent's signed-in inbound queue scopes
the context shown. See [Incoming calls](../telephony/index.md#incoming-calls) for the modal contract.

### Ingress

A provider or PBX integration posts a normalized `InboundVoiceEvent` to the authenticated ingress
endpoint:

```text
POST /api/contact-center/voice/inbound
```

The endpoint requires the `Manage interactions` permission. Provider-specific webhooks that validate
their own provider signature can instead call `IVoiceContactCenterCallRouter` directly, the same way
the Omnichannel SMS webhook handles inbound messages.

### Shared disposition for inbound and outbound

Both inbound and outbound work is an `OmnichannelActivity` with a Subject, and both are dispositioned
through the single, source-neutral `IActivityDispositionService`. Completing an activity records the
disposition and completion metadata and then runs the configured Subject Actions, so inbound and
outbound calls are wrapped up through the same subject workflow.

## UI extensibility

All Contact Center UI is built with Orchard Core display management: shapes, display drivers,
placement, templates, and shape alternates. The skill, queue, and dialer profile CRUD screens follow
the Omnichannel Campaigns UI pattern: controllers load catalog entries through managers and build
summary/editor shapes with `IDisplayManager<T>`. Activity screens remain Omnichannel screens that
Contact Center augments with display drivers for reservation state, interaction history, dialer
controls, wrap-up, and supervisor decorations.

## Status

The Contact Center is under active, phased development. The first milestone is a voice MVP that
proves agents can run inbound and outbound voice work entirely inside the CRM while preserving the
Telephony boundary. Inbound voice routing and the soft-phone incoming-call modal now ship in the
[Inbound voice](#inbound-voice) feature. This documentation will expand as each capability ships. See
[Agents, Queues & Dialer](agents-queues-dialer.md) for the agent, queue, reservation, and dialer
features.
