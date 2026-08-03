# CrestApps.OrchardCore.ContactCenter

Provides the **contact center orchestration layer** that turns the CrestApps CRM (Omnichannel) into a full contact center. It owns the orchestration boundary between the CRM (which owns business work data) and Telephony providers (which execute media): the headless interaction lifecycle, a durable domain-event log, agent presence and availability, work queues and routing, an outbound dialer with compliance, recording orchestration, real-time experiences, and analytics.

`OmnichannelActivity` remains the universal work item; a Contact Center `Interaction` is the communication history for a single attempt and never owns workflow or disposition.

## Features

The module ships as many small, independently deployable, feature-gated capabilities so a tenant enables only what it licenses. Administration screens are separated from their runtime capability so a headless tenant can run without any admin UI.

### Foundation

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center | `CrestApps.OrchardCore.ContactCenter` | Headless interaction lifecycle, durable domain event log, and baseline permissions. Depends on Omnichannel Activities. |
| Contact Center Administration | `CrestApps.OrchardCore.ContactCenter.Admin` | Settings screens and the administration screens for every enabled capability. |

### Agents, queues, and routing

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, presence, capacity, skills, and queue/campaign sign-in. |
| Contact Center Availability | `CrestApps.OrchardCore.ContactCenter.Availability` | Canonical availability, durable sessions, heartbeat state, and after-call recovery without requiring real-time transport. |
| Contact Center Queues | `CrestApps.OrchardCore.ContactCenter.Queues` | Work queues, queue items, reservations, and availability-based activity assignment. |
| Contact Center Routing | `CrestApps.OrchardCore.ContactCenter.Routing` | Policy-based routing strategies and activity-assignment orchestration. |

### Voice and dialing

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Voice | `CrestApps.OrchardCore.ContactCenter.Voice` | Routes inbound/outbound voice through the Voice Contact Center Call Router while Telephony providers execute media. |
| Contact Center Voice Media | `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Executable bidirectional media-provider resolution for active calls (enabled by dependency only). |
| Contact Center Entry Points | `CrestApps.OrchardCore.ContactCenter.EntryPoints` | Inbound entry-point administration, qualification, business-hours decisions, and queue ingress. |
| Contact Center Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound dialing profiles, callbacks, and Manual/Preview activity batches. |
| Contact Center Outbound Compliance | `CrestApps.OrchardCore.ContactCenter.Compliance` | Mandatory outbound eligibility gate, suppression auditing, retry limits, and calling-window enforcement. |
| Contact Center Automated Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer.Automated` | Compliance-gated Power and Progressive dialing strategies with scheduled pacing. |
| Contact Center Recording | `CrestApps.OrchardCore.ContactCenter.Recording` | Provider-capability-gated recording orchestration and recording-state events. |

### Real-time experiences and reporting

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | Shared SignalR hub and real-time presence/offer/queue broadcasts. |
| Contact Center Voice - Soft Phone | `CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone` | Projects voice state into the Telephony soft phone and real-time agent experience. |
| Contact Center Agent Desktop | `CrestApps.OrchardCore.ContactCenter.AgentDesktop` | CRM-integrated real-time agent workspace for presence, offers, active interactions, and recent work. |
| Contact Center Supervision | `CrestApps.OrchardCore.ContactCenter.Supervision` | Real-time supervisor dashboard and provider-capability-gated monitoring actions. |
| Contact Center Reports & Analytics | `CrestApps.OrchardCore.ContactCenter.Analytics` | Executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject reports. |
| Contact Center - Workflows | `CrestApps.OrchardCore.ContactCenter.Workflows` | Contact Center domain-event activity and bridge for Orchard Core Workflows. |

The single **Contact Center Administration** feature (`CrestApps.OrchardCore.ContactCenter.Admin`) carries every capability's administration screens. Each capability's screens — Agents, Queues, Dialer, Recording, and Entry Points — appear only when both this feature and the matching capability are enabled, so enabling administration restores exactly the screens for the capabilities the tenant runs. There is no separate `.Admin` feature to enable per capability.

## Installation

Install the package into the web/startup project and enable the capabilities you need, together with the **Contact Center Administration** feature for the screens you want to configure. A minimal inbound-voice contact center, for example:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Admin",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Availability",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.Routing",
        "CrestApps.OrchardCore.ContactCenter.Voice",
        "CrestApps.OrchardCore.ContactCenter.EntryPoints"
      ]
    }
  ]
}
```

A Telephony provider (for example Asterisk or DialPad) and its Contact Center Voice feature must also be enabled for voice execution.

## Configuration

Enable the **Contact Center Administration** feature and configure each capability under its **Contact Center** settings screen. Compliance, business hours, calling windows, routing policies, and dialer profiles are all configured per tenant and enforced server-side.

## Usage

- Business code interacts with the headless interaction lifecycle and domain-event log; voice execution is delegated to Telephony providers through the Voice Contact Center Call Router.
- Real-time experiences (agent desktop, supervisor dashboard, soft phone projection) consume the shared SignalR hub exposed by the Real-Time feature.
- Domain events can be observed through the Workflows bridge for custom automation.

## Dependencies

- `CrestApps.OrchardCore.Omnichannel` (Activities and Managements)
- `CrestApps.OrchardCore.Telephony` — for voice features
- `CrestApps.OrchardCore.SignalR` — for real-time features
- `OrchardCore.Workflows` — for the Workflows feature

## Documentation

See the [Contact Center documentation](https://orchardcore.crestapps.com/contact-center/) for architecture, routing, agent desktop, reporting, and deployment guidance.
