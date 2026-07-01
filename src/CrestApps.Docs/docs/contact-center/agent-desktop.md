---
sidebar_label: Agent Desktop & Dashboard
sidebar_position: 2
title: Agent Desktop and Supervisor Dashboard
description: How contact center agents work inbound and outbound interactions in the Agent Workspace, and how managers set up and monitor operations with queues, campaigns, and the live supervisor dashboard.
---

This guide covers the two day-to-day Contact Center surfaces:

- The **Agent Workspace** - the full-screen desktop where a **contact center agent** spends the shift:
  it presents work, connects calls, shows customer context, and captures the outcome.
- The **Supervisor Dashboard** - the live wallboard a **contact center manager** uses to monitor queue
  health and agent presence in real time.

Both build on the [real-time SignalR layer](index.md#real-time-experience) and the
[Telephony](../telephony/index.md) soft phone. The CRM still owns the work (activities, contacts,
subjects, dispositions), the Contact Center orchestrates it, and Telephony executes the media.

## Enabling the surfaces

Enable the **Contact Center Real-Time** feature (`CrestApps.OrchardCore.ContactCenter.RealTime`). It
depends on **Contact Center Queues** (and therefore **Agents** and the base **Contact Center**
feature) and the **SignalR** module, so enabling it pulls in the routing, presence, and reservation
core. For voice work, also enable **Contact Center Voice** and a voice provider (for example
[DialPad](../telephony/dialpad.md)).

Once enabled, two entries appear under **Interaction Center** in the admin menu:

- **My workspace** - the Agent Workspace, available to anyone with the
  `ContactCenterSignIntoQueues` permission.
- **Live dashboard** - the Supervisor Dashboard, available to anyone with the `MonitorContactCenter`
  permission (granted to the built-in **Supervisor** role).

## For contact center managers: preparing the environment

Agents can only receive work once the routing environment exists. Configure these in the
**Interaction Center** before your team signs in. Each item links to its detailed reference in
[Agents, Queues & Dialer](agents-queues-dialer.md).

1. **Skills** (*Interaction Center → Skills*) - define the competencies routing can require, for
   example `Spanish`, `Billing`, or `Tier2`.
2. **Queues** (*Interaction Center → Queues*) - create a queue per line of business. Choose the
   routing strategy (longest-idle, round-robin, or least-busy), optional sticky-agent preference, the
   SLA threshold, the reservation timeout, required skills, and - for inbound voice - the dialed number
   (DID) that feeds the queue. See [Queues, reservations, and assignment](agents-queues-dialer.md#queues-reservations-and-assignment).
3. **Business hours** (*Interaction Center → Business hours*) - attach a calendar to a queue so it
   pauses routing (or overflows) when closed.
4. **Entry points** (*Interaction Center → Entry points*, Voice feature) - map an inbound DID to a
   queue with a priority, business-hours gating, and a closed-hours action (hold, voicemail, overflow,
   or reject).
5. **Agent state reason codes** (*Interaction Center → Agent states*) - define the not-ready presence
   reasons agents can choose (for example `Lunch`, `Coaching`, `Admin`). These appear in the agent
   presence menu.
6. **Campaigns and dispositions** - campaigns and dispositions live in the
   [Omnichannel](../omnichannel/index.md) **Interaction Center**. Every activity carries a **Subject**
   whose **Subject Flow** is the single decision controller: it defines the dispositions an agent can
   choose and the follow-up actions each disposition triggers. See
   [Subject Flow is the single decision controller](index.md#subject-flow-is-the-single-decision-controller).
7. **Dialer profiles** (*Interaction Center → Dialer Profiles*, Dialer feature) - for outbound work,
   tie a campaign's activities to a queue, a dialing mode (manual, preview, power, or progressive),
   pacing, and compliance rules. See [Dialer](agents-queues-dialer.md#dialer).
8. **Callbacks** - use the callback service or workflow bridge to schedule callback requests against a
   contact, destination, due window, and optional queue. Due callbacks are promoted into outbound
   callback activities and, when a queue is set, enter the same routing path as other work.
9. **Assign agents** (*Interaction Center → Agents*) - create an agent profile per user, set the
   maximum concurrent interactions (capacity), and grant the routing skills. Skills are
   administrator-owned; agents choose only which of their allowed queues and campaigns to sign in to.

Grant agents the `ContactCenterSignIntoQueues` permission (or a role that includes it), and grant
supervisors the built-in **Supervisor** role (or the `MonitorContactCenter` permission).

## For contact center managers: the Live Dashboard

Open **Interaction Center → Live dashboard**. The dashboard connects to the real-time hub and refreshes
automatically as work and presence change (with a periodic safety refresh), so it can be left open on a
wallboard.

It shows three sections:

- **Summary metrics** - total items waiting across all queues, the number of available agents, the
  total agent count, and the queue count.
- **Queue tiles** - one tile per enabled queue showing the waiting count, the longest current wait, and
  the number of items that have breached the queue's SLA threshold. Tiles turn amber as waits approach
  the SLA and red once items breach it, so hotspots stand out at a glance.
- **Agent board** - every agent with a live presence dot (available, busy, wrap-up, break, and so on),
  their current reason, and how many interactions they are handling.

Use it to spot a backing-up queue, an SLA breach, or too few available agents, and then rebalance
staffing, adjust queue priorities, or open a campaign.

When an agent has a live interaction and the voice provider advertises the matching capability, the
agent card shows **Monitor**, **Whisper**, **Barge**, and **Take over** actions. Each action posts to the
audited monitoring service, which refuses unsupported modes and records the engagement as a Contact
Center domain event. Providers still own the media execution, so buttons are effective only when the
active provider implements the requested capability.

## For contact center managers: inbound routing runbook

Use this checklist before publishing a new inbound line:

1. Create or confirm the Omnichannel **channel endpoint** for the dialed number.
2. Configure the Subject Flow for that endpoint so inbound activities get the right subject, campaign,
   disposition list, required-disposition policy, and follow-up subject actions.
3. Create the target queue, set its SLA, reservation timeout, routing strategy, required skills, and
   overflow queue.
4. Attach a business-hours calendar when the queue should pause or overflow outside staffed hours.
5. Create an **Entry point** for the DID. Set the target queue, priority, optional welcome/closed
   messages, and the closed action: hold, voicemail, overflow, or reject.
6. Sign at least one skilled agent in to the queue, then place a test call. The expected path is
   **provider webhook → entry point → queue → reservation → Agent Workspace offer → soft-phone media**.
7. Watch **Live dashboard** while testing. The queue waiting count should increase before assignment,
   then the selected agent should move from available to reserved/busy/wrap-up as the call progresses.

## For contact center managers: outbound and callback runbook

Use CRM campaigns and activities as the source of outbound work; the dialer profile only controls
execution.

1. Create the campaign and Subject Flow in Omnichannel. Configure dispositions and subject actions first
   so every outcome has a business result.
2. Load activities through an Activity Batch. Choose a dialer source for dialer inventory so activities
   are loaded unassigned and available for reservation.
3. Create a dialer profile that points to the campaign, queue, voice provider, dialing mode, pacing, and
   compliance settings.
4. Confirm do-not-call, retry delay, calling window, and national registry settings before enabling an
   automated mode.
5. For callbacks, schedule a callback request with the destination, due time, queue, and notes. The
   callback dispatcher promotes due callbacks into outbound callback activities and enqueues them when a
   queue is set.
6. Agents receive preview/manual work from their signed-in campaign or automated power/progressive work
   from the queue, then complete it with the same disposition flow used for inbound work.

## For contact center managers: workflow automation

The Subject Flow is the primary business workflow for work completion. Use it for required
dispositions and disposition-driven actions such as finish, retry, new activity, or communication-
preference updates. Enable the optional **OrchardCore.Workflows** bridge only when you need automation
from Contact Center domain events such as routing decisions, offer acceptance, call connected/ended,
callback scheduled/promoted, or SLA/analytics events. Workflow automation should enrich or react to
activity state; it should not bypass queues, reservations, or the source-neutral disposition service.

## For contact center agents: the Agent Workspace

Open **Interaction Center → My workspace**. This is the screen an agent keeps open for the whole shift.
Keep the [Telephony soft phone](../telephony/index.md) available too - it is where the call audio and
device controls live.

### Shift checklist

1. Open **My workspace** and the Telephony soft phone.
2. Sign in to the queues and campaigns you are staffed for.
3. Set presence to **Available** when ready, or choose a reason code when not ready.
4. Accept or decline offers from the ringing card; use the soft phone for media controls.
5. End the conversation, choose the disposition, add notes when needed, and click **Complete & wrap up**.
6. Use **Recent activity** to verify your last outcomes before taking the next offer.

### 1. Sign in and set your presence

- **Sign in to queues and campaigns** from the soft phone's **Work** tab. You can only choose queues and
  campaigns you are allowed to handle.
- **Set your presence** from the presence button at the top of the workspace. Choose **Available** to
  receive work, pick a **reason code** (for example *Lunch* or *Coaching*) to go not-ready, or choose
  **Request break**. A break is granted immediately when nothing is being routed to you; if an offer is
  already in flight, you finish it and the break is granted automatically afterward.

The top bar also shows a live chip per signed-in queue with its current waiting count, so you can see
where the pressure is.

### 2. Receive and answer an offer

When routing selects you for a piece of work, a **ringing offer card** appears with the customer name
(or number), the queue, and a countdown showing how long you have to respond. You have two choices:

- **Accept** - accepts the reservation, connects the media, and moves the work into your active panel.
  For providers that ring your device (such as DialPad's soft phone), your device rings and you answer
  there; the workspace and the incoming-call modal coordinate so the call is only answered after the
  reservation is confirmed - you will never pick up a call that has already been re-offered to someone
  else.
- **Decline** - releases the offer so it is immediately re-offered to the next available agent.

If you do not respond before the countdown ends, the offer is revoked and routed elsewhere.

### 3. Handle the active interaction

Once you accept, the **active interaction** panel shows:

- The **customer**, with a link to open the full CRM contact record (customer 360).
- The **direction** (inbound or outbound), the current **call status**, and a **live talk timer**.
- The **queue** the work came from.

Use the soft phone for hold, mute, transfer, and hang-up. The workspace reflects the call state in real
time.

### 4. Wrap up with a disposition

When the conversation ends, capture the outcome in the **wrap-up** section of the active panel:

1. Choose a **disposition** from the list defined by the activity's subject flow.
2. Optionally add **notes**.
3. Click **Complete & wrap up**.

Completing routes through the shared disposition path, which applies the disposition, marks the activity
completed, and runs the subject flow's follow-up actions - the same path used everywhere in the CRM, so
inbound, outbound, and manual work all behave consistently. If the subject flow **requires** a
disposition, completion is blocked until you pick one and the workspace shows why.

Your in-progress disposition and notes are never lost when the screen refreshes for a live update.

### 5. Review recent activity

The **Recent activity** panel lists your most recent interactions with their direction, outcome, and
time, so you can quickly refer back to your last few contacts.

## How it works

- The workspace loads a **state snapshot** from the server and then keeps itself current from the
  real-time hub's presence, offer, and queue events. It re-reads the authoritative state after you act,
  so what you see always matches the server.
- **Accept** calls a single server-side command that accepts the reservation, tells the voice provider
  to connect the call to you, and advances the interaction and call session together - one atomic,
  audited transition rather than several best-effort client actions.
- **Complete** goes through the source-neutral `IActivityDispositionService`, so dispositions,
  required-disposition rules, and subject-flow actions behave identically across every channel and
  source.

## Permissions and roles

| Permission | Grants |
| --- | --- |
| `ContactCenterSignIntoQueues` | Sign in to queues/campaigns, change own presence, and use the Agent Workspace (accept/decline offers, complete work). |
| `MonitorContactCenter` | Open the Supervisor Dashboard and watch queues in real time. Included in the **Supervisor** role. |
| `ManageContactCenterQueues`, `ManageContactCenterAgents`, `ManageContactCenterSkills`, `ManageContactCenterDialer` | Configure the routing environment (queues, agents, skills, dialer). See [Agents, Queues & Dialer](agents-queues-dialer.md). |
