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

**Dialer attempt completed** fires after every call the dialer places, answered or not. A call that ended before an agent was connected is also dispositioned by the dialer, so **Activity disposition applied** fires for it right after.

| Value | What it holds |
| --- | --- |
| `Data.Outcome` | `Answered`, `NoAnswer`, `Busy`, `AnsweringMachine`, `NotInService`, `Rejected`, `Failed` or `Disconnected` (a customer answered but hung up before the agent was connected). |
| `Data.Details.attemptNumber`, `Data.Details.maxAttempts`, `Data.Details.dialerProfileId` | Which attempt the call was, how many the dialer profile allows, and the profile. |
| `Data.PhoneNumber` | The number that was called. |
| `Data.ActivityItemId` | The activity the call was for. |
| `Data.CampaignId` | The activity's campaign. |
| `Data.HangupCause`, `Data.ProviderHangupCause`, `Data.SipHangupCause` | How the call ended, in the platform's terms and the provider's. |

**Activity disposition applied** fires whenever an activity is completed with a disposition: by an agent, by the dialer for a call that ended before an agent was connected, or by an automated (AI) call, including one nobody answered.

| Value | What it holds |
| --- | --- |
| `Data.DispositionName` | The disposition's name. |
| `Data.Outcome` | The disposition's [outcome](dispositions.md#outcomes): `NotInService`, `NoAnswer`, `Busy`, `AnsweringMachine`, `Rejected`, `Failed`, `Disconnected` or `None`. |
| `Data.DialerOutcome` | For a call the dialer placed: how it ended, as in **Dialer attempt completed**. |
| `Data.AttemptNumber`, `Data.MaxAttempts`, `Data.RemainingAttempts` | For a call the dialer placed: which attempt it was, how many the dialer profile allows, and how many are left. |
| `Data.DialerProfileId`, `Data.InteractionId`, `Data.TerminalReasonCode` | The dialer profile, the call, and why the activity ended (for example `dialer_no_answer`). |
| `Data.Source` | What applied it: `Agent`, `AI`, `Provider`, `Workflow` or `System`. |
| `Data.PhoneNumber` | The number the activity was reaching. |
| `Data.ActivityItemId`, `Data.ContactContentItemId`, `Data.CampaignId`, `Data.SubjectContentType`, `Data.Channel`, `Data.Attempts` | The activity, its contact, campaign, subject, channel and attempt count. |

### Call a contact again

When the dialer dispositions a call, what happens next is up to the disposition. There are two ways to have the dialer call the contact again:

- **In the subject flow (no workflow).** Wire **Try Again** to the disposition, for example *No answer*. The next attempt is created as a new activity and put back in the same campaign with the same dialer profile, due no sooner than the profile's retry delay. No attempt is created past the profile's **Max attempts**.
- **In a workflow.** Use the **Schedule Dialer Retry** task. It does the same, but the workflow decides when: set **Delay in minutes** (empty uses the profile's retry delay, and a shorter delay is raised to it). It ends with **Scheduled** (the next activity is in `{{ Workflow.Output.NextActivityItemId }}`), **Exhausted** when no attempt is left, or **Failed**.

Use one or the other for the same disposition, or the contact gets two follow-up activities.

1. Create a workflow and add the **Contact Center Event** event with the event type **Activity disposition applied**.
2. Add an **If/Else** task with the condition `input("Data").DialerOutcome == "Busy" && input("Data").RemainingAttempts > 0`, and connect the event's **Matched** outcome to it.
3. Add a **Schedule Dialer Retry** task. Leave **Activity** as `{{ Workflow.Input.Data.ActivityItemId }}` and set **Delay in minutes** to `15`, then connect the **If/Else** task's **True** outcome to it.
4. Save the workflow.

### Example: text a customer the dialer could not reach

1. Create a workflow and add the **Contact Center Event** event with the event type **Dialer attempt completed**.
2. Add an **If/Else** task with the condition `input("Data").Outcome == "NoAnswer"`, and connect the event's **Matched** outcome to it.
3. Add a **Send SMS** task. Set **Phone number** to `{{ Workflow.Input.Data.PhoneNumber }}` and write the **Body**, then connect the **If/Else** task's **True** outcome to it.
4. Save the workflow.

Every Power, Progressive or Preview call that rings out now sends the text. Use `"Busy"`, `"AnsweringMachine"` or `"Disconnected"` in the condition to text on those outcomes instead. For automated (AI) calls, use **Activity disposition applied** with `input("Data").Outcome == "NoAnswer"`.

### Example: text a customer from the agent's own number after a call

**Find Agent Numbers** looks up the numbers an agent calls and texts from and writes them to the workflow: `{{ Workflow.Output.AgentPhoneNumber }}` and `{{ Workflow.Output.AgentSmsNumber }}`. They come from the Omnichannel Address whose **Agents who dial from this number** or **Agents who text from this number** lists the agent, or else from the [default numbers](contact-center-settings.md#default-numbers). The **User name** field takes Liquid, and a user identifier or email works too. Leave it empty to get the default numbers. The task ends in **NotFound** only when it finds neither number.

1. Start the workflow from the **Contact Center Event** that follows the call, for example **Activity disposition applied**.
2. Add **Find Agent Numbers** with **User name** set to the user who handled the call: `{{ Workflow.Input.Data.CompletedById }}`.
3. Add **Send Text Message** with **From** `{{ Workflow.Output.AgentSmsNumber }}`, **To** `{{ Workflow.Input.Data.PhoneNumber }}`, your **Message**, and **Agent** `{{ Workflow.Input.Data.CompletedById }}`, and connect the **Done** outcome of Find Agent Numbers to it.

The text is sent by the provider that owns the number and lands in the customer's conversation in the messaging workspace, owned by the agent, so the customer's reply reaches them.

The **Event type** list is grouped: Interaction, Activity, Routing & queues, Agent, Offer, Dialer, Callback, Call, Recording, Supervision, Secure capture and Shared voicemail.

## Contact Center activities

| Activity | Fields | Outcomes | Needs |
| --- | --- | --- | --- |
| **Contact Center Event** (event) | Event type | Matched, Ignored | Contact Center |
| **Place Call or Send Message** | Activity | Done, Already Started, Failed | Contact Center |
| **Set Agent Presence** | User, Status, Reason | Done, Failed | Contact Center Agents |
| **Find Agent Numbers** | User name | Done, NotFound | Contact Center, Omnichannel Addresses |
| **Send Text Message** | From, To, Message, Agent | Done, Failed | SMS Messaging Channel |
| **Enqueue Activity** | Activity, Queue | Done, Failed | Contact Center Work Distribution |
| **Hand Off to Live Agent** | Activity, Queue, Reason, Summary | Connected, Waiting In Queue, Callback Scheduled, Failed | Contact Center Work Distribution |
| **Schedule Callback** | Destination, Delay (minutes), Campaign, Queue, Contact | Done, Failed | Contact Center Outbound Dialer |
| **Start Call Recording** / **Stop Call Recording** | Interaction | Done, Indeterminate, Failed | Contact Center Call Recording |

