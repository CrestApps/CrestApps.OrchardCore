---
sidebar_label: Live Dashboard
sidebar_position: 34
title: Live Dashboard and Supervisor Interventions
description: Watch queues and agents in real time, filter the agent board, and listen to, whisper to, barge into, take over, transfer or end a live call.
technical_manual:
  - contact-center/agent-desktop
  - telephony/telnyx
---

The **live dashboard** is the supervisor's real-time view of the contact center: how many people are waiting, which agents are free, and who is on a call. From an agent's card a supervisor can step into the call, coach the agent, or take the call over. Leave it open on a wallboard to keep an eye on the floor.

| | |
| --- | --- |
| **Menu** | Interaction Center > Live dashboard |
| **Permissions** | Monitor the Contact Center in real time (to watch, listen, whisper, barge and message). Take over, end, transfer and record live Contact Center calls, and set agents' state (for the other actions). The *Supervisor* role has both. |
| **Feature** | Contact Center Supervision & Live Dashboard |

<AskYourAdmin />

<YouTube video="contact-center-agent-walkthroughs" title="Video of the live dashboard summary, queue tiles and agent board filters" start={770} end={799} />

:::info[You see your own queues]
The dashboard shows the queues and campaigns on **your own** [agent entitlement](skills-and-entitlements.md) record, and you can only act on agents and calls in those queues. A supervisor without an entitlement record sees an empty dashboard, even with the Administrator role.
:::

## Reading the dashboard

The page updates as events happen and refreshes every 10 seconds. The status at the top says *Connected* while live updates work; *Connection lost. Reconnecting...* or *Disconnected. Live updates are paused.* means the numbers may be out of date.

| Area | What it shows |
| --- | --- |
| **Dialer banner** | When the Contact Center Paced Dialing feature is on: one line saying whether the dialer is stalled, idle, holding back, dialing or has nothing to call (see [Dialer banner](#dialer-banner)). |
| **Summary** | Total waiting across your queues, available agents, agents, and queues. |
| **Call-quality alerts** | An agent whose recent calls were rated poor, with the likely cause (for example packet loss on their network). Dismiss an alert once handled. See [Call-quality alerts](#call-quality-alerts). |
| **Why isn't this dialing?** | One card for each queue or campaign that has work, with how much is dialable and why the rest is held back. See [Why isn't this dialing?](#why-isnt-this-dialing). |
| **Queue tiles** | For each queue: waiting, signed-in agents, available, busy and not-ready agents, the longest wait, and SLA breaches. A tile turns **amber** when the longest wait passes half the SLA threshold and **red** when there is a breach. |
| **Agent board** | One card per agent with presence, a status badge, active interactions and action buttons. |

Each agent's badge shows their state as their own screens do: *Available*, *Reserved*, *On a call*, *Wrap-up*, or the reason for a not-ready state. An agent the dialer is calling for is *Reserved* until a person answers and they are connected.

Filter the agent board by **Agent** name, **Signed-in queue**, **Signed-in campaign** and **Status** (Available, Busy, On a call, Not ready, Offline). **Busy** groups everyone at work: *Reserved*, *On a call* and *Wrap-up*, as the queue tiles count them. **On a call** keeps only agents who are connected to a call; a *Reserved* agent whose call is still ringing is left out. **Clear** resets the filters; the browser remembers them for next time. The board header shows how many agents match, for example *4 of 12 agents*.

Use the dashboard to spot a queue that is backing up, an SLA breach or too few available agents, then move people between queues or ask agents to come back from a break.

The agent board lists only agents who are signed in to a queue or campaign you oversee. To find out why an agent is not getting calls, including an agent the board leaves out, open the **Agent availability and wait time** report (see [Reports](reports.md#agent-availability-and-wait-time)).

An agent on a call of their own (a number they dialed from the keypad, or an extension call) shows **On a call** with the number or colleague and how long the call has lasted.

## Dialer banner

When your site dials campaigns automatically (the Contact Center Paced Dialing feature), a banner above the summary says what the dialer is doing for the campaigns you oversee. It refreshes every 30 seconds, and names up to three campaigns.

| Banner | What it means |
| --- | --- |
| Red: *Dialer not pacing: the pacing lock of (campaigns) has not been acquired since (time), so every pacing cycle skips itself. Check the distributed lock.* | The dialer cannot take the lock it needs to pace the campaign, so nothing is dialed. This is a fault: tell your administrator. |
| Red: *Dialer stalled: due work is waiting but no pacing cycle has completed for N minutes* | Records are waiting, but the dialer has stopped working through the campaign. This is a fault: tell your administrator. |
| Amber: *Dialer idle: work is due but no agent is available to take it* | The dialer is working, but every agent signed in to the campaign is busy or away. Bring agents back or sign more agents in. |
| Amber: *Dialer holding back* | The dialer is working but chose not to call: the compliance check refused the numbers, the abandoned-call cap was reached, or calls already ringing cover the free agents. |
| Blue: *Dialer starting* | Records arrived a moment ago; the dialer has not finished its first pass yet. |
| Green: *Dialer active: placing calls* | The dialer is placing calls. |
| Grey: *Nothing due yet: records wait (campaigns), but the latest pacing cycle found none it may dial now* | The campaign has records, but none is due to be dialed at this moment: they are scheduled for later or waiting out their retry delay. |
| Grey: *Nothing to call* | No paced campaign has work waiting. |

When several campaigns are in different states, the banner shows the most serious one. The **Dialer activity and health** report gives the details for every campaign: see [Reports](reports.md#dialer-activity-and-health).

## Why isn't this dialing?

The **Why isn't this dialing?** panel under the queue tiles explains each queue or campaign that has work waiting. It refreshes every 30 seconds. A card shows how many records are pending, how many are *dialable now* and how many are held back, with each reason and its count, for example *Do not call*, *Outside the calling window*, *Waiting out the retry delay*, *Out of attempts*, *No number to dial*, *On the national do-not-call registry*, *Number not in service*, *Compliance check unavailable* or *Abandonment rate cap reached*. Other reasons point at the setup: *The queue is turned off*, *The queue's business hours are closed*, *The dialer profile is turned off*, *Not in the queue, so routing never sees it* and *Assigned to a user for manual work*.

Notes under the list say how the campaign is paced (for example *Paced by the system*), how many agents are signed in and how many are available, and what routing found the last time it looked, with the reason it could not assign anyone. A card is red when something is wrong (a queue blocker, a routing failure, a pacing fault, or dialable work with no available agent) and amber when records are held back and none is dialable. When a pacing lock is not acquired, or pacing has stalled, the card says so first. The panel also appears for ordinary queues with waiting work, where *dialable now* means ready to be handed out.

## Call-quality alerts

When three of an agent's last five calls were rated poor, the dashboard raises an alert for the supervisors of that agent's queues: *(agent): N of their last M calls rated poor. Likely cause: (cause)*. The cause is one of no audio received, no audio sent from their phone, packet loss on their network, jitter on their network, latency on their network path, the customers' lines, or not determined. An agent raises at most one alert an hour.

Alerts appear only while the dashboard is open: a supervisor who opens it later sees none. The cross on an alert (**Dismiss**) hides it in your browser only; reloading the page brings the dashboard back without it.

## Free agent

An agent can look busy while nothing backs it: a call the provider has already ended, a reservation nothing holds, or work still assigned with no call in progress. Their card then shows a warning badge, with a tooltip that says to use **Free agent** in the **⋮** menu. Choose **Free agent…** there: the panel asks *Release what (agent) is holding that nothing backs?*, lists what is held (for example *Marked busy with no call in progress*, *Holds a reservation that nothing backs* or *Work is still assigned with no call in progress*), and releases it when you click **Free agent**. A call the provider confirms is still alive is never ended; if there is one, nothing is released. The action needs the permission to take over calls, and it is recorded in the audit trail.

## Listen, whisper or barge

You hear the call on **your own soft phone**, so keep it open and signed in. When you start, the system rings your soft phone and it answers by itself. The call does not appear as a call row: a banner at the top of your soft phone shows it instead.

1. On the agent board, find the agent who is on a call.
2. Click one of the mode buttons on their card:
   - **Listen**: you hear the call; nobody hears you.
   - **Whisper**: only the agent hears you, so you can coach them.
   - **Barge**: you join the call; the agent and the customer both hear you.
3. The card shows *Connecting your phone…* until your phone answers, then *You: Listen* (or Whisper, or Barge). Your soft phone's banner reads *Connecting to (agent)'s call…*, then *Monitoring (agent)*.
4. To change how you are heard, click another mode on the card or on the banner. Your phone is not rung again; the active mode is shown pressed. The mode buttons are disabled until your phone is on the call.
5. Click **Stop** on the card or the banner when you are done. Only your part of the call ends; the agent and the customer stay connected.

If your soft phone does not answer, it tells you to close and reopen it, wait until it says it is ready, and try again.

An agent on a call that cannot be monitored shows **Cannot be monitored**. Point at it to see why:

- the agent has paused the recording to take card details (*A sensitive-data capture is in progress*), and monitoring comes back when it completes;
- the call's phone provider does not support monitoring;
- the agent is on a call of their own that the phone provider cannot let a supervisor join.

Calls an agent dialed from the keypad, and extension calls between colleagues, can be listened to, whispered to, barged into and ended like any other call. A keypad call can also be taken over. Neither can be transferred or recorded from the dashboard.

## Take over a call

Taking over hands the call to you and releases the agent, for example when a customer asks for a manager.

Before you start: you need the permission to take over calls, an agent profile of your own (created the first time you sign in to a queue), and your soft phone open. Your phone provider must support taking over (Telnyx does).

1. Click **Take over** on the agent's card. If you are not on the call yet, you join it first, and the call is taken over as soon as your phone is on it.
2. The customer hears you before the agent is released. Your soft phone's banner changes to *You took over (agent)'s call*, with **Hang up**.

From then on the call is yours: its talk time from that moment and its after-call work are recorded against you, and you get no other calls while you are on it. The agent goes to wrap-up (for a queue or campaign call) or back to ready (for a direct call). Extension calls cannot be taken over.

## End, transfer or record a call

Open the **⋮** menu at the right of the agent's name (*More actions for (agent)*). It shows only the actions your permissions and the call's phone provider allow.

- **End call…**: asks *End (agent)'s call for everyone on it?* Click **End call** to hang up the call for everyone.
- **Transfer…**: pick where to send the call under **Transfer to**: a queue, an available agent, or **A phone number** (type it in **Number**). Click **Transfer**. It is a blind transfer: the call goes straight there. Anyone listening, you included, is dropped first.
- **Start recording** / **Stop recording**: turns the call's recording on or off, under your company's recording rules. Not possible while the agent has paused recording to take card details.

## Set an agent's state or message them

These are in the same **⋮** menu, and work whether or not the agent is on a call.

- **Set Available**, **Set Not ready** or **Set Break** changes the agent's state under the same rules as their own change: an agent on a call gets the new state when their work ends. **Set Not ready** puts them in **Away**.
- **Sign out of queues** asks *Sign (agent) out of their queues?* and signs them out of everything.
- **Message…** sends a short note (up to 500 characters). It appears on the agent's soft phone as *Message from (your name)* until they dismiss it.

The state and sign-out actions need the permission to take over calls; **Message…** needs only the permission to monitor.

## Who can do what

| Action | Where | Permission |
| --- | --- | --- |
| **Listen**, **Whisper**, **Barge**, **Stop** | Agent card | Monitor |
| **Message…** | **⋮** menu | Monitor |
| **Take over** | Agent card | Take over calls |
| **End call…**, **Transfer…**, **Start recording** / **Stop recording** | **⋮** menu | Take over calls |
| **Set Available**, **Set Not ready**, **Set Break**, **Sign out of queues**, **Free agent…** | **⋮** menu | Take over calls |

*Monitor* is *Monitor the Contact Center in real time*; *Take over calls* is *Take over, end, transfer and record live Contact Center calls, and set agents' state*. Every action is recorded in the audit trail under your name.

:::note[About the screencast]
The demo site has no live calls, so the screencast shows the dashboard and the agent board. The intervention buttons appear on an agent's card while that agent is on a call.
:::
