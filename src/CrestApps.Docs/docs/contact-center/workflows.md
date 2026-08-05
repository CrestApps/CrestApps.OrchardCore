---
sidebar_label: Workflows automation
sidebar_position: 4
title: Contact Center Workflows Automation
description: React to Contact Center domain events and drive presence, queueing, callbacks, and recording from Orchard Core Workflows without writing code.
---

The **Workflows** bridge (`CrestApps.OrchardCore.ContactCenter.Workflows`) exposes the Contact Center to the Orchard Core [Workflows](https://docs.orchardcore.net/en/latest/reference/modules/Workflows/) module. It contributes one event activity that starts or resumes a workflow whenever a domain event is published, and a set of task activities that let a no-code author act on the contact center in response.

Enable `CrestApps.OrchardCore.ContactCenter.Workflows` alongside `OrchardCore.Workflows`. The task activities are additionally gated on the capability that owns the underlying service, so an activity only appears in the editor when its capability is enabled and its service is guaranteed to be resolvable.

## Contact Center Event

The **Contact Center Event** activity (category *Contact Center*) starts or resumes a workflow when a domain event is published. Its **Event type** field is a grouped picker of every canonical event - interactions, activities, routing and queues, agents, offers, dialer, callbacks, calls, recording, and supervision. Leave it set to **Any event type** to react to every event, or pick a single type such as *Call ended* or *Interaction created*.

The activity offers two outcomes:

- **Matched** - the published event matches the selected type, or **Any event type** is selected.
- **Ignored** - the published event does not match the selected type.

When a workflow starts, the triggering event is available on the workflow input, including `EventType`, `InteractionId`, `AggregateType`, `AggregateId`, `ActorId`, and `SourceComponent`. Task activities read these values through Liquid expressions such as `{{ Workflow.Input.InteractionId }}`.

## Task activities

Each task exposes its identifier fields as Liquid expressions so they can bind to the triggering event, and returns a **Done** or **Failed** outcome (recording tasks add a third, **Indeterminate**, outcome).

| Task | Capability feature | What it does |
| --- | --- | --- |
| **Set Agent Presence** | `CrestApps.OrchardCore.ContactCenter.Agents` | Sets an agent's presence status (for example, into break or away) from a resolved user id, status, and optional reason. The reservation- and work-lifecycle-owned states (`Reserved`, `Busy`, `WrapUp`) are excluded from the picker and rejected at execution, because those states are applied by the runtime as a side effect of an offer, an active interaction, or wrap-up - setting them from automation would create a parked profile with no backing call and block future routing. |
| **Enqueue Activity** | `CrestApps.OrchardCore.ContactCenter.Queues` | Adds a CRM activity to a queue for routing, with an optional priority override. The target queue and the CRM activity must both exist; a resolved identifier that matches neither takes the **Failed** outcome instead of creating an orphan queue item. |
| **Schedule Callback** | `CrestApps.OrchardCore.ContactCenter.Dialer` | Schedules a customer callback - for example, after an abandoned call - with an optional delay, campaign, queue, and contact. |
| **Start Call Recording** | `CrestApps.OrchardCore.ContactCenter.Recording` | Starts recording for a resolved interaction. |
| **Stop Call Recording** | `CrestApps.OrchardCore.ContactCenter.Recording` | Stops recording for a resolved interaction. |

### Indeterminate recording outcome

Recording is a release-critical mutation. When the provider may have executed the state change but its outcome could not be observed, the recording tasks report the distinct **Indeterminate** outcome rather than collapsing to success or failure, so a workflow can branch into a reconciliation or alerting path instead of assuming a result.

## Deliberately excluded tasks

Two of the actions a workflow might want are intentionally **not** shipped as tasks, because doing so safely is not possible from a background, event-triggered workflow:

- **Transfer a live call.** A transfer is authorized against the initiating agent's `ClaimsPrincipal` for destination role-based access control. A workflow runs without an authenticated agent principal, so a workflow-driven transfer would either be denied or force an unsafe bypass of that authorization. Transfers remain an agent- or supervisor-initiated action.
- **Assign work to a specific agent.** Agent-targeted assignment is owned by the routing engine, which honors presence, skills, entitlements, and reservations. There is no agent-targeted assignment service to call, and bypassing routing would break those guarantees. Use **Enqueue Activity** to place work on a queue and let routing assign it.

Both remain available to code that has the necessary call-control context.
