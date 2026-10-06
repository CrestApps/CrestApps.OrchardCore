---
sidebar_label: Agent Workspace
sidebar_position: 30
title: Agent Workspace - Sign In, Presence and Work Offers
description: How an agent signs in to queues and campaigns, sets their presence, accepts work, and uses My workspace and the docked agent bar during a shift.
technical_manual:
  - contact-center/agent-desktop
  - contact-center/agents-queues-dialer
---

An agent works with three things on screen:

- the **soft phone** window (from the [browser extension or Windows app](phone-apps.md)), which carries the call audio and where you **sign in** to queues and campaigns (its **Work** tab);
- **My workspace**, which shows your presence, your queues, incoming offers, the call you are on and your recent work;
- the **docked agent bar** at the bottom of every admin page, which pops up with each offer so you can stay on any page of the CRM.

| | |
| --- | --- |
| **Menu** | Interaction Center > My workspace |
| **Permission** | Sign in to Contact Center queues and campaigns (the *Agent* role has it), and Use the telephony soft phone for the soft phone (the *Agent* role does not have it by default; an administrator adds it) |
| **Features** | Contact Center Agents, Contact Center Real-Time, Contact Center Voice, and Telephony Soft Phone Core (turned on with Telephony Soft Phone Extension) |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent signing in to a queue and a campaign from the soft phone Work tab and setting presence to Available">
  <source src="/img/docs/um-agent-signin.mp4" type="video/mp4" />
</video>

## Your shift at a glance

1. Open the soft phone and **My workspace**.
2. Sign in to the queues and campaigns you work today.
3. Set your presence to **Available** when you are ready, or pick a reason when you are not.
4. Accept or decline each offer. Dialer work opens its activity for you.
5. When a call ends, finish your notes, pick a disposition and complete the activity.
6. Check **Recent activity** to confirm your last outcomes before you take the next offer.

## Start your shift: sign in to queues and campaigns

You sign in from the soft phone, not from **My workspace**. The workspace only shows a chip for each queue you are signed in to.

1. Open the soft phone and choose the **Work** tab.
2. Under **Queues**, select the queues you work today (the box reads **Select queue(s)** until you pick one). Under **Campaigns**, select the dialer campaigns (**Select campaign(s)**). If your company uses [agent entitlements](skills-and-entitlements.md), you only see the queues and campaigns you are entitled to; otherwise you see them all.
3. Click **Sign in**. You must pick at least one queue or campaign; otherwise the tab says *Select at least one queue or campaign before signing in.*
4. You become **Available**, and the Work tab lists each queue and campaign you are signed in to, each with its own **Sign out** button. If callers are already waiting in one of your queues, you are offered the next one straight away.

To leave one queue or campaign and stay in the others, click its **Sign out**. At the end of your shift click **Sign out of all**. The page does not reload when you sign in or out.

## Set your presence

Pick your presence from the menu at the top of the soft phone, or from the presence button in **My workspace**.

- **Available** means you can receive work. When you switch back to Available, you are offered any call already waiting in your queues straight away.
- A **reason code** (for example *Team meeting* or *Away from desk*) makes you not ready and records why. Your manager sets up the reason codes on the [Agent states](agent-states.md) screen and decides which state each one puts you in. When no reason codes are set up, the soft phone menu offers **Away**, **Do not disturb**, **Meeting**, **Training** and **After-hours unavailable** instead.
- **Break**: break reasons (for example *Short break* or *Lunch*) are grouped under a **Break** heading; with no break reasons there is a single **Break** item. A break starts at once when nothing is being routed to you. While you have an offer, a call or wrap-up, the heading reads **Request break**, and the break starts by itself when that work ends. You get no new work while you are on a break or waiting for one.
- **Offline** (soft phone menu only) stops all work. **My workspace** has no Offline item.

A break that is waiting for your current work to end shows as **Break pending** next to your state, with the reason if you picked one (for example *Break pending: Lunch*).

## My workspace

<video controls preload="metadata" width="100%" aria-label="Screencast of the agent workspace showing presence, queue chips, the active interaction panel and recent activity">
  <source src="/img/docs/um-agent-workspace.mp4" type="video/mp4" />
</video>

Open **Interaction Center > My workspace**. Keep it open, with the soft phone, for your whole shift. From top to bottom:

| Area | What it shows |
| --- | --- |
| **Name, extension and presence** | Who is signed in, your [extension](extensions.md) (the `#` badge), and a presence button to change your state. |
| **Connection** | *Connected* while live updates work. *Connection lost. Reconnecting...* or *Disconnected. Live updates are paused.* means the page will not show new offers until it reconnects. |
| **Live dashboard** | A shortcut, shown to supervisors only. |
| **Queue chips** | The queues you are signed in to, with how many items are waiting in each, so you can see where the pressure is. Sign in and out from the soft phone Work tab. |
| **Offer card** | A new offer, with the caller, the queue and a countdown (*Respond in 25s*). Click **Accept** or **Decline**. See [Answer or decline an offer](#answer-or-decline-an-offer). |
| **Active interaction** | The interaction you are working: customer, direction, queue, number, status and talk time, with **Open customer record**, **Complete activity**, and the recording controls below. It says *No active interactions right now* until you accept something. It fills in while the call is live: an inbound call once you accept it, and an outbound dialer call (preview, power or progressive) as soon as it is dialed. The status follows the call (*Ringing*, *Connected*, *Held*). When the call ends it shows *Ended* for as long as you are in wrap-up, and clears once you complete the activity. A dial nobody answered clears straight away. The docked agent bar shows the same call. |
| **Recent activity** | Your latest interactions. See [Review your recent activity](#review-your-recent-activity). |

When you accept a preview or dialer offer that is set to open the activity automatically, the workspace goes straight to the activity's **Complete** page.

Use the soft phone for the call itself: hold, mute, keypad, transfer and hang up. See [Placing and Handling Calls](calls.md).

## Answer or decline an offer

When routing picks you for a call, an offer card appears in **My workspace** and the docked agent bar opens, showing the caller (name or number), the queue and a countdown.

1. Click **Accept** before the countdown ends. The card shows *Answering…* while the call connects, then the call moves into **Active interaction**. If the call does not connect, the card says *The call is taking too long to connect. Check your soft phone, then try again.*
2. Or click **Decline**. The caller goes back to the queue and is offered to another agent first. The call comes back to you only when nobody else can take it.

If you do nothing, the offer ends with the countdown and the queue's **Unanswered offer action** applies: the call is offered to the next agent (the default), sent to voicemail, or ended. See [Queues](queues.md). A call to your personal line that you decline or do not answer goes to your voicemail when the line is set to send unanswered calls to voicemail.

:::tip
If you refresh the page while an offer is ringing, the offer comes back, so you can still answer it.
:::

## Calls from a dialer campaign

When you are signed in to a campaign, the outbound dialer gives you calls in one of these ways, set by the campaign's [dialer profile](dialer-profiles.md).

**Preview**

1. The docked agent bar shows the offer (*Preview — review then dial*) with **Dial** and **Skip**, and the activity's **Complete activity** page opens so you can read the record first. It does not open if the page you are on has unsaved changes.
2. Read the customer and activity details.
3. Click **Dial**. The system places the call for you; you do not dial the number yourself. **Skip** passes on the record.
4. Talk to the customer, then complete the activity with a disposition.

**Power and progressive**

You do not press anything. Stay signed in to the campaign and **Available**. The dialer places the call, reserves you for it, and the activity's **Complete activity** page opens as the call connects. Handle the call like an inbound call, then complete the activity to become **Available** for the next one.

## Protect card details on a recorded call

When your administrator allows it, the **Active interaction** panel shows:

- **Pause recording** / **Resume recording**: stops the recording while the customer reads out card details or another sensitive number. If your company requires a reason, you are asked to *Enter a reason for pausing recording*. While the recording is paused the panel says *Recording paused for sensitive-data capture*. Click **Resume recording** as soon as the customer has finished. If you forget, the recording resumes on its own after the maximum pause time your administrator set.
- **Collect data securely**: creates a one-time link for the customer to type their card or bank details themselves, on their own device. The link is copied for you and shown so you can send it. Only a masked value (such as the last four digits of a card) is kept; you, your supervisor and the recording never see the full value. Only one secure capture can run on a call at a time. If the customer does not finish, the link expires (after 5 minutes unless your administrator changed it).

While the recording is paused, supervisors cannot listen in to the call, and a supervisor who was already listening is dropped. Every pause and resume is recorded in the audit trail.

These need the *Pause recording on own live interactions* and *Initiate secure capture on own live interactions* permissions (the *Agent* role has both), the [Contact Center settings](contact-center-settings.md) to be switched on and, for pausing, a phone provider that can pause recording.

## The docked agent bar

<video controls preload="metadata" width="100%" aria-label="Screencast of the docked agent bar collapsed on an admin page and expanded to show the agent's status">
  <source src="/img/docs/um-agent-bar.mp4" type="video/mp4" />
</video>

The bar sits as a small tab at the bottom of every admin page while you are signed in. Click it to expand; it also opens by itself when an offer arrives, and collapses when you click elsewhere (except while an offer is ringing). It keeps working when the soft phone runs in its own window, so an offer reaches you on any page of the CRM.

| Offer | Buttons | What happens |
| --- | --- | --- |
| **Incoming call** | **Accept** / **Decline** | The bar beeps once. When you accept, the activity's **Complete activity** page opens. |
| **Preview** (*Preview — review then dial*) | **Dial** / **Skip** | The activity opens so you can read it first, unless the page you are on has unsaved changes. **Dial** places the call. |
| **Power or progressive call** | none | The system has already dialed; the activity opens as the call connects. |

During the call the bar shows the direction, contact, number, queue, status and a talk timer, plus **Open activity** or **Complete activity** and **Open customer record**. The headset icon opens **My workspace**. Your presence shows on the bar but is changed from the soft phone. The bar has no disposition controls; you pick the disposition on the activity's **Complete activity** page.

## After the call: wrap-up

A call that came from a queue or a campaign puts you in **Wrap-up** when it ends, so you can finish your notes. Wrap-up does not end on a timer; you end it by completing the activity:

1. Click **Complete activity** in the **Active interaction** panel.
2. Check the customer and activity details, and update the subject details if needed.
3. Pick a **disposition**. If the activity's subject flow requires one, you cannot complete without it.
4. Add notes if needed and save.

Completing returns you to **Available**, or starts the break you requested during the call. You get no new calls until then. Direct calls to you, and calls you dialed yourself, skip wrap-up.

If an activity is left open, wrap-up ends by itself after 15 minutes (a setting your administrator can change). The activity stays open, with no disposition recorded.

See [Activities](activities.md#complete-an-activity) for the Complete page.

## Review your recent activity

The workspace's **Recent activity** panel lists the interactions you finished most recently: the direction, the customer's number, the outcome, when it ended and the talk time. It says *No recent interactions* until you finish your first one.

The soft phone keeps your history too, and both tabs update as calls end:

- **Recent** lists your latest calls, inbound and outbound, with their direction, outcome and time, and a **Call** button to dial the number back.
- **Voicemail** lists the voicemails left for you. See [Voicemail](voicemail.md#listen-to-your-voicemail).
