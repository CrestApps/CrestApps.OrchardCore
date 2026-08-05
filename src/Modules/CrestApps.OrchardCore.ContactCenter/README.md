# CrestApps.OrchardCore.ContactCenter

Provides the **contact center orchestration layer** that turns the CrestApps CRM (Omnichannel) into a full contact center. It owns the orchestration boundary between the CRM (which owns business work data) and Telephony providers (which execute media): the interaction lifecycle, a durable domain-event log, agent presence and availability, work queues and routing, an outbound dialer with compliance, recording orchestration, administration, real-time experiences, and analytics.

`OmnichannelActivity` remains the universal work item; a Contact Center `Interaction` is the communication history for a single attempt and never owns workflow or disposition.

## Features

The module ships as feature-gated capabilities so a tenant enables only what it needs. Each capability includes its own administration screens; no separate administration feature is required.

### Foundation

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center | `CrestApps.OrchardCore.ContactCenter` | Interaction lifecycle, durable domain event log, baseline permissions, settings, and administration menu. Depends on Omnichannel Management. |

### Agents, queues, and routing

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Workforce | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, presence, capacity, skills, and queue/campaign sign-in, plus canonical availability, durable sessions, heartbeat state, and after-call recovery without requiring real-time transport. |
| Contact Center Work Distribution | `CrestApps.OrchardCore.ContactCenter.Queues` | Work queues, queue items, reservations, and policy-based routing strategies with availability-based activity assignment. |

### Voice and dialing

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Voice Media | `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Executable bidirectional media-provider resolution for active calls (enabled by dependency only). |
| Contact Center Inbound Voice | `CrestApps.OrchardCore.ContactCenter.EntryPoints` | Inbound entry-point administration, qualification, business-hours decisions, and queue ingress. |
| Contact Center Outbound Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound dialing profiles, callbacks, Manual/Preview activity batches, and mandatory eligibility, suppression, retry, do-not-call, and calling-window enforcement. |
| Contact Center Automated Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer.Automated` | Power and Progressive dialing strategies with scheduled pacing over the compliant base Dialer. |
| Contact Center Call Recording | `CrestApps.OrchardCore.ContactCenter.Recording` | Provider-gated recording orchestration and recording-state events. |

> The server-side voice orchestration (`CrestApps.OrchardCore.ContactCenter.Voice`) is enabled automatically as a dependency of Inbound Voice, Outbound Dialer, Call Recording, Supervision, the soft phone, and provider adapters, so it is not listed as a separately selectable feature.

### Real-time experiences and reporting

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | Shared SignalR hub and real-time presence/offer/queue broadcasts (enabled by dependency only). |
| Contact Center Voice - Soft Phone | `CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone` | Lightweight soft-phone agent tier: live call state, presence, and offers inside the Telephony soft phone. |
| Contact Center Agent Desktop | `CrestApps.OrchardCore.ContactCenter.AgentDesktop` | Full CRM-integrated agent workspace for presence, offers, active interactions, and recent work. Builds on the soft-phone tier. |
| Contact Center Supervision & Live Dashboard | `CrestApps.OrchardCore.ContactCenter.Supervision` | Real-time supervisor dashboard and provider-gated monitoring actions. |
| Contact Center Reports & Analytics | `CrestApps.OrchardCore.ContactCenter.Analytics` | Executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject reports. |
| Contact Center - Workflows | `CrestApps.OrchardCore.ContactCenter.Workflows` | Contact Center domain-event activity and bridge for Orchard Core Workflows. |

## Installation

Install the package into the web/startup project and enable the capabilities you need. Their administration screens are enabled with them. A minimal inbound-voice contact center, for example:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.EntryPoints"
      ]
    }
  ]
}
```

A Telephony provider (for example Asterisk or DialPad) and its Contact Center Voice feature must also be enabled for voice execution.

## Configuration

Configure each enabled capability from its Contact Center or Interaction Center administration screen. Compliance, business hours, calling windows, routing policies, and dialer profiles are all configured per tenant and enforced server-side.

## Usage

- Business code interacts with the interaction lifecycle and domain-event log; voice execution is delegated to Telephony providers through the Voice Contact Center Call Router.
- Real-time experiences (agent desktop, supervisor dashboard, soft phone projection) consume the shared SignalR hub exposed by the Real-Time feature, which is enabled automatically as a dependency of those experiences.
- Domain events can be observed through the Workflows bridge for custom automation.

## Dependencies

- `CrestApps.OrchardCore.Omnichannel` (Activities and Managements)
- `CrestApps.OrchardCore.Telephony` — for voice features
- `CrestApps.OrchardCore.SignalR` — for real-time features
- `OrchardCore.Workflows` — for the Workflows feature

## Documentation

See the [Contact Center documentation](https://orchardcore.crestapps.com/contact-center/) for architecture, routing, agent desktop, reporting, and deployment guidance.
