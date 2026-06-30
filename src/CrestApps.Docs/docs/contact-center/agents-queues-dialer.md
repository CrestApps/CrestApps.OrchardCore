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
| Contact Center Queues | `CrestApps.OrchardCore.ContactCenter.Queues` | Managed skills, work queues, queue items, reservations, and availability-based assignment. |
| Contact Center Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound profiles, pacing, and dialer activity batches routed through Contact Center Voice. |
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
skills, an optional inbound channel endpoint mapping, and a reservation timeout. Activities enter a
queue as **queue items**; the system pairs the highest-priority, oldest waiting item with an eligible
available agent signed in to that queue and creates a short-lived **reservation**.

Routing is strategy-based. The default strategy chain first rejects agents that do not have every
required queue skill, then scores the remaining candidates by longest idle time. Each assignment
publishes an auditable routing-decision event that records the queue item, selected agent, candidate
scores, and reasons, so later supervisor and analytics features can explain why work was offered to
an agent.

A reservation locks the activity for one agent and can be accepted, rejected, canceled, or expired.
The CRM activity moves through `Available → Reserved → Assigned`, mirrored on the queue item and
agent presence. Expired or canceled reservations return the item to the queue automatically. A
background task expires stale reservations and assigns waiting work every minute.

## Dialer

A **dialer profile** is an execution policy, not the source of CRM work. Activities, campaigns,
subjects, batches, dispositions, and contact context still come from Omnichannel. The profile tells
the Contact Center how a specific outbound campaign should be dialed: which queue supplies agents,
which dialing mode (`Manual`, `Preview`, `Power`, `Progressive`, `Predictive`) is used, which Contact
Center voice provider places calls, how pacing works, and how attempts/retries are bounded. Power and
progressive profiles run automatically each minute: the Contact Center reserves an available agent,
creates an outbound interaction, and asks the Voice Contact Center Call Router to place the call.
Manual and preview profiles wait for agent action. Dialer activity batches load **unassigned**
inventory the dialer reserves later.

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

Contact Center management entries live under **Interaction Center**. Skills, queues, and dialer
profile CRUD screens match the Omnichannel Campaigns UI: searchable list pages render summary shapes,
and create/edit screens render display-driver editor shapes with the required root edit wrapper
templates. Agent sign-in and presence are injected into the Telephony soft phone through
`DisplayDriver<SoftPhoneWidget>`, so the operational controls stay with the phone while management
screens remain catalog-focused.

Agent state/reason-code management is planned as a catalog-backed admin surface, not as a
provider-specific dialer setting. When added, it should include recipe and deployment steps so
presence states/reason codes can move between tenants, and its migration should seed standard values
by executing the module recipe during tenant setup.

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
        "CrestApps.OrchardCore.DialPad.Dialer"
      ]
    }
  ]
}
```
