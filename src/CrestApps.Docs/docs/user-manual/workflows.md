---
sidebar_label: Contact Center Workflows
sidebar_position: 36
title: Contact Center Workflows
description: Start a workflow when a contact center event happens, and use contact center tasks to place calls, set presence, queue work, hand off to a person, schedule callbacks and control recording.
---

Orchard Core **Workflows** can react to contact center events and act on them without code. For example: when a call is abandoned, schedule a callback; when an AI conversation qualifies a lead, hand it to the sales queue.

| | |
| --- | --- |
| **Menu** | Design > Workflows |
| **Permission** | Manage workflows |
| **Features** | Workflows (`OrchardCore.Workflows`) plus the Contact Center features each task needs (see below) |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a workflow that starts on a contact center event and adds contact center tasks">
  <source src="/img/docs/um-cc-workflows.mp4" type="video/mp4" />
</video>

## Build a workflow

The screencast builds a workflow that starts recording every call as soon as it connects.

1. Open **Design > Workflows** and click **Create Workflow**. Enter a **Name** such as *Record connected calls* and click **Save**.
2. Click **Add Event**, find **Contact Center Event** and click its **Add** button.
3. Pick the **Event type** to react to, for example **Call connected** (leave *Any event type* to react to every event), and click **Save**.
4. Click **Add Task**, find a Contact Center task such as **Start Call Recording**, and click **Add**. Fill in its fields; for the interaction, use `{{ Workflow.Input.InteractionId }}` to take it from the event. Click **Save**.
5. On the canvas, drag the task away from the event, then drag from the event's **Matched** outcome to the task to connect them.
6. Click **Save**.

The event passes these values to the workflow, which you can use in any field with Liquid: `EventType`, `InteractionId`, `AggregateType`, `AggregateId`, `ActorId`, `ActorType`, `AgentId`, `AgentUserId` and `SourceComponent`, for example `{{ Workflow.Input.AgentUserId }}`.

The **Event type** list is grouped: Interaction, Activity, Routing & queues, Agent, Offer, Dialer, Callback, Call, Recording, Supervision, Secure capture and Shared voicemail.

## Contact Center activities

| Activity | Fields | Outcomes | Needs |
| --- | --- | --- | --- |
| **Contact Center Event** (event) | Event type | Matched, Ignored | Contact Center |
| **Place Call or Send Message** | Activity | Done, Already Started, Failed | Contact Center |
| **Set Agent Presence** | User, Status, Reason | Done, Failed | Contact Center Agents |
| **Enqueue Activity** | Activity, Queue | Done, Failed | Contact Center Work Distribution |
| **Hand Off to Live Agent** | Activity, Queue, Reason, Summary | Connected, Waiting In Queue, Callback Scheduled, Failed | Contact Center Work Distribution |
| **Schedule Callback** | Destination, Delay (minutes), Campaign, Queue, Contact | Done, Failed | Contact Center Outbound Dialer |
| **Start Call Recording** / **Stop Call Recording** | Interaction | Done, Indeterminate, Failed | Contact Center Call Recording |

