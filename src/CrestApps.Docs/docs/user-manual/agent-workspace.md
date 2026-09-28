---
sidebar_label: Agent Workspace
sidebar_position: 30
title: Agent Workspace - Sign In, Presence and Work Offers
description: How an agent signs in to queues and campaigns, sets their presence, accepts work, and uses My workspace and the docked agent bar during a shift.
---

An agent works with three things on screen:

- the **soft phone** window (from the [browser extension or Windows app](phone-apps.md)), which carries the call audio and where you **sign in** to queues and campaigns (its **Work** tab);
- **My workspace**, which shows your presence, your queues, incoming offers, the call you are on and your recent work;
- the **docked agent bar** at the bottom of every admin page, which pops up with each offer so you can stay on any page of the CRM.

| | |
| --- | --- |
| **Menu** | Interaction Center > My workspace |
| **Permission** | Sign in to queues and campaigns (the *Agent* role has it) |
| **Features** | Contact Center Agents, Contact Center Real-Time, Contact Center Voice, and Telephony Soft Phone Extension |

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent signing in to a queue and a campaign from the soft phone Work tab and setting presence to Available">
  <source src="/img/docs/um-agent-signin.mp4" type="video/mp4" />
</video>

## Start your shift: sign in to queues and campaigns

1. Open the soft phone and choose the **Work** tab.
2. Under **Queues**, select the queues you work today. Under **Campaigns**, select the dialer campaigns. You only see the ones you are [entitled to](skills-and-entitlements.md).
3. Click **Sign in**. You become **Available**, and the Work tab lists each membership with its own **Sign out** button.
4. At the end of your shift click **Sign out of all**.

## Set your presence

Pick your presence from the menu at the top of the soft phone, or from the presence button in **My workspace**.

- **Available** means you can receive work.
- A **reason code** (for example *Team meeting* or *Away from desk*) makes you not ready and records why.
- Break reasons (*Short break*, *Lunch*) are grouped under **Break**. While you are busy with work the heading reads **Request break**, and the break starts when the work ends.
- **Offline** (soft phone menu only) stops all work.

A change that is waiting for the current work to end shows as *Label · Pending*. Your manager sets up the reason codes on the [Agent states](agent-states.md) screen.

## My workspace

<video controls preload="metadata" width="100%" aria-label="Screencast of the agent workspace showing presence, queue chips, the active interaction panel and recent activity">
  <source src="/img/docs/um-agent-workspace.mp4" type="video/mp4" />
</video>

Open **Interaction Center > My workspace**. From top to bottom:

| Area | What it shows |
| --- | --- |
| **Name, extension and presence** | Who is signed in, your [extension](extensions.md) (the `#` badge), and a presence button to change your state. |
| **Connection** | *Connected* while live updates work. *Reconnecting...* or *Disconnected* means the page will not show new offers until it reconnects. |
| **Live dashboard** | A shortcut, shown to supervisors only. |
| **Queue chips** | The queues you are signed in to, with how many items are waiting in each. Sign in and out from the soft phone Work tab. |
| **Offer card** | A new offer, with the caller, the queue and a countdown (*Respond in 25s*). Click **Accept** or **Decline**. An unanswered offer follows the queue's *Unanswered offer action*. |
| **Active interaction** | The interaction you are working: customer, direction, queue, number, status and talk time, with **Open customer record**, **Complete activity**, and the recording controls below. For an outbound dialer call it fills in when the call ends and wrap-up starts. |
| **Recent activity** | Your latest interactions with their status, time and talk time. |

When you accept a preview or dialer offer that is set to open the activity automatically, the workspace goes straight to the activity's **Complete** page.

### Protect card details on a recorded call

When your administrator allows it, the **Active interaction** panel shows:

- **Pause recording** / **Resume recording**: stops the recording while the customer reads out card details. You may be asked for a reason. The recording resumes on its own after the maximum pause window.
- **Collect data securely**: creates a one-time link for the customer to type their card or bank details themselves, and copies it for you to send.

These need the *Pause recording* and *Initiate secure capture* permissions and the [Contact Center settings](contact-center-settings.md) to be switched on.

## The docked agent bar

<video controls preload="metadata" width="100%" aria-label="Screencast of the docked agent bar collapsed on an admin page and expanded to show the agent's status">
  <source src="/img/docs/um-agent-bar.mp4" type="video/mp4" />
</video>

The bar sits as a small tab at the bottom of every admin page while you are signed in. Click it to expand; it also opens by itself when an offer arrives, and collapses when you click elsewhere (except while an offer is ringing).

| Offer | Buttons | What happens |
| --- | --- | --- |
| **Incoming call** | **Accept** / **Decline** | The bar beeps once. When you accept, the customer's record opens. |
| **Preview** (*Preview - review then dial*) | **Dial** / **Skip** | The record opens so you can read it first, unless the page you are on has unsaved changes. **Dial** places the call. |
| **Power or progressive call** | none | The system has already dialed; the record opens as the call connects. |

During the call the bar shows the direction, contact, number, queue, status and a talk timer, plus **Open activity** or **Complete activity** and **Customer record**. The headset icon opens **My workspace**. Your presence shows on the bar but is changed from the soft phone.

## After the call: wrap-up

A call that came from a queue or a campaign puts you in **Wrap-up** when it ends, so you can finish your notes. Complete the activity (pick a disposition and save) to become available again. Direct calls to you, and calls you dialed yourself, skip wrap-up. If an activity is left open, wrap-up ends by itself after 15 minutes (a setting your administrator can change).

See [Activities](activities.md#complete-an-activity) for the Complete page.
