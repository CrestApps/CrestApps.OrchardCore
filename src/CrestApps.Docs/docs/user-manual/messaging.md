---
sidebar_label: Messaging Workspace
sidebar_position: 33
title: Messaging Workspace - Inbox, Transfers, Templates and Broadcasts
description: Answer SMS conversations from a shared inbox, claim and transfer them, start new conversations, reuse canned responses, and send broadcasts.
---

The **Messaging workspace** is a shared inbox for text conversations with customers. Messages sent to a department number land in that queue's shared inbox, where any agent can claim them; messages to an agent's personal number go straight to that agent.

| | |
| --- | --- |
| **Menu** | Messaging > Inbox, Messaging > Broadcasts, Messaging > Templates |
| **Permissions** | Use the messaging workspace (agents); Send group messages (broadcasts); Manage messaging (templates) |
| **Features** | Omnichannel Messaging Workspace (`CrestApps.OrchardCore.Omnichannel.Messaging`) and SMS Messaging Channel (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`) |

## Before you start (administrator)

1. Enable **SMS Messaging Channel**. It turns on the workspace and its dependencies.
2. Set up the SMS provider under **Settings > Communication > SMS** (see [Phone and SMS setup](telephony-settings.md)).
3. Add each SMS number under **Interaction Center > Management > Omnichannel Addresses** with **Text messages (SMS)** ticked, then add a **Text messages** entry point under **Interaction Center > Management > Inbound entry points** that sends its texts to an agent or a queue. See [Text entry points](entry-points-and-ivr.md#text-entry-points).

## Work the inbox

<video controls preload="metadata" width="100%" aria-label="Screencast of the messaging inbox: filtering conversations, opening a thread, claiming it and using a canned response">
  <source src="/img/docs/um-messaging-inbox.mp4" type="video/mp4" />
</video>

Open **Messaging > Inbox**. The menu shows how many unread conversations are waiting for you.

| Control | What it does |
| --- | --- |
| **Search** | Narrows the conversation list. |
| **Filter** (funnel button) | **All conversations**, **My conversations**, or **Unassigned**, each with a count; and a channel filter when you have more than one channel. |
| **Available** switch | *Accept routed conversations to your inbox.* Only used by queues set to routed distribution. |
| **New conversation** (pencil button) | Starts a conversation with a contact or a number. |

Open a conversation to read the thread. At the top:

- **Claim** makes you the owner and removes it from the shared inbox.
- **Transfer** hands it to a person or back to a queue.
- **Close** or **Mark spam** finishes it; **Reopen** brings it back.
- The channel tabs show where the conversation happens and whether the customer can be reached there.

This screencast opens the **Unassigned** view, claims a conversation, closes it (a closed conversation shows *This conversation is closed and cannot be replied to*), and reopens it:

<video controls preload="metadata" width="100%" aria-label="Screencast of claiming an unassigned conversation, closing it and reopening it">
  <source src="/img/docs/um-messaging-actions.mp4" type="video/mp4" />
</video>

In the composer, type your reply and press **Enter** to send (**Shift+Enter** adds a line). **Insert a canned response...** fills in a [template](#templates). A counter shows the characters left when the channel has a limit. The side panel has an **AI summary** of the thread and the **Customer** card, with **View account** for the linked contact.

A banner warns you during the customer's **quiet hours** (outside the queue's business hours). It does not stop you sending.

### Send and receive attachments

Each channel accepts its own kinds of files. SMS takes pictures (JPEG, PNG, GIF and WebP); hover over the attach button to see what the conversation's channel accepts.

- **Attach a file** by dragging it onto the conversation, pasting it into the message box, or clicking the attach button beside the channel name. Each file shows above your message; the **×** removes it.
- You can send attachments on their own or with text, up to 10 at a time. On SMS, large photos are shrunk automatically so the carrier accepts them.
- Files the customer sends appear in the conversation. Click a picture to open it full size in a new tab; any other file downloads.
- If a file cannot be shown (for example, a customer sends a document by text message), the message says *An attachment could not be shown*, and its text is still there.

### Favorites

Keep the customers you message most one click away.

- **Add a favorite:** open their conversation and click **Add to favorites** at the top, beside **Transfer**, or on the **Customer** card. The button then reads **Favorite**; click it again to remove them.
- **See your favorites:** click the **star button** beside the filter above the conversation list, or choose **Favorites** in the filter menu. Each favorite shows their latest conversation; one with none yet says *No conversation yet: click to write* and opens a new message. Your favorites also appear in a row above the conversation list.

Your favorites are your own; other agents do not see them.

## Transfer a conversation

1. Click **Transfer**.
2. Choose **A person** and search by name, or **Back to a queue** and pick the queue.
3. Add an optional **Note for the recipient** (up to 500 characters). The note stays in the history; the customer never sees it.
4. Confirm. The new owner gets a notification on whatever admin page they are on.

## Start a new conversation

<video controls preload="metadata" width="100%" aria-label="Screencast of starting a new SMS conversation and sending it">
  <source src="/img/docs/um-messaging-send.mp4" type="video/mp4" />
</video>


1. Click the **New conversation** (pencil) button.
2. Pick **From**, the number you send from (it decides the channel).
3. In **To**, search for contacts; add other numbers under **Other**, separated by commas.
4. Type the **Message** and click **Send**.

More than one recipient needs the *Send group messages* permission and becomes a broadcast. You can also click the **Send SMS** button beside any phone field in the CRM.

## Templates

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a message template and sending a broadcast">
  <source src="/img/docs/um-messaging-templates-broadcasts.mp4" type="video/mp4" />
</video>

Templates are canned responses agents insert into a reply.

1. Open **Messaging > Templates** and click **Add template**.
2. Enter a **Name** agents will recognize and the **Body** text, then save.

## Broadcasts

A broadcast sends the same message to many people; each recipient gets their own one-to-one thread, and replies come back to the inbox.

1. Open **Messaging > Broadcasts** and click **New broadcast**.
2. Enter a **Name**, pick **Send from**, choose **Recipients (contacts)** and add **Additional addresses** (one per line or comma-separated).
3. Type the **Message** and click **Queue broadcast**.

The list shows each broadcast's status and how many messages were sent and failed. A queued broadcast cannot be cancelled.

## Keywords customers can send

| Keyword | Also accepted | What happens |
| --- | --- | --- |
| **STOP** | STOPALL, UNSUBSCRIBE, CANCEL, END, QUIT | The customer is opted out of SMS and gets a confirmation. |
| **START** | UNSTOP, YES | The customer is opted back in. |
| **HELP** | INFO | The customer gets the help reply. |

:::note Where replies come from
With Twilio, inbound texts reach the workspace through the Twilio webhook, `/api/twilio/webhook/sms`, which comes with the SMS channel. You do not need SMS Omnichannel Automation for it. Telnyx inbound texts use the Telnyx SMS webhook.
:::

:::note About the screencasts
The inbox and broadcast screencasts fill in replies and broadcasts without sending them; the new-conversation screencast sends a real text.
:::
