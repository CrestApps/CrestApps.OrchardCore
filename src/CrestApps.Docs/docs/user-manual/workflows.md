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

The event passes these values to the workflow, which you can use in any field with Liquid: `EventType`, `InteractionId`, `AggregateType`, `AggregateId`, `ActorId`, `ActorType`, `AgentId`, `AgentUserId` and `SourceComponent`, for example `{{ Workflow.Input.AgentUserId }}`. The event's own details are under `Data`, for example `{{ Workflow.Input.Data.PhoneNumber }}`; see the two events below.

## Follow up on a call or a completed activity

Two events carry what a follow-up needs, such as a text message to a customer the dialer could not reach.

**Dialer attempt completed** fires after every call the dialer places, answered or not. The dialer does not disposition a call nobody answered (it dials the record again later), so this is the event to use for "we called and they did not answer".

| Value | What it holds |
| --- | --- |
| `Data.Outcome` | `Answered`, `NoAnswer`, `Busy`, `AnsweringMachine`, `NotInService`, `Rejected` or `Failed`. |
| `Data.PhoneNumber` | The number that was called. |
| `Data.ActivityItemId` | The activity the call was for. |
| `Data.CampaignId` | The activity's campaign. |
| `Data.HangupCause`, `Data.ProviderHangupCause`, `Data.SipHangupCause` | How the call ended, in the platform's terms and the provider's. |

**Activity disposition applied** fires whenever an activity is completed with a disposition: by an agent, by the dialer when it finds a number not in service, or by an automated (AI) call, including one nobody answered.

| Value | What it holds |
| --- | --- |
| `Data.DispositionName` | The disposition's name. |
| `Data.Outcome` | The disposition's [outcome](dispositions.md#outcomes): `NotInService`, `NoAnswer`, `Busy`, `AnsweringMachine` or `None`. |
| `Data.Source` | What applied it: `Agent`, `AI`, `Provider`, `Workflow` or `System`. |
| `Data.PhoneNumber` | The number the activity was reaching. |
| `Data.ActivityItemId`, `Data.ContactContentItemId`, `Data.CampaignId`, `Data.SubjectContentType`, `Data.Channel`, `Data.Attempts` | The activity, its contact, campaign, subject, channel and attempt count. |

### Example: text a customer the dialer could not reach

1. Create a workflow and add the **Contact Center Event** event with the event type **Dialer attempt completed**.
2. Add an **If/Else** task with the condition `input("Data").Outcome == "NoAnswer"`, and connect the event's **Matched** outcome to it.
3. Add a **Send SMS** task. Set **Phone number** to `{{ Workflow.Input.Data.PhoneNumber }}` and write the **Body**, then connect the **If/Else** task's **True** outcome to it.
4. Save the workflow.

Every Power, Progressive or Preview call that rings out now sends the text. Use `"Busy"` or `"AnsweringMachine"` in the condition to text on those outcomes instead. For automated (AI) calls, use **Activity disposition applied** with `input("Data").Outcome == "NoAnswer"`.

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

