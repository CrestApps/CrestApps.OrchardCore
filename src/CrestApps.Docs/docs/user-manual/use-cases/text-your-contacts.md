---
sidebar_label: Text Your Contacts
title: Text Your Contacts
description: Import your contacts, set up texting numbers, and let agents answer and start text conversations from a shared inbox, with templates and broadcasts.
technical_manual:
  - omnichannel/messaging-workspace
  - omnichannel/sms
---

## Who it's for and what you get

For **managers** setting up two-way texting with customers, and the **agents** and **supervisors** who answer.

When it is done:

- customers who text your numbers land in a shared inbox, or with one agent, depending on the number;
- agents claim conversations, reply, send pictures, transfer to a colleague or a queue, and close them;
- agents start new conversations from their own texting number, and reuse canned responses;
- supervisors send the same message to many customers as a broadcast, with each reply coming back as its own conversation;
- customers who text STOP are opted out automatically.

<video controls preload="metadata" width="100%" aria-label="Screencast of starting a new SMS conversation and sending it">
  <source src="/img/docs/um-messaging-send.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| An SMS provider account (Twilio or Telnyx) with numbers that can text | Your administrator, with IT |
| The features **SMS Messaging Channel** (it turns on the messaging workspace), **Contact Center Inbound Entry Points**, **Omnichannel Management** and **Content Transfer** (to import contacts). **Omnichannel Messaging Routed Distribution** if each conversation should go to one available agent. | Your administrator, in **Tools > Features** |
| A manager role with Manage omnichannel addresses and Manage Contact Center queues | Your administrator |
| **Use the messaging workspace** for agents (the Agent role has it); **Send group messages** and **Manage the messaging workspace** for whoever sends broadcasts and writes templates (the Supervisor role has them) | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Connect the SMS provider.** The administrator picks the default SMS provider and fills in its settings under **Settings > Communication > SMS**. See [Phone and SMS setup](../telephony-settings.md).
2. **Import your contacts.** Set up the contact type, then import your list under **Content > Import**. Contacts who should not get texts can be marked **Do not SMS**. See [Contacts](../contacts.md).
3. **Add your texting numbers.** Add each number under **Interaction Center > Management > Omnichannel Addresses** with **Text messages (SMS)** ticked, pick its provider, and pick the **Agents who text from this number**. See [Omnichannel addresses](../channel-endpoints.md).
4. **Set the default number.** The administrator picks the **Default SMS number** used by agents who are on no number's list, under **Settings > Contact Center**. See [Contact Center settings](../contact-center-settings.md).
5. **Create a queue for shared inboxes (optional).** To share a number's texts across a team, create a queue for that team, such as *Support*. You send the number's texts to it in the next step. See [Queues](../queues.md).
6. **Route incoming texts.** Add a **Text messages** entry point for each number under **Inbound entry points**: send its texts to a queue or to one agent, choose shared-pool or routed distribution, and write the auto-reply and the closed auto-reply. See [Inbound entry points and IVR menus](../entry-points-and-ivr.md).
7. **Write templates.** Add canned responses agents insert into replies under **Messaging > Templates**. See [Messaging workspace](../messaging.md).
8. **Train agents on the inbox.** Agents filter, claim, reply, transfer, close and reopen conversations, and start new ones, under **Messaging > Inbox**. See [Messaging workspace](../messaging.md).
9. **Send broadcasts.** Supervisors send one message to many contacts under **Messaging > Broadcasts**. See [Messaging workspace](../messaging.md).

## Check that it works

1. Text one of your numbers from a mobile phone. The conversation appears in **Messaging > Inbox**: under **Unassigned** for a shared queue, or in the agent's own conversations for a personal number. If the entry point has an auto-reply, your phone receives it.
2. Claim the conversation, reply, and check that your phone receives the reply.
3. Start a **New conversation** to your phone from an agent's number.
4. Text **STOP** from your phone and check that you receive the confirmation. Text **START** to opt back in.

## Tips

- A number with no enabled text entry point still receives texts. They land in the unassigned inbox.
- A banner warns agents during the customer's **quiet hours**, outside the queue's business hours. It does not stop them sending.
- With **Routed** distribution, agents turn on the **Available** switch in the inbox to receive conversations.
- A broadcast cannot be cancelled once it is queued. Send a test to yourself first.
- To have the AI answer texts first and hand over to a person, see [Let the AI answer your customers](ai-answers-customers.md). To nudge customers who stop replying, see [Follow up automatically](automatic-follow-ups.md).
- Use a workflow to text a customer automatically after a call, for example when nobody answered. See [Contact Center workflows](../workflows.md).
