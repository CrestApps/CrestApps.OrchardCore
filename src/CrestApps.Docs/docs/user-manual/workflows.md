---
sidebar_label: Contact Center Workflows
sidebar_position: 36
title: Contact Center Workflows
description: Start a workflow when a contact center event happens, and use contact center tasks to place calls, set presence, queue work, hand off to a person, schedule callbacks and control recording.
technical_manual:
  - contact-center/workflows
---

Workflows can react to contact center events and act on them without code. For example: when a call is abandoned, schedule a callback; when an AI conversation qualifies a lead, hand it to the sales queue.

| | |
| --- | --- |
| **Menu** | Design > Workflows |
| **Permission** | Manage workflows |
| **Features** | Workflows, plus the Contact Center features each task needs (see [Contact Center activities](#contact-center-activities)) |

<AskYourAdmin />

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

The **Event type** list is grouped: Interaction, Activity, Routing & queues, Agent, Offer, Dialer, Callback, Call, Recording, Supervision, Secure capture and Shared voicemail. The event has two outcomes: **Matched** when the event is the type you picked (or you picked *Any event type*), and **Ignored** otherwise.

### Values the event passes to the workflow

You can use these in any field that takes Liquid, for example `{{ Workflow.Input.AgentUserId }}`:

| Value | What it holds |
| --- | --- |
| `EventType` | The kind of event, such as a call connecting. |
| `InteractionId` | The call or conversation the event is about. |
| `AggregateType`, `AggregateId` | The record the event is about, such as an activity. Activity tasks take `{{ Workflow.Input.AggregateId }}`. |
| `ActorId`, `ActorType` | Who made the change: the agent, a supervisor, a workflow, the telephony provider, or the platform itself (`system`). |
| `AgentId`, `AgentUserId` | Which agent the change is about, whoever made it. |
| `SourceComponent` | The part of the platform that raised the event. |
| `Data` | The event's own details, for example `{{ Workflow.Input.Data.PhoneNumber }}`. See the two events below. |

To act on the agent, for example with **Set Agent Presence**, use `{{ Workflow.Input.AgentUserId }}`, not the actor: when the platform reserves an agent or starts their wrap-up, the actor is the platform, not the agent.

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
| `Data.CompletedById` | The user who completed the activity, when a person did. |
| `Data.ActivityItemId`, `Data.ContactContentItemId`, `Data.CampaignId`, `Data.SubjectContentType`, `Data.Channel`, `Data.Attempts` | The activity, its contact, campaign, subject, channel and attempt count. |

### Example: text a customer the dialer could not reach

1. Create a workflow and add the **Contact Center Event** event with the event type **Dialer attempt completed**.
2. Add an **If/Else** task with the condition `input("Data").Outcome == "NoAnswer"`, and connect the event's **Matched** outcome to it.
3. Add a **Send SMS** task. Set **Phone Number** to `{{ Workflow.Input.Data.PhoneNumber }}` and write the **Body**, then connect the **If/Else** task's **True** outcome to it.
4. Save the workflow.

Every Power, Progressive or Preview call that rings out now sends the text. Use `"Busy"` or `"AnsweringMachine"` in the condition to text on those outcomes instead. For automated (AI) calls, use **Activity disposition applied** with `input("Data").Outcome == "NoAnswer"`.

### Example: text a customer from the agent's own number after a call

**Find Agent Numbers** looks up the numbers an agent calls and texts from and writes them to the workflow: `{{ Workflow.Output.AgentPhoneNumber }}` and `{{ Workflow.Output.AgentSmsNumber }}`. They come from the Omnichannel Address whose **Agents who dial from this number** or **Agents who text from this number** lists the agent, or else from the [default numbers](contact-center-settings.md#default-numbers). The **User name** field takes Liquid, and a user identifier or email works too. Leave it empty to get the default numbers. The task ends in **NotFound** only when it finds neither number.

1. Start the workflow from the **Contact Center Event** that follows the call, for example **Activity disposition applied**.
2. Add **Find Agent Numbers** with **User name** set to the user who handled the call: `{{ Workflow.Input.Data.CompletedById }}`.
3. Add **Send Text Message** with **From** `{{ Workflow.Output.AgentSmsNumber }}`, **To** `{{ Workflow.Input.Data.PhoneNumber }}`, your **Message**, and **Agent** `{{ Workflow.Input.Data.CompletedById }}`, and connect the **Done** outcome of Find Agent Numbers to it.

The text is sent by the provider that owns the number and lands in the customer's conversation in the [messaging workspace](messaging.md), owned by the agent, so the customer's reply reaches them. Leave **Agent** empty to send a system text instead.

## Contact Center activities

A task only appears in the **Add Task** list when the feature it needs is on. Every field marked as Liquid can take a value from the event, such as `{{ Workflow.Input.AggregateId }}` for an activity.

| Activity | Fields | Outcomes | Needs |
| --- | --- | --- | --- |
| **Contact Center Event** (event) | Event type | Matched, Ignored | Contact Center |
| **Place Call or Send Message** | Activity | Done, Already Started, Failed | Contact Center |
| **Set Agent Presence** | User, Status, Reason | Done, Failed | Contact Center Agents |
| **Find Agent Numbers** | User name | Done, NotFound | Contact Center and Omnichannel Channel Endpoints |
| **Send Text Message** | From, To, Message, Agent | Done, Failed | SMS Messaging Channel |
| **Enqueue Activity** | Activity, Queue, Priority | Done, Failed | Contact Center Work Distribution |
| **Hand Off to Live Agent** | Activity, Queue, Reason, Summary | Connected, Waiting In Queue, Callback Scheduled, Failed | Contact Center Work Distribution |
| **Schedule Callback** | Destination, Delay (minutes), Campaign, Queue, Contact | Done, Failed | Contact Center Outbound Dialer |
| **Start Call Recording** / **Stop Call Recording** | Interaction | Done, Indeterminate, Failed | Contact Center Call Recording |

The [Leads, Accounts and Opportunities](leads-accounts-opportunities.md#automate-with-workflows) page describes the **Lead Converted** event and the **Convert Lead** task of the Omnichannel CRM feature.

### What each task does

- **Place Call or Send Message** starts an automated activity right away instead of waiting for the next automatic pass. A Phone activity is dialed; an SMS activity is sent its opening message. It ends in **Already Started** when the activity was already started, so a workflow that fires twice never calls a customer twice.
- **Set Agent Presence** puts an agent into a status such as a break, with an optional reason. The **Status** list offers *Offline*, *Available*, *Break*, *Requested break*, *Away*, *Do not disturb*, *Meeting*, *Training* and *After-hours unavailable*. It leaves out the reserved, busy and wrap-up states, because the platform sets those itself while an agent is offered or handling work.
- **Find Agent Numbers** looks up the numbers an agent calls and texts from. See the example above.
- **Send Text Message** sends a text from one of your SMS numbers. **Agent** is optional: name an agent so the customer's reply reaches them, or leave it empty for a system text.
- **Enqueue Activity** puts an activity on a queue for routing. **Priority** is optional; *Use queue default* keeps the queue's own priority. If the activity or the queue does not exist, the task ends in **Failed**.
- **Hand Off to Live Agent** moves an automated (AI) conversation to people: a live call is put in a queue and offered to an agent, and a text conversation becomes a queue-owned conversation in the messaging workspace. Leave **Queue** empty to use the hand-off queue of the subject flow. **Reason** is shown to the agent who takes it, and **Summary** gives them the conversation so far. It ends in **Connected** (put straight through), **Waiting In Queue**, or **Callback Scheduled** (for example after hours), so you can handle each case differently. It does nothing to a call a person is already handling.
- **Schedule Callback** schedules a callback to the **Destination** number. **Delay (minutes)** of 0 makes it due now. **Campaign**, **Queue** and **Contact** are optional.
- **Start Call Recording** and **Stop Call Recording** end in **Indeterminate** when the provider may have made the change but the platform could not confirm it. Use that outcome to alert someone or check the recording, rather than assuming it worked.

### What workflows cannot do

- **Transfer a live call.** A transfer needs the agent or supervisor who asks for it, so it stays an action people take on the call.
- **Give work to one particular agent.** Routing decides who gets work, taking presence, skills and entitlements into account. Use **Enqueue Activity** to put the work on a queue and let routing pick the agent.
