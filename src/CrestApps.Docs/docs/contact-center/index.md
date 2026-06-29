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

## Agent workspace

Agents receive Contact Center work in the Contact Center Agent Workspace inside the CRM admin
experience. The workspace is the place where agents sign in to allowed queues and outbound
campaigns, set presence and reason codes, receive activity offers, accept or reject reservations,
work the active CRM activity, use injected Telephony call controls, review interaction history, and
complete wrap-up/disposition.

Managers configure queue membership, campaign assignment, dialer mode, priority, capacity, and
compliance rules. Inbound queues, callback queues, preview dial queues, power/progressive/predictive
campaigns, and future channels all offer Activities through the same real-time workspace model.

## Voice provider integration

The Telephony module continues to own soft-phone call control and media execution. Contact Center
adds optional voice-provider abstractions for PBX providers that can participate in contact-center
orchestration beyond basic call control:

- Dial on behalf of a dialer.
- Assign an existing provider call to an agent after Contact Center chooses the assignment.
- Place or move provider calls in provider-side queues after Contact Center chooses the queue.
- Publish queue events and synchronize PBX presence when supported.

Contact Center remains the brain: it selects the Activity, queue, agent, campaign, dialer mode, and
compliance gates, then sends provider-neutral intents to the provider adapter.

## UI extensibility

All Contact Center UI is built with Orchard Core display management: shapes, display drivers,
placement, templates, and shape alternates. CRUD screens should follow the AI Profile UI pattern,
where controllers load catalog entries through managers and build summary/editor shapes with
`IDisplayManager<T>`. Activity screens remain Omnichannel screens that Contact Center augments with
display drivers for reservation state, interaction history, dialer controls, wrap-up, and supervisor
decorations.

## Status

The Contact Center is under active, phased development. The first milestone is a voice MVP that
proves agents can run inbound and outbound voice work entirely inside the CRM while preserving the
Telephony boundary. This documentation will expand as each capability ships. See
[Agents, Queues & Dialer](agents-queues-dialer.md) for the agent, queue, reservation, and dialer
features.
