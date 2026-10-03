---
sidebar_label: Entry Points & IVR
sidebar_position: 24
title: Inbound Entry Points and IVR Menus
description: Decide what happens when someone dials one of your numbers - which queue or person it rings, what happens after hours, where voicemail goes, and which keypad menu callers hear first.
---

An **inbound entry point** is the front door for one or more phone numbers. It decides:

- **where** the call goes: a queue, or one specific agent (a personal line);
- **what callers hear first**: an optional keypad (IVR) menu;
- **what happens when you are closed**: hold, voicemail, overflow or reject;
- **where voicemail goes**: an agent's inbox or the queue's shared voicemail box.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Inbound entry points |
| **Permission** | Manage Contact Center queues |
| **Feature** | Contact Center Inbound Voice (`CrestApps.OrchardCore.ContactCenter.InboundVoice`) |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an inbound entry point that routes a dialed number to a queue with business hours and voicemail settings">
  <source src="/img/docs/um-entry-point.mp4" type="video/mp4" />
</video>

## Create an entry point

1. Open **Interaction Center > Management > Inbound entry points** and click **Add inbound entry point**.
2. Fill in the five cards described below.
3. Click **Save**. Calls to the dialed numbers follow the new rules straight away.

### General

| Field | What it does |
| --- | --- |
| **Name** / **Description** | How the entry point is listed. Name is required. |
| **Dialed numbers** | One phone number (DID) per line, in international format such as `+17025550100`. Every call to one of these numbers uses this entry point, unless the number's own [channel endpoint](channel-endpoints.md#inbound-routing-for-phone-numbers) picks a different entry point. |
| **Enabled** | Disabled entry points receive no calls. |

### Routing

| Field | What it does |
| --- | --- |
| **Route to** | **Queue** (the default) or **Specific agent**. A specific-agent line rings one person and never falls back to a queue. |
| **Target queue** | The queue that receives the calls (queue routing). |
| **Priority** | Lowest to Highest. Calls from this number jump ahead of lower-priority work in the queue. |
| **Target agent** | The person to ring (specific-agent routing). |
| **Send unanswered calls to voicemail** | Specific-agent lines only. When off, the caller keeps ringing until the agent answers or the caller hangs up. |
| **Ring window (seconds)** | How long to ring the agent before voicemail. 5 to 300, default 30. |

### Hours

| Field | What it does |
| --- | --- |
| **Business hours calendar** | When the entry point is open. Empty means *Always open*. See [Business hours](business-hours.md). |
| **Closed action** | What to do with calls while closed: **Hold in queue** (the default), **Voicemail**, **Overflow** (to the overflow queue below), or **Reject**. On a specific-agent line every choice except Reject becomes Voicemail. |
| **Overflow queue** | The queue that takes after-hours calls when the closed action is Overflow. |
| **Closed message** | Spoken to a caller who rings while the entry point is closed, before the closed action. Once it has been said the caller is held in the queue, moved to the overflow queue or sent to voicemail; with **Reject** the call ends after the message. Empty applies the closed action straight away, with no message. |

### Welcome and IVR menu

The IVR menu is optional. With no menu, callers go straight to the routing target.

The **Welcome message** is spoken to every caller who rings while the entry point is open: before the first menu's prompt, or, with no menu, before the caller is put through to the target queue or agent. Each caller hears it once. It is not repeated when a caller goes back to the main menu, presses a key the menu does not offer, or is moved on to another queue. Empty means nothing is said.

To speak the welcome, the call is answered. On a line with no menu the caller then hears the queue's hold music, or a ringing tone, instead of the phone network's ringing while they wait for an agent.

### Voicemail

| Field | What it does |
| --- | --- |
| **Default voicemail greeting** | Spoken when the person receiving the voicemail has not recorded [their own greeting](voicemail.md#record-your-voicemail-greeting). Empty uses the system default. |
| **Deliver voicemail to** | Queue lines only: **An agent's inbox** (the default) or **The queue's shared voicemail box**, which any entitled supervisor or agent can pick up from [Shared voicemail](voicemail.md#shared-voicemail). |
| **Voicemail inbox** | The agent whose inbox receives the voicemail. Hidden when the shared box is chosen. |

## Build an IVR menu

<video controls preload="metadata" width="100%" aria-label="Screencast of building a two-level IVR menu with queue, agent, voicemail and submenu key actions">
  <source src="/img/docs/um-ivr-menu.mp4" type="video/mp4" />
</video>

An IVR menu is a set of **menus**. Each menu has a prompt and a list of **keys**; each key has an **action**. Callers answer with the telephone keypad; speech input is not supported.

1. Edit the entry point and, on the **Welcome and IVR menu** card, click **Build an IVR menu**. A **Main menu**, marked **First menu**, appears with one key row.
2. In **What callers hear**, type the prompt, for example *"For support press 1. To talk to Taylor press 2. To leave a message press 9."* You can pick a **Recorded prompt** from [Voice media](voice-media.md) instead.
3. For each choice, set the row's **Key**, **Action** and **Target**, and click **Add key** for the next one:

   | Action | What happens |
   | --- | --- |
   | **Send to a queue** | The caller waits in the chosen queue. |
   | **Send to an agent** | Rings the chosen agent. |
   | **Open a submenu** | Plays another menu, which you then fill in the same way. |
   | **Go to another menu** | Jumps back to a menu you already built, such as the main menu. |
   | **Send to voicemail** | Records a voicemail using the entry point's voicemail settings. |
   | **Transfer to an approved external number** | Connects the caller to a number on the [approved destinations list](contact-center-settings.md#external-transfer-destinations). |
   | **Repeat this menu** | Plays the prompt again. |

4. At the top, set **Tries**, the number of wrong or missing keys a caller is allowed (default 3), and **When the tries run out** (the default, *Route to the entry point target*, sends the caller where the entry point routes calls with no menu).
5. Click **Save**. Until every problem is fixed, the builder shows how many there are and the entry point will not save: a menu needs a prompt or recording and at least one key, a key can only be used once per menu, and queue, agent and transfer actions need a target. Allowed keys are 0-9, `*` and `#`.

**Remove the IVR menu** takes the menu off the entry point. **Edit as JSON** shows the same menu as JSON, which is handy for copying a menu between entry points.

A wrong key, no key, or **Repeat this menu** each use up one try; entering a new menu starts the count again. When a choice cannot be reached (the queue is disabled, for example), the caller goes to the entry point's target, and then to voicemail. The menu only plays while the entry point is open.

