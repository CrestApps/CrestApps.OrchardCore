---
sidebar_label: Messaging Workspace
sidebar_position: 33
title: Messaging Workspace - Inbox, Transfers, Templates and Broadcasts
description: Answer SMS conversations from a shared inbox, claim and transfer them, start new conversations, reuse canned responses, and send broadcasts.
technical_manual:
  - omnichannel/messaging-workspace
  - telephony/telnyx
---

The **Messaging workspace** is a shared inbox for text conversations with customers. Messages sent to a department number land in that queue's shared inbox, where any agent can claim them; messages to an agent's personal number go straight to that agent. Use it when people, not the AI, answer your customers' texts.

| | |
| --- | --- |
| **Menu** | Messaging > Inbox, Messaging > Broadcasts, Messaging > Templates |
| **Permissions** | Use the messaging workspace, plus one of: View your own messaging conversations; View your own and your queues' unclaimed messaging conversations (agents); View all messaging conversations (supervisors). Send group messages (broadcasts); Manage the messaging workspace (templates) |
| **Features** | Omnichannel Messaging Workspace and SMS Messaging Channel |

<AskYourAdmin />

## Before you start (administrator)

1. Enable **SMS Messaging Channel** under **Tools > Features**. It turns on the Omnichannel Messaging Workspace and everything it needs. Without a channel such as SMS, the inbox has nothing to send or receive on.
2. Set up the SMS provider under **Settings > Communication > SMS** (see [Phone and SMS setup](telephony-settings.md)).
3. Add each SMS number under **Interaction Center > Management > Omnichannel Addresses** with **Text messages (SMS)** ticked (see [Omnichannel addresses](channel-endpoints.md)), then add a **Text messages** entry point under **Interaction Center > Management > Inbound entry points** that sends its texts to an agent or a queue. See [Text entry points](entry-points-and-ivr.md#text-entry-points).
4. Give the roles that staff the inbox the permissions in the table above. The permissions cover every channel; there is no separate permission per channel.

Your administrator also connects the SMS provider to the site, so that customers' texts and delivery receipts reach the inbox. For Twilio, the address to give Twilio shows as **Webhook URL** on the Twilio tab of **Settings > Communication > SMS**. The [technical manual](../omnichannel/messaging-workspace.md#setting-up-sms) has the details.

## Work the inbox

<video controls preload="metadata" width="100%" aria-label="Screencast of the messaging inbox: filtering conversations, opening a thread, claiming it and using a canned response">
  <source src="/img/docs/um-messaging-inbox.mp4" type="video/mp4" />
</video>

Open **Messaging > Inbox**. The left side lists your customers, most recent first; the right side shows the conversation you open. A customer who wrote on several channels appears only once, with the unread messages of all their channels added together and an icon for each channel they used.

| Control | What it does |
| --- | --- |
| **Search** | Narrows the conversation list. |
| **Filter** (funnel button) | **All conversations**, **My conversations**, or **Unassigned**, each with a count; and a channel filter when you have more than one channel. |
| **Available** switch | *Accept routed conversations to your inbox.* Only used by queues set to routed distribution: new conversations are given only to agents who are available and have the inbox open. |
| **New conversation** (pencil button) | Starts a conversation with a contact or a number. |

Open a conversation to read the thread. At the top:

- **Claim** makes you the owner and removes it from the shared inbox. Replying to a conversation nobody holds claims it for you too. Replying never takes a conversation from an agent who already holds it.
- **Transfer** hands it to a person or back to a queue. See [Transfer a conversation](#transfer-a-conversation).
- **Close** or **Mark spam** finishes it; **Reopen** brings it back.
- The **channel tabs** show one icon per channel. The channel on screen is highlighted. A channel with unread messages carries a red badge with the count. A channel where the customer has an address but no conversation yet opens a new message on that channel. A channel where the customer cannot be reached is greyed out.

This screencast opens the **Unassigned** view, claims a conversation, closes it (a closed conversation shows *This conversation is closed and cannot be replied to*), and reopens it:

<video controls preload="metadata" width="100%" aria-label="Screencast of claiming an unassigned conversation, closing it and reopening it">
  <source src="/img/docs/um-messaging-actions.mp4" type="video/mp4" />
</video>

In the composer, type your reply and press **Enter** to send (**Shift+Enter** adds a line). **Insert a canned response...** fills in a [template](#templates). A counter shows the characters left when the channel has a limit. The side panel has an **AI summary** of the thread and the **Customer** card, which lists every contact record that matches the customer's address, with **View account** for the linked contact.

When the AI was handling a customer's texts and hands the conversation to a person, the whole AI conversation is copied into the thread, so you see every message the customer and the AI exchanged, in order.

### Quiet hours

A banner above the composer warns you during the customer's **quiet hours**: outside the queue's business hours, in the customer's local time. It never stops you sending. Without the *Send messages outside business hours* permission, the banner adds *Sending now may reach the contact at an unsociable hour.*

### Know when new work arrives

You do not have to keep the inbox open to hear about new work.

- The **Messaging > Inbox** menu item shows how many conversations are waiting for you: the unread open conversations assigned to you, plus the unread open conversations nobody has taken yet in the queues you serve. The **Messaging** menu icon turns red while that number is above zero, so a collapsed menu still shows that something is waiting. The number drops as soon as you open the conversation.
- On any other admin page, a notice pops up at the bottom right when a customer writes on a conversation you can see (*New message from* the customer's number), when a conversation is transferred to you, and when one is sent back to a queue you serve. Click the notice to open the conversation. It closes on its own after a few seconds.
- The notices and the count start once you have opened **Messaging > Inbox** for the first time, because that first visit ties you to your queues.
- New messages appear in the open conversation as they arrive, and the customer moves to the top of the list. If your connection drops, the conversation catches up within seconds and the list within half a minute.

Browsing other admin pages does not count as being at the inbox: queues set to routed distribution only hand new conversations to agents whose **Inbox** is open.

### Send and receive attachments

Each channel accepts its own kinds of files. SMS takes pictures (JPEG, PNG, GIF and WebP); hover over the attach button to see what the conversation's channel accepts. Anything else is refused.

- **Attach a file** by dragging it onto the conversation, pasting it into the message box, or clicking the attach button beside the channel name. Each file shows above your message; the **×** removes it.
- You can send attachments on their own or with text, up to 10 at a time. On SMS, large photos are shrunk automatically so the carrier accepts them. An animated GIF is never shrunk, because it would lose its animation, so a GIF that is too large is refused.
- Pictures can be sent from Telnyx and Twilio numbers. With any other SMS provider, a message with pictures is not sent at all and shows as failed.
- Files the customer sends appear in the conversation. Click a picture to open it full size in a new tab; any other file downloads. Only people who can open the conversation can see its pictures.
- If a file cannot be shown (for example, a customer sends a document by text message, or a picture larger than 10 MB), the message says *An attachment could not be shown*, and its text is still there.

### Favorites

Keep the customers you message most one click away.

- **Add a favorite:** open their conversation and click **Add to favorites** at the top, beside **Transfer**, or on the **Customer** card. The button then reads **Favorite**; click it again to remove them.
- **See your favorites:** click the **star button** beside the filter above the conversation list, or choose **Favorites** in the filter menu, which also shows how many you have. Each favorite shows their latest conversation; one with none yet says *No conversation yet: click to write* and opens a new message. Your favorites also appear in a row above the conversation list, and the list's search narrows them too.

Your favorites are your own, up to 100 customers; other agents do not see them.

### When a message fails

If the provider does not accept a message, it is tried again after 1, 5, 15 and 60 minutes before it is marked failed. A message refused because the customer opted out is marked failed at once, and the contact is marked **Do not SMS**.

## Transfer a conversation

Transferring keeps the same conversation, so its whole history goes with it. The customer is not told.

1. Click **Transfer**.
2. Choose **A person** and search by name, or **Back to a queue** and pick the queue. **Back to a queue** is offered when Contact Center Work Distribution is on; the conversation goes back to that queue's shared inbox for any member to claim.
3. Add an optional **Note for the recipient** (up to 500 characters). The note stays in the history for other agents; the customer never sees it.
4. Click **Transfer**. The new owner gets a notification on whatever admin page they are on, and the thread shows *Transferred from* the old owner *to* the new one.

Who can transfer and to whom:

- The agent who holds the conversation can transfer it. A supervisor with *View all messaging conversations* can transfer any conversation. A conversation nobody holds must be claimed first, unless a supervisor assigns it directly.
- Only people who can use the messaging workspace are offered, never the person who already holds the conversation, and never you.
- A queue's conversation handed to one of the queue's members stays the queue's. Handed to someone outside the queue, it becomes that person's own conversation.

## Start a new conversation

<video controls preload="metadata" width="100%" aria-label="Screencast of starting a new SMS conversation and sending it">
  <source src="/img/docs/um-messaging-send.mp4" type="video/mp4" />
</video>

1. Click the **New conversation** (pencil) button.
2. Pick **From**, the number you send from (it decides the channel).
3. In **To**, search for contacts reachable on that channel; add other numbers under **Other**, separated by commas.
4. Write your message and click **Send**.

One recipient starts a conversation; several get their own private conversation each. More than one recipient needs the *Send group messages* permission and is sent as a group message in the background.

You can also click the **Send SMS** button beside any phone field on admin pages, such as a contact's phone number. It opens the customer's existing SMS conversation, or a new message when there is none.

## Templates

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a message template and sending a broadcast">
  <source src="/img/docs/um-messaging-templates-broadcasts.mp4" type="video/mp4" />
</video>

Templates are canned responses agents insert into a reply. Use them for answers you give often, such as your opening hours.

1. Open **Messaging > Templates** and click **Add template**.
2. Enter a **Name** agents will recognize and the **Body** text, then save.

Your administrator can copy templates to another site through a deployment plan.

## Broadcasts

A broadcast sends the same message to many people; each recipient gets their own one-to-one thread and cannot see the others, and replies come back to the inbox.

1. Open **Messaging > Broadcasts** and click **New broadcast**.
2. Enter a **Name**, pick **Send from** (the address decides the channel), choose **Recipients (contacts)** and add **Additional addresses** (one per line or comma-separated).
3. Type the **Message** and click **Queue broadcast**.

The list shows each broadcast's channel, sending address, status, recipients, and how many messages were sent and failed. A queued broadcast cannot be cancelled.

## Keywords customers can send

The SMS channel always answers these carrier keywords, and a keyword never triggers the entry point's auto-reply.

| Keyword | Also accepted | What happens | Reply the customer gets |
| --- | --- | --- | --- |
| **STOP** | STOPALL, UNSUBSCRIBE, CANCEL, END, QUIT | The conversation is closed, the contact is marked **Do not SMS**, and the customer gets one confirmation. | *You have been unsubscribed and will not receive further messages. Reply START to resubscribe.* |
| **START** | UNSTOP, YES | The customer is opted back in. Only works when they had opted out, so an ordinary "Yes" stays part of the conversation. | *You have been resubscribed and will receive messages again. Reply STOP to unsubscribe.* |
| **HELP** | INFO | The customer gets the help reply. | *Reply STOP to unsubscribe. Message and data rates may apply.* |

Your administrator can change the reply texts.

:::note[About the screencasts]
The inbox and broadcast screencasts fill in replies and broadcasts without sending them; the new-conversation screencast sends a real text.
:::
