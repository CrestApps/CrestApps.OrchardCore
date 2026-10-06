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

<video controls preload="metadata" width="100%" aria-label="Screencast of the live dashboard summary, queue tiles and agent board filters">
  <source src="/img/docs/um-live-dashboard.mp4" type="video/mp4" />
</video>

:::info[You see your own queues]
The dashboard shows the queues and campaigns on **your own** [agent entitlement](skills-and-entitlements.md) record, and you can only act on agents and calls in those queues. A supervisor without an entitlement record sees an empty dashboard, even with the Administrator role.
:::

## Reading the dashboard

The page updates as events happen and refreshes every 10 seconds. The status at the top says *Connected* while live updates work; *Connection lost. Reconnecting...* or *Disconnected. Live updates are paused.* means the numbers may be out of date.

| Area | What it shows |
| --- | --- |
| **Summary** | Total waiting across your queues, available agents, agents, and queues. |
| **Call-quality alerts** | An agent whose recent calls were rated poor, with the likely cause (for example packet loss on their network). Dismiss an alert once handled. |
| **Queue tiles** | For each queue: waiting, signed-in agents, available, busy and not-ready agents, the longest wait, and SLA breaches. A tile turns **amber** when the longest wait passes half the SLA threshold and **red** when there is a breach. |
| **Agent board** | One card per agent with presence, a status badge, active interactions and action buttons. |

Filter the agent board by **Agent** name, **Signed-in queue**, **Signed-in campaign** and **Status** (Available, Busy, On a call, Not ready, Offline). **Clear** resets the filters; the browser remembers them for next time. The board header shows how many agents match, for example *4 of 12 agents*.

Use the dashboard to spot a queue that is backing up, an SLA breach or too few available agents, then move people between queues or ask agents to come back from a break.

An agent on a call of their own (a number they dialed from the keypad, or an extension call) shows **On a call** with the number or colleague and how long the call has lasted.

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
| **Set Available**, **Set Not ready**, **Set Break**, **Sign out of queues** | **⋮** menu | Take over calls |

*Monitor* is *Monitor the Contact Center in real time*; *Take over calls* is *Take over, end, transfer and record live Contact Center calls, and set agents' state*. Every action is recorded in the audit trail under your name.

:::note[About the screencast]
The demo site has no live calls, so the screencast shows the dashboard and the agent board. The intervention buttons appear on an agent's card while that agent is on a call.
:::
