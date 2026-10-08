---
sidebar_label: Voicemail
sidebar_position: 32
title: Voicemail - Your Greeting, Your Messages and Shared Voicemail
description: Record your own voicemail greeting, listen to your messages in the soft phone, and work a queue's shared voicemail box as a team.
technical_manual:
  - contact-center/agent-desktop
  - contact-center/voice-routing
---

Voicemail goes to one of two places, chosen on the [inbound entry point](entry-points-and-ivr.md#voicemail):

- **your own inbox**, which you play from the soft phone's **Voicemail** tab;
- a **queue's shared voicemail box**, which the team works from **Interaction Center > Shared voicemail**.

## Record your voicemail greeting

Your greeting is what callers hear before they leave you a message.

| | |
| --- | --- |
| **Menu** | Interaction Center > My voicemail greeting |
| **Permission** | Sign in to Contact Center queues and campaigns (you also need an agent profile, which is created the first time you sign in) |
| **Features** | Contact Center Agents, Contact Center Real-Time, Contact Center Voice, and Telephony Soft Phone Core (turned on with Telephony Soft Phone Extension) |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the My voicemail greeting page with record, upload and preview controls">
  <source src="/img/docs/um-voicemail-greeting.mp4" type="video/mp4" />
</video>

1. Open **Interaction Center > My voicemail greeting**.
2. Choose how to add the greeting:
   - **Record with microphone** (the default): click **Record**, speak, then click **Stop** (the same button). The page says *Recorded. Review it below, then Save.* If your browser cannot record, it says so; upload a file instead.
   - **Upload a file**: pick an audio file (MP3, WAV, OGG or WebM, up to 20 MB).
3. Listen to the **Preview**, then click **Save greeting**.

**Remove greeting** goes back to the default greeting. When you have no greeting of your own, callers hear the entry point's **Default voicemail greeting**, and when that is empty too, a built-in greeting.

## Listen to your voicemail

Open the soft phone and choose the **Voicemail** tab. A badge on the tab shows how many messages are unread; opening the tab marks them read and clears the badge. Each message shows the caller's number and the time.

- **Play** a message with the play button on its row. The player keeps the caller's number and time in view while it plays. Every playback is recorded in the audit trail under your name.
- **Delete** messages: tick one or more rows (or select all) and click **Delete**. Deleting removes the message and erases its recording. If some cannot be deleted, the tab says how many, and each one left stays selected with the reason on its row, for example *This voicemail is under legal hold and must be kept.*

### Whose inbox a voicemail goes to

You can play and delete exactly the voicemails in your own Voicemail tab. Nobody can play or delete a voicemail in another person's inbox.

| How the call reached voicemail | Whose inbox |
| --- | --- |
| A call to you (your extension, or your personal line) that you did not answer | Yours. |
| You clicked **Voicemail** on a ringing call | Yours. |
| A queue call offered to you that you let ring out, on a queue whose **Unanswered offer action** is **Send to voicemail** | Yours, whatever the entry point says. |
| A queue call that reached the queue's voicemail after its maximum wait, after an offer to you ran out | Yours, as the last agent it was offered to. |
| Any other queue call that reached voicemail (the caller chose voicemail in the phone menu, the queue was full, or they waited too long before anyone was offered the call) | The agent set as the entry point's **Voicemail inbox**. With nobody set, the message is recorded but is in nobody's inbox. |

When the entry point delivers to **The queue's shared voicemail box**, queue voicemail goes to no one's inbox: it is on the **Shared voicemail** page instead (see below), including a caller who reached voicemail after their maximum wait. A call that rang out on one agent still goes to that agent.

Otherwise, queue voicemail is not shared among the queue's agents and is not listed for supervisors. To let a supervisor hear the messages left on a queue line, ask your administrator to set the entry point's **Voicemail inbox** to that supervisor; the messages then appear in their own **Voicemail** tab.

## Shared voicemail

A queue's shared voicemail box lets a team share the messages left on a line: anyone working the box can claim a message, call the caller back and mark it done, so nothing is missed and nobody calls the same customer twice.

| | |
| --- | --- |
| **Menu** | Interaction Center > Shared voicemail (the menu shows how many are new) |
| **Permissions** | Access shared queue voicemail for entitled queues (to work the box); Manage shared queue voicemail: delete messages and take over other users' claims. The *Supervisor* role has both. |
| **Feature** | Contact Center Inbound Voice |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the shared voicemail list with queue and status filters and the claim, call back and mark done actions">
  <source src="/img/docs/um-shared-voicemail.mp4" type="video/mp4" />
</video>

1. Open **Interaction Center > Shared voicemail**.
2. Filter by queue (*All my queues* by default) and status: **Open (new and claimed)**, **New**, **Claimed**, **Done** or **All**, then click **Filter**.
3. Play a message, then click **I'll handle this** to claim it so the rest of the team knows it is yours. It then shows *Claimed by you*.
4. Click **Call back** to call the caller back straight from your own soft phone.
5. When you are finished, click **Mark done** and add an optional note (up to 1000 characters).

Other actions: **Return to queue** releases your claim, **Reopen** brings back a finished message, **Take over** claims a message someone else is holding (needs the manage permission), and **Delete** removes the message and erases its recording (needs the manage permission; not allowed under legal hold).

You see the queues on your own [entitlement record](skills-and-entitlements.md); users with *Manage the Contact Center* see every queue.
