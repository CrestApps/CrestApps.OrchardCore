---
sidebar_label: Workflows automation
sidebar_position: 4
title: Contact Center Workflows Automation
description: React to Contact Center domain events and drive presence, queueing, callbacks, and recording from Orchard Core Workflows without writing code.
user_manual:
  - user-manual/workflows
  - user-manual/leads-accounts-opportunities
---

The **Workflows** bridge exposes the Contact Center to the Orchard Core [Workflows](https://docs.orchardcore.net/en/latest/reference/modules/Workflows/) module. It contributes one event activity that starts or resumes a workflow whenever a domain event is published, and a set of task activities that let a no-code author act on the contact center in response.

The bridge is not a separate feature. Simply enable `OrchardCore.Workflows` alongside Contact Center and the event activity becomes available automatically. The task activities are additionally gated on the capability that owns the underlying service, so an activity only appears in the editor when its capability is enabled and its service is guaranteed to be resolvable.

How to build a workflow in the designer, with worked examples (recording connected calls, texting a customer the dialer could not reach, texting from the agent's own number), is in [Contact Center Workflows](../user-manual/workflows.md) in the User Manual. The Omnichannel CRM feature adds a **Lead Converted** event and a **Convert Lead** task; see [Workflows](../omnichannel/crm.md#workflows) on the CRM page.

## Contact Center Event

The **Contact Center Event** activity (category *Contact Center*) starts or resumes a workflow when a domain event is published. Its **Event type** field is a grouped picker of every canonical event - interactions, activities, routing and queues, agents, offers, dialer, callbacks, calls, recording, supervision, secure capture, and shared voicemail. Leave it set to **Any event type** to react to every event, or pick a single type such as *Call ended* or *Interaction created*.

The activity offers two outcomes:

- **Matched** - the published event matches the selected type, or **Any event type** is selected.
- **Ignored** - the published event does not match the selected type.

When a workflow starts, the triggering event is available on the workflow input, including `EventType`, `InteractionId`, `AggregateType`, `AggregateId`, `ActorId`, `ActorType`, `AgentId`, `AgentUserId`, and `SourceComponent`. Task activities read these values through Liquid expressions such as `{{ Workflow.Input.InteractionId }}`.

`ActorId` and `ActorType` say who made the change: the agent, a supervisor, a workflow, the telephony provider, or the platform (`system`). `AgentId` (the agent profile) and `AgentUserId` (the agent's user) say which agent the change is about, whoever made it. A workflow that acts on the agent, such as **Set Agent Presence**, reads `{{ Workflow.Input.AgentUserId }}`: when the platform reserves an agent or starts their wrap-up, the actor is the platform, not the agent.

### Event payloads used for follow-ups

These events carry a `Data` payload that follow-up workflows read as `Workflow.Input.Data.<Property>`:

| Event | Payload type | Properties |
| --- | --- | --- |
| **Dialer attempt completed** (`DialerAttemptCompleted`) | `CallLifecycleEventData` | `Outcome` (one of `DialerAttemptOutcomes`: `Answered`, `NoAnswer`, `Busy`, `AnsweringMachine`, `NotInService`, `Rejected`, `Failed`), `PhoneNumber`, `ActivityItemId`, `CampaignId`, `HangupCause`, `ProviderHangupCause`, `SipHangupCause`, plus the other call-lifecycle properties. Raised after every dialer attempt, answered or not. |
| **Dialer call answered by a person** (`DialerLiveAnswered`) | `CallLifecycleEventData` | `InteractionId`, `ActivityItemId`, `ProviderCallId`, `AgentId`, `QueueId`, and `Details` with `dialerProfileId` and `attemptNumber`. Raised once per Power, Progressive or Predictive call a person (not a machine) answers. `AggregateType` is `DialerProfile` and `AggregateId` the profile. |
| **Dialer call abandoned** (`DialerCallAbandoned`) | `CallLifecycleEventData` | `Reason` (`agent_leg_failed`, `agent_connect_failed`, `agent_unavailable`, `agent_connected_late` or `customer_hung_up_waiting`), `DurationSeconds` (seconds from the answer), and `Details` with `dialerProfileId` and `messagePlayed`. Raised once per answered automated call no agent reached within two seconds. `AggregateType` is `DialerProfile` and `AggregateId` the profile. |
| **Activity disposition applied** (`ActivityDispositionApplied`) | `ActivityDispositionEventData` | `ActivityItemId`, `DispositionId`, `DispositionName`, `Outcome` (`NotInService`, `NoAnswer`, `Busy`, `AnsweringMachine` or `None`), `Source` (`Agent`, `AI`, `Provider`, `Workflow` or `System`), `TerminalReasonCode`, `Channel`, `CampaignId`, `SubjectContentType`, `ContactContentItemId`, `PhoneNumber`, `Attempts`, `CompletedById`. Published by `ActivityDispositionAppliedPublisher` whenever an activity is completed with a disposition, whoever applied it. |

## Task activities

Each task exposes its identifier fields as Liquid expressions so they can bind to the triggering event, and returns a **Done** or **Failed** outcome. Some report more than that: recording tasks add **Indeterminate**, and the two omnichannel tasks report the outcomes described in [Outcomes beyond Done and Failed](#outcomes-beyond-done-and-failed).

| Task | Capability feature | What it does |
| --- | --- | --- |
| **Set Agent Presence** | `CrestApps.OrchardCore.ContactCenter.Agents` | Sets an agent's presence status (for example, into break or away) from a resolved user id, status, and optional reason. The reservation- and work-lifecycle-owned states (`Reserved`, `Busy`, `WrapUp`) are excluded from the picker and rejected at execution, because those states are applied by the runtime as a side effect of an offer, an active interaction, or wrap-up - setting them from automation would create a parked profile with no backing call and block future routing. |
| **Enqueue Activity** | `CrestApps.OrchardCore.ContactCenter.Queues` | Adds a CRM activity to a queue for routing, with an optional priority override. The target queue and the CRM activity must both exist; a resolved identifier that matches neither takes the **Failed** outcome instead of creating an orphan queue item. |
| **Schedule Callback** | `CrestApps.OrchardCore.ContactCenter.Dialer` | Schedules a customer callback - for example, after an abandoned call - with an optional delay, campaign, queue, and contact. |
| **Start Call Recording** | `CrestApps.OrchardCore.ContactCenter.Recording` | Starts recording for a resolved interaction. |
| **Stop Call Recording** | `CrestApps.OrchardCore.ContactCenter.Recording` | Stops recording for a resolved interaction. |
| **Place Call or Send Message** | `CrestApps.OrchardCore.ContactCenter` | Starts an automated omnichannel activity immediately, instead of waiting for the periodic automated-activities pass to pick it up. The activity's own channel selects the processor, so the same task places the outbound call for a Phone activity and sends the opening message for an SMS activity. |
| **Hand Off to Live Agent** | `CrestApps.OrchardCore.ContactCenter.Queues` | Moves an **automated** conversation out of the AI lane and into the human lane: a live call is seated in a queue and offered to an agent, and a text conversation becomes a queue-owned thread in the messaging workspace. Optionally names the queue, a reason, and a summary; when no queue is named, the subject flow's configured handoff queue is used. |
| **Find Agent Numbers** | `CrestApps.OrchardCore.ContactCenter` and `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` | Resolves the numbers an agent calls and texts from (the omnichannel address that lists the agent, or else the Contact Center default numbers) and writes them to `Workflow.Output.AgentPhoneNumber` and `Workflow.Output.AgentSmsNumber`. An empty user name returns the default numbers. Takes **NotFound** only when neither number exists. |
| **Send Text Message** | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | Sends a text from one of the tenant's SMS numbers through the provider that owns it. The text goes through the messaging workspace (`IMessagingConversationService.SendDirectAsync`), so it appears in the customer's conversation. A new conversation it starts belongs to the agent named in **Agent**, so the reply reaches them; without one it is a system text. |

### Inputs and outcomes

Text inputs accept Liquid that resolves the identifier or value, so they can bind to the triggering event (for example `{{ Workflow.Input.InteractionId }}`). Pickers and numbers are set in the editor.

| Activity | Inputs | Outcomes |
| --- | --- | --- |
| **Contact Center Event** | **Event type** (picker, or **Any event type**) | Matched, Ignored |
| **Set Agent Presence** | **User**, **Status** (picker), **Reason** (optional) | Done, Failed |
| **Enqueue Activity** | **Activity**, **Queue**, **Priority** (optional picker) | Done, Failed |
| **Schedule Callback** | **Destination**, **Delay (minutes)** (number), **Campaign**, **Queue**, **Contact** (the last three optional) | Done, Failed |
| **Start Call Recording** / **Stop Call Recording** | **Interaction** | Done, Indeterminate, Failed |
| **Place Call or Send Message** | **Activity** | Done, Already Started, Failed |
| **Hand Off to Live Agent** | **Activity**, **Queue**, **Reason**, **Summary** (the last three optional) | Connected, Waiting In Queue, Callback Scheduled, Failed |
| **Find Agent Numbers** | **User name** (optional) | Done, NotFound |
| **Send Text Message** | **From**, **To**, **Message**, **Agent** (optional) | Done, Failed |

### Outcomes beyond Done and Failed

Two of the tasks above report more than a binary result, because the workflow that follows usually needs to say something different in each case:

- **Place Call or Send Message** adds **Already Started**. The task only starts an activity that is still `NotStated` or `Scheduled`, mirroring the due-set filter the periodic pass uses. A workflow that fires twice - or that races that pass - takes this outcome instead of placing a second call to a customer who is already on the line.
- **Hand Off to Live Agent** replaces *Done* with **Connected**, **Waiting In Queue**, and **Callback Scheduled**, so a caller who was put straight through, one who is holding, and one who was offered a callback after hours can each be handled differently.

### Handing off is not transferring

**Hand Off to Live Agent** is not the live-call transfer excluded below, and does not reopen that decision. The two use different services:

- A **transfer** moves a call an agent is already on, through `IContactCenterTransferService`, and is authorized against that agent's `ClaimsPrincipal`. It remains unavailable to workflows.
- A **handoff** escalates an *automated* conversation through `IOmnichannelHandoffService`, which takes no principal. It is the same path the AI itself uses when the model invokes its transfer tool, and it ends by placing the work on a queue and letting routing assign it - exactly what the *assign work to a specific agent* exclusion recommends.

The task therefore does nothing for an agent-to-agent transfer, and has no effect on an interaction a human is already handling.

### Indeterminate recording outcome

Recording is a release-critical mutation. When the provider may have executed the state change but its outcome could not be observed, the recording tasks report the distinct **Indeterminate** outcome rather than collapsing to success or failure, so a workflow can branch into a reconciliation or alerting path instead of assuming a result.

## Deliberately excluded tasks

Two of the actions a workflow might want are intentionally **not** shipped as tasks, because doing so safely is not possible from a background, event-triggered workflow:

- **Transfer a live call.** A transfer is authorized against the initiating agent's `ClaimsPrincipal` for destination role-based access control. A workflow runs without an authenticated agent principal, so a workflow-driven transfer would either be denied or force an unsafe bypass of that authorization. Transfers remain an agent- or supervisor-initiated action. This is distinct from **Hand Off to Live Agent**, which escalates an automated conversation onto a queue and never touches a call a human is already on - see [Handing off is not transferring](#handing-off-is-not-transferring).
- **Assign work to a specific agent.** Agent-targeted assignment is owned by the routing engine, which honors presence, skills, entitlements, and reservations. There is no agent-targeted assignment service to call, and bypassing routing would break those guarantees. Use **Enqueue Activity** to place work on a queue and let routing assign it.

Both remain available to code that has the necessary call-control context.
