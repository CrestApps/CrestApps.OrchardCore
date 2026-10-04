---
sidebar_label: Entry Points & IVR
sidebar_position: 24
title: Inbound Entry Points and IVR Menus
description: Decide what happens when someone dials one of your numbers - which queue or person it rings, what happens after hours, where voicemail goes, and which keypad menu callers hear first.
---

An **inbound entry point** is the front door for one or more of your numbers, and the one place a number's inbound traffic is routed from. Each entry point answers one channel: **Voice calls** or **Text messages**. A number used for both has one entry point for its calls and one for its texts. A call entry point decides:

- **where** the call goes: a queue, one specific agent (a personal line), or an AI voice agent;
- **what callers hear first**: an optional keypad (IVR) menu;
- **what happens when you are closed**: hold, voicemail, overflow or reject;
- **where voicemail goes**: an agent's inbox or the queue's shared voicemail box.

A text entry point decides where the conversations go, how a queue hands them out, and the automatic replies, including one for when you are closed. See [Text entry points](#text-entry-points).

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Inbound entry points |
| **Permission** | Manage Contact Center queues |
| **Feature** | Contact Center Inbound Entry Points (`CrestApps.OrchardCore.ContactCenter.EntryPoints`), with Contact Center Inbound Voice (`CrestApps.OrchardCore.ContactCenter.InboundVoice`) for call entry points and SMS Messaging Channel (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`) for text entry points |

<video controls preload="metadata" width="100%" aria-label="Screencast of adding a voice calls entry point that picks a number from the address list and routes its calls to a queue, with business hours and voicemail settings">
  <source src="/img/docs/um-entry-point.mp4" type="video/mp4" />
</video>

## Create an entry point

1. Add the numbers first, under **Interaction Center > Management > [Omnichannel Addresses](channel-endpoints.md)**, with **Voice calls** or **Text messages (SMS)** ticked.
2. Open **Interaction Center > Management > Inbound entry points** and click **Add inbound entry point**. When more than one channel is available, a dialog asks what the entry point answers: click **Add** on the **Voice calls** or **Text messages** card.
3. Fill in the cards described below. A call entry point has five; the priority, closed-call, phone menu and voicemail settings are for calls only.
4. Click **Save**. Calls or texts to its numbers follow the new rules straight away.

### General

| Field | What it does |
| --- | --- |
| **Name** / **Description** | How the entry point is listed. Name is required. |
| **Answers** | The channel, chosen when the entry point is added. It cannot be changed. |
| **Numbers** | The numbers this entry point answers, picked from your [Omnichannel Addresses](channel-endpoints.md). Only numbers ticked for the entry point's channel are offered, and a number can be answered by one enabled entry point per channel. |
| **Enabled** | Disabled entry points answer nothing. |

Numbers that were typed on an entry point before entry points picked them from the address list were moved onto the address list when the site was upgraded, along with the entry point a phone number picked on its own screen and the number a queue mapped. An entry point imported from an older recipe can still list typed numbers; they keep routing, and the General card lists them so you can pick their addresses.

### Routing

| Field | What it does |
| --- | --- |
| **Route to** | **Queue** (the default), **Specific agent**, or an AI: **AI voice agent** for calls, offered when the Telnyx AI Voice Agent feature is on, and **AI agent** for texts, offered when SMS Omnichannel Automation is on. A specific-agent line rings one person and never falls back to a queue. |
| **AI agent** | The AI chat profile that answers the calls (AI voice agent routing). See [AI voice agent](#ai-voice-agent). |
| **Target queue** | The queue that receives the calls (queue routing). |
| **Priority** | Calls only. Lowest to Highest. Calls from this number jump ahead of lower-priority work in the queue. |
| **Target agent** | The person to ring (specific-agent routing). |
| **Send unanswered calls to voicemail** | Specific-agent lines only. When off, the caller keeps ringing until the agent answers or the caller hangs up. |
| **Ring window (seconds)** | How long to ring the agent before voicemail. 5 to 300, default 30. |

### Hours

| Field | What it does |
| --- | --- |
| **Business hours calendar** | When the entry point is open. Empty means *Always open*. See [Business hours](business-hours.md). |
| **Closed action** | Calls only. What to do with calls while closed: **Hold in queue** (the default), **Voicemail**, **Overflow** (to the overflow queue below), or **Reject**. On a specific-agent line every choice except Reject becomes Voicemail. |
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

## Clone an entry point

To start an entry point from an existing one, open its **Actions** menu in the entry point list and choose **Clone**. The form opens with every setting of the original, including its routing, hours, phone menu and voicemail, and the name **Copy of** the original's name. **Numbers** starts empty, because each number is answered by one entry point on each channel. Pick the new entry point's numbers and click **Save**.

## AI voice agent

A call entry point can hand its calls to an AI voice agent: an AI chat profile, such as one made from the **Answer calls at the front desk** template. This is the only place an AI is set to answer calls; texts are set the same way on a [text entry point](automated-ai.md#let-the-ai-answer-incoming-texts). Queues have no AI setting.

1. Make the profile under **Artificial Intelligence > Profiles**, with an initial prompt (what it says when it picks up) and the business details its prompt asks for.
2. On the entry point's **Routing** card, set **Route to** to **AI voice agent** and pick the profile under **AI agent**.
3. To let the AI pass callers to a person, set up the hand-off on the [subject flow](subjects.md) for the number.

While the entry point is open, the AI answers each call itself, greets the caller with its initial prompt and holds the conversation. The welcome message and phone menu are not used. The call becomes an automated activity with the AI's transcript, and it reaches an agent's queue only if the AI hands the caller over. While the entry point is closed, callers go to voicemail, or are refused when the closed action is **Reject**.

The AI voice agent needs the **Telnyx AI Voice Agent** feature. A realtime-capable chat deployment holds a live conversation; others take turns, speaking and then listening. If the feature is turned off after an entry point is set up, its calls are refused until you pick another target.

## Text entry points

<video controls preload="metadata" width="100%" aria-label="Screencast of adding a text messages entry point that routes a number's texts to a queue, with an auto-reply and a closed auto-reply">
  <source src="/img/docs/um-entry-point-texts.mp4" type="video/mp4" />
</video>

A text entry point has the **General**, **Routing** and **Hours** cards, with these settings added for texts:

| Field | Card | What it does |
| --- | --- | --- |
| **Route to: AI agent** | Routing | The AI chat profile under **AI agent** answers the texts itself, starting with the customer's first text. Offered when SMS Omnichannel Automation is on. See [Let the AI answer incoming texts](automated-ai.md#let-the-ai-answer-incoming-texts). |
| **Queue distribution** | Routing | Queue targets only. **Shared pool (claim to own)**: every agent in the queue sees the conversation and one claims it. **Routed (assign to an available agent)**: each new conversation is given to one available agent. Routed needs the *Omnichannel Messaging Routed Distribution* feature. |
| **Auto-reply** | Routing | A message sent back automatically to a contact who texts these numbers, at most once a day per conversation. Contacts who have opted out never receive it. |
| **Closed auto-reply** | Hours | Sent in place of the auto-reply to a contact who texts while the entry point is closed. Their texts still reach the queue or agent, to answer when you open. Empty sends the ordinary auto-reply at any time. |

A number with no enabled text entry point still receives texts; they land in the unassigned inbox.

Before text entry points, where a number's texts went was set on the number itself. When the site was upgraded, each number's settings became a text entry point named after the number (with " (SMS)" added when the name was taken, usually by the number's call entry point), with the same target, distribution and auto-reply. A number whose texts went nowhere got no entry point.

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

