---
sidebar_label: Agents, Queues & Dialer
sidebar_position: 1
title: Agents, Queues, Routing, and Dialer
description: Contact Center agent presence, queues, skill-aware routing, reservations, availability-based assignment, and voice-routed outbound dialing.
---

This phase adds the operational core of the Contact Center: agent presence, work queues,
reservations, skill-aware routing, availability-based assignment, and an outbound dialer that routes
voice calls through Contact Center Voice providers. Each capability is a separate, feature-gated
module so tenants enable only what they need.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, presence, capacity, skills, and queue/campaign sign-in. |
| Contact Center Queues | `CrestApps.OrchardCore.ContactCenter.Queues` | Managed skills, business-hours calendars, work queues, queue items, reservations, policy-based routing, and availability-based assignment. |
| Contact Center Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound profiles, pacing, and dialer activity batches routed through Contact Center Voice. |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | SignalR hub, live agent sessions with heartbeat and stale-session cleanup, and real-time presence, offer, and queue broadcasts. |
| Contact Center Reports & Analytics | `CrestApps.OrchardCore.ContactCenter.Analytics` | Reports area under Interaction Center with call insights, agent productivity, queue usage, and campaign/subject progress reports plus CSV exports. |
| DialPad Contact Center Voice | `CrestApps.OrchardCore.DialPad.Dialer` | DialPad implementation of the Contact Center voice provider boundary. |

## Agents and presence

An **agent profile** links an Orchard user to Contact Center configuration: display name, capacity,
administrator-assigned skills, queue membership, campaign membership, and live presence. Presence
states include `Offline`, `Available`, `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`,
`AfterHoursUnavailable`, and system-managed states such as `Reserved`, `Busy`, and `WrapUp`.

Agents sign in from the floating Telephony soft phone. When the Contact Center queues feature is
enabled, Contact Center contributes a **Work** tab where agents select the queues and campaigns they
want to receive work from and sign out. Signing in sets presence to `Available`; signing out sets it
to `Offline`. The `SignIntoQueues` permission grants self-service sign-in.

Presence is a dropdown in the soft-phone header so agents can change availability without switching
tabs. **Request break** is system-approved: if no assignment is in progress, the request is granted
immediately and the agent enters `Break`; if a route/reservation is already in progress, the request is
kept pending while the call continues, and the system grants `Break` automatically when that in-flight
work is released. Agents in `RequestBreak` or `Break` are not eligible for new routing decisions.

## Agent state reason codes

Administrators define **reason codes** from **Interaction Center → Agent states** so agents pick an
auditable, standardized reason when they go not ready. A reason code has a unique name, an optional
description, the presence state it places the agent in (`AppliesTo` — `Break`, `Away`, `DoNotDisturb`,
`Meeting`, `Training`, or `AfterHoursUnavailable`), a sort order, and an enabled flag. The catalog is
managed with the same display-driver CRUD pattern as Skills and queues, and the `ManageContactCenterAgents`
permission gates it.

When reason codes are configured, the soft-phone presence dropdown lists them (ordered by sort order)
in place of the fixed not-ready states; selecting one sets the agent's presence to the reason's
`AppliesTo` state and records the reason on the agent profile and the `AgentPresenceChanged` event.
If no reason codes exist, the dropdown falls back to the built-in not-ready states.

The Agents feature seeds a standard set of reason codes at setup (short break, lunch, away from desk,
team meeting, training, coaching, and system issue) by running the `agent-state-reason-codes` module
recipe. Reason codes are also importable through the `AgentStateReasonCode` recipe step so they can be
seeded or moved between tenants in deployment recipes.

## Skills

Administrators manage routeable capabilities from **Interaction Center → Skills**. A skill has a
unique name, description, and enabled state. Enabled skills appear in admin assignment surfaces and
queue editor selectors; disabled skills remain on existing agents and queues but are hidden from new
selections. Agents do not self-select skills from the soft phone because skills are routing
eligibility data owned by supervisors/administrators.

Queues can require one or more skills. Agents must have every required skill assigned on their agent
profile to be eligible for that queue, and the default routing strategy filters out agents missing any
required skill before longest-idle scoring runs.

## Queues, reservations, and assignment

A **queue** holds activities waiting for an agent, with a default priority, an SLA threshold, required
skills, an optional inbound channel endpoint mapping, a reservation timeout, a routing policy, an
optional business-hours calendar, and optional overflow settings. Activities enter a queue as **queue
items**; the system pairs the highest-priority, oldest waiting item with an eligible available agent
signed in to that queue and creates a short-lived **reservation**.

Routing is strategy-based. The strategy chain first rejects agents that do not have every required
queue skill, then rejects agents that are already handling their maximum number of concurrent
interactions, then applies the queue's selected scoring strategy. Each assignment publishes an
auditable routing-decision event that records the queue item, selected agent, candidate scores, and
reasons, so later supervisor and analytics features can explain why work was offered to an agent.

### Routing policy

Each queue selects a primary **routing strategy** that decides which available, eligible agent receives
the next item:

- **Longest idle** (default) — offers work to the agent who has been available the longest.
- **Round robin** — distributes work fairly by offering to the agent who least recently received an
  assignment (tracked on the agent's `LastAssignedUtc`, stamped when a reservation is created).
- **Least busy** — offers work to the agent currently handling the fewest active interactions.

Only the selected strategy scores candidates; the other primary strategies stay inert for that queue.

When a queue enables **prefer sticky agent**, routing boosts the eligible candidate who most recently
owned the activity (captured from the activity's assigned user when it is enqueued), so returning work
prefers the agent the customer already worked with. The sticky preference is additive and never
overrides skill or capacity eligibility.

When a queue enables **SLA aging**, a waiting item's effective priority increases by one step for every
SLA-threshold interval it waits beyond the threshold, so aging work is routed ahead of newer
higher-priority work instead of starving.

Agent capacity is enforced during candidate selection. Each agent profile defines
`MaxConcurrentInteractions` (default `1`), and the capacity routing strategy counts the agent's active
(not ended and not failed) interactions before they can be offered new work, so an agent is never
offered more concurrent interactions than they are configured to handle.

### Business hours and overflow

A queue can reference a reusable **business-hours calendar** (managed from **Interaction Center →
Business hours**). A calendar defines a time zone, a weekly open window per day, and all-day holiday
dates. While the calendar reports the queue closed, assignment pauses. The queue's **after-hours
action** decides what happens to waiting items: *Hold in queue* keeps them until the queue reopens, and
*Overflow* moves them to the configured overflow queue.

Independently of business hours, a queue may set an **overflow queue** and an **overflow-after**
threshold. Waiting items that exceed the threshold are moved to the overflow queue so long-waiting work
can be picked up by a broader team. Overflow moves run each minute alongside reservation expiry and
assignment.

A reservation locks the activity for one agent and can be accepted, rejected, canceled, or expired.
The CRM activity moves through `Available → Reserved → Assigned`, mirrored on the queue item and
agent presence. Expired or canceled reservations return the item to the queue automatically. A
background task expires stale reservations and assigns waiting work every minute.

Assignment is concurrency-safe. Each queue's assignment runs under a per-queue distributed lock, so
two nodes — or the reservation-expiry background task running alongside an inbound call — cannot
double-assign the same item or reserve the same agent twice.

## Dialer

A **dialer profile** is an execution policy, not the source of CRM work. Activities, campaigns,
subjects, batches, dispositions, and contact context still come from Omnichannel. The profile tells
the Contact Center how a specific outbound campaign should be dialed: which queue supplies agents,
which dialing mode is used, which Contact Center voice provider places calls, how pacing works, and
how attempts/retries and compliance are bounded. Power and progressive profiles run automatically
each minute: the Contact Center reserves an available agent, evaluates the compliance gate, creates
an outbound interaction, and asks the Voice Contact Center Call Router to place the call. Manual and
preview profiles wait for agent action. Dialer activity batches load **unassigned** inventory the
dialer reserves later.

### Dialing modes and safety

Each automated mode is implemented as a dedicated `IDialerStrategy`, so unsupported modes are
withheld rather than falling through to an unsafe default:

| Mode | Behavior |
| --- | --- |
| `Manual` | The agent chooses and places the call. No automated cycle runs. |
| `Preview` | The agent reviews the activity, then accepts or skips. No automated cycle runs. |
| `Power` | Reserves agents and places a capped number of calls per cycle. **Calls per agent is hard-capped** (`PowerDialerStrategy.MaxCallsPerAgent`) until predictive pacing exists. |
| `Progressive` | Places one call per available agent as agents become available. |
| `Predictive` | **Disabled.** The editor hides it, saving it is rejected, and the dialer refuses to run it until answer-rate forecasting and abandonment controls exist. |

### Outbound compliance gate

Before every attempt, `IDialerEligibilityService` runs and records an auditable `DialSuppressed`
event when an attempt must be blocked. The default gate enforces, in order:

- **Destination present** and the **maximum attempt count** has not been reached.
- **Retry cool-down** - a previous attempt must be older than `RetryDelayMinutes`.
- **Do-not-call / communication preferences** - the contact's `DoNotCall` opt-out (when
  *Respect do-not-call and communication preferences* is enabled).
- **Calling window** - when *Enforce a calling window* is enabled, the contact is only dialed while
  their local time (from the contact's time zone, or the profile's default time zone) is within the
  configured start/end hours.
- **National do-not-call registries** - any registered `INationalDoNotCallRegistry` (for example the
  USA FTC or Canada DNCL registries) is scrubbed when *Respect do-not-call* is enabled.

Do-not-call and registry suppressions cancel the activity; calling-window and cool-down suppressions
release the reservation and leave the activity available for a later cycle. Full calling-window
calendars, abandonment caps, and answering-machine detection are hardened in a later compliance
phase.

### Callback operations

Callbacks use the same Activity, queue, routing, and disposition path as outbound campaign calls. A
`CallbackRequest` records the contact, destination, optional campaign and queue, requested/due window,
attempt count, status, and notes. The callback dispatcher runs every minute and promotes each due
pending callback into an outbound `Callback` activity. When the request has a queue, that activity is
enqueued so the next eligible signed-in agent receives it through the Agent Workspace.

Use callbacks when an agent schedules a later follow-up, an inbound entry point offers a callback
instead of waiting in queue, or workflow automation decides the next best action is a phone callback.
Managers should configure a dedicated callback queue when callbacks need different SLA, skills, or
priority from live inbound calls. Agents handle the promoted callback like any other outbound call:
answer the offer, complete the conversation, select a disposition, and finish wrap-up through the
Subject Flow.

## Voice Contact Center Call Router

The dialer never talks to a telephony platform directly. It calls `IVoiceContactCenterCallRouter`,
which resolves the configured `IContactCenterVoiceProvider`, so the Contact Center keeps assignment,
queue, pacing, and compliance logic while the provider executes call operations. The
`DialPad.Dialer` feature implements `IContactCenterVoiceProvider` over the DialPad telephony
provider; enable it to dial through DialPad.

Voice providers that support contact-center orchestration beyond soft-phone call control can also
register `IContactCenterVoiceProvider`. The `IContactCenterVoiceProviderResolver` resolves those
providers by technical name so future PBX integrations can participate in provider-side queueing,
call assignment, and voice-specific orchestration without coupling Contact Center to one provider.

## Admin UX and extensibility

Contact Center management entries live under **Interaction Center**. Skills, queues, business-hours
calendars, and dialer profile CRUD screens match the Omnichannel Campaigns UI: searchable list pages
render summary shapes, and create/edit screens render display-driver editor shapes with the required
root edit wrapper templates. Agent sign-in and presence are injected into the Telephony soft phone
through `DisplayDriver<SoftPhoneWidget>`, so the operational controls stay with the phone while
management screens remain catalog-focused.

Agent state reason codes are a catalog-backed admin surface (**Interaction Center → Agent states**),
not a provider-specific dialer setting. The Agents feature seeds standard reason codes during tenant
setup by executing the `agent-state-reason-codes` module recipe, and the `AgentStateReasonCode` recipe
step lets reason codes be imported or moved between tenants. A dedicated deployment-plan step is a
planned follow-up.

## Enable via recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.Dialer",
        "CrestApps.OrchardCore.ContactCenter.RealTime",
        "CrestApps.OrchardCore.ContactCenter.Analytics",
        "CrestApps.OrchardCore.DialPad.Dialer"
      ]
    }
  ]
}
```
