---
sidebar_label: Queues
sidebar_position: 20
title: Queues and Queue Groups
description: Create queues, choose how the next agent is picked, and decide what callers hear and what happens when nobody answers, the queue is full, or you are closed.
---

A **queue** is where waiting work lines up until an agent takes it: an inbound call, a callback, or a message sent to a department. The queue decides **which agent** gets the next item, **how long** an offer rings, **what callers hear** while they wait, and **where they go** when you are closed or too busy.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Queues (and Queue groups) |
| **Permission** | Manage Contact Center queues (queue groups also accept Manage Contact Center queue groups) |
| **Feature** | Contact Center Work Distribution (`CrestApps.OrchardCore.ContactCenter.Queues`) |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a queue group and a queue with routing, service level, hours and caller treatment settings">
  <source src="/img/docs/um-queues.mp4" type="video/mp4" />
</video>

## Create a queue group (optional)

Queue groups only organize queues for administration and reports, for example *Sales* and *Service*. They never change routing.

1. Open **Interaction Center > Management > Queue groups** and click **Add queue group**.
2. Enter a **Name** and, optionally, a **Description**, then **Save**.

## Create a queue

1. Open **Interaction Center > Management > Queues** and click **Add Queue**.
2. Fill in the cards described below. Only **Name** is required; every other value has a sensible default.
3. Click **Save**.

To send phone calls to the queue, point an [inbound entry point](entry-points-and-ivr.md) at it, or pick the phone number in the queue's **Inbound channel endpoint** field. Agents take work from the queue once they [sign in to it](agent-workspace.md).

### General

| Field | What it does |
| --- | --- |
| **Name** | The name agents and supervisors see. Required. |
| **Description** | A note for administrators. |
| **Queue group** | The group the queue is reported under. Leave empty for *No queue group*. |
| **Enabled** | A disabled queue receives no new assignments. |

### Routing

| Field | What it does |
| --- | --- |
| **Default priority** | Lowest, Low, **Normal**, High or Highest. Higher-priority items are offered first. |
| **Routing strategy** | How the next agent is chosen: **Longest idle** (the agent who has been available the longest, the default), **Round robin** (the agent who least recently got work), or **Least busy** (the agent with the fewest active interactions). |
| **Prefer the last assigned agent (sticky)** | When a returning customer's previous agent is available and eligible, offer them the work first. |
| **Reservation timeout (seconds)** | How long an offer rings for one agent before the unanswered-offer action applies. Default 30. |
| **Unanswered offer action** | **Requeue** (offer it to the next agent, the default), **Send to voicemail**, or **Reject call** (end the call). |

### Service levels

| Field | What it does |
| --- | --- |
| **Service Level Agreement (SLA) threshold (seconds)** | A waiting item that waits longer than this breaches the service level. The live dashboard counts breaches. Default 120. |
| **Age waiting items past the SLA threshold** | Raise the priority of an item by one level for every full threshold it waits past the SLA, so old items are not stuck behind new high-priority ones. |
| **First-response target (seconds)** | Messaging only: how quickly the first reply to a new conversation should go out. 0 means no target. |

### Skills

| Field | What it does |
| --- | --- |
| **Required skills** | Agents must hold every skill picked here at proficiency 3 or higher. |
| **Skill requirements** | Finer rules, one row per skill: **Minimum proficiency** (1-5); **Required** (unticked means *preferred*, which only ranks agents rather than excluding them); and **Relax after (s)**, which drops the requirement once an item has waited that long (0 means never). |

Agents get their skills on the [agent entitlements](skills-and-entitlements.md) screen.

### Hours and overflow

| Field | What it does |
| --- | --- |
| **Inbound channel endpoint** | The phone number whose calls route to this queue. Leave empty to use the *default inbound queue*, which only works when exactly one queue has no number. |
| **Business hours calendar** | The [calendar](business-hours.md) that says when the queue is open. Empty means *Always open*. Routing pauses while the queue is closed. |
| **After-hours action** | **Hold in queue** (the default) or **Overflow** to another queue while closed. |
| **Overflow queue** / **Overflow after (seconds)** | A single overflow hop: after this many seconds of waiting, move the caller to the overflow queue. 0 means no time-based overflow. |
| **Overflow chain** | Several hops, each with a queue and a wait in seconds. It replaces the single hop. A caller never returns to a queue they have already been in. |

### Limits

| Field | What it does |
| --- | --- |
| **Maximum callers waiting** | 0 means no limit. |
| **Queue-full action** | **Admit anyway** (the default), **Overflow to the first queue with room**, or **Send to voicemail**. |
| **Maximum wait (seconds)** | 0 means no limit. |
| **Maximum-wait action** | **Keep waiting** (the default), **Overflow to the next queue**, or **Send to voicemail**. An overflow action needs an overflow queue or chain. |

### While callers wait

| Field | What it does |
| --- | --- |
| **Welcome message** | Spoken once when the caller enters the queue. |
| **Hold music** | A recording from [Voice media](voice-media.md). Empty uses the phone provider's default. |
| **Announcement interval (seconds)** | How often to interrupt the music with an update. 0 means no announcements. |
| **Announce the caller's place in line** / **Announce the estimated wait** | What the update says. The estimate is place in line × average handle time ÷ available agents. |
| **Average handle time (seconds)** | Used for the estimate. With 0, no estimate is spoken. |
| **Minimum / Maximum announced estimate (seconds)** | Keeps the spoken estimate between these bounds (30 and 1800 by default). |

### Callback

| Field | What it does |
| --- | --- |
| **Callback key** | The key (0-9, * or #) a waiting caller presses to hang up and get a call back instead. Empty means callbacks are not offered. |
| **Offer callback after (seconds)** | How long a caller waits before the callback is offered. |

A caller who presses the callback key hangs up and keeps their place in line: the callback is scheduled with the time they first entered the queue, and it is offered to an agent as outbound work. Callbacks need the **Contact Center Outbound Dialer** feature; without it no callback is stored.
