---
sidebar_label: Live Dashboard
sidebar_position: 34
title: Live Dashboard and Supervisor Interventions
description: Watch queues and agents in real time, filter the agent board, and listen to, whisper to, barge into, take over, transfer or end a live call.
---

The **live dashboard** is the supervisor's real-time view of the contact center: how many people are waiting, which agents are free, and who is on a call. From an agent's card a supervisor can step into the call.

| | |
| --- | --- |
| **Menu** | Interaction Center > Live dashboard |
| **Permission** | Monitor the Contact Center in real time (the *Supervisor* role has it) |
| **Feature** | Contact Center Supervision & Live Dashboard (`CrestApps.OrchardCore.ContactCenter.Supervision`) |

<video controls preload="metadata" width="100%" aria-label="Screencast of the live dashboard summary, queue tiles and agent board filters">
  <source src="/img/docs/um-live-dashboard.mp4" type="video/mp4" />
</video>

:::info You see your own queues
The dashboard shows the queues and campaigns on **your own** [agent entitlement](skills-and-entitlements.md) record. A supervisor without an entitlement record sees an empty dashboard, even with the Administrator role.
:::

## Reading the dashboard

The page updates as events happen and refreshes every 10 seconds.

| Area | What it shows |
| --- | --- |
| **Summary** | Total waiting, available agents, agents, and queues. |
| **Call-quality alerts** | An agent whose recent calls were rated poor, with the likely cause. Dismiss an alert once handled. |
| **Queue tiles** | For each queue: waiting, signed-in agents, available, busy and not-ready agents, the longest wait, and SLA breaches. A tile turns **amber** when the longest wait passes half the SLA threshold and **red** when there is a breach. |
| **Agent board** | One card per agent with presence, a status badge, active interactions and action buttons. |

Filter the agent board by **Agent** name, **Signed-in queue**, **Signed-in campaign** and **Status** (Available, Busy, On a call, Not ready, Offline). **Clear** resets the filters; the browser remembers them for next time.

## Step into a live call

The buttons on an agent's card depend on your permissions and on what the phone provider supports.

| Action | What it does | Permission |
| --- | --- | --- |
| **Listen** | Hear the call silently. | Monitor |
| **Whisper** | Talk to the agent only; the customer cannot hear you. | Monitor |
| **Barge** | Join the call; both sides hear you. | Monitor |
| **Message…** | Send the agent a short text (up to 500 characters) that pops up in their soft phone. | Monitor |
| **Take over** | Take the call yourself; the agent drops off. | Intervene in calls |
| **Transfer...** | Blind-transfer the call to a queue, an available agent or a number. | Intervene in calls |
| **End call...** | Hang up the call. | Intervene in calls |
| **Start / Stop recording** | Control the recording. | Intervene in calls |
| **Set Available**, **Set Not ready**, **Set Break**, **Sign out of queues** | Change the agent's state, from the agent card's **⋮** menu. | Intervene in calls |

While you listen, whisper or barge, a banner in your soft phone lets you **switch mode** or **Stop**. Listening is blocked while the agent has paused the recording for card details. Every action is recorded in the audit trail.

:::note About the screencast
The demo site has no live calls, so the screencast shows the dashboard and the agent board. The intervention buttons appear on an agent's card while that agent is on a call.
:::
