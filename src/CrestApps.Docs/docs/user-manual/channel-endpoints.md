---
sidebar_label: Omnichannel Addresses
sidebar_position: 15
title: Omnichannel Addresses
description: List the phone numbers you own, tick what each is used for (calls, texts or both), choose the SMS provider, and set how inbound calls and texts are routed.
---

An **omnichannel address** is an address the business owns, such as a phone number. Each address is listed once, with a checkbox for each thing it is used for: **Voice calls**, **Text messages (SMS)**, or both. Addresses are what agents dial out from, what automatic SMS loads and the Messaging workspace send from, what automated inbound subjects answer on, and where incoming calls and texts are matched.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Omnichannel Addresses |
| **Permission** | Manage omnichannel addresses |
| **Feature** | Omnichannel Management. **Voice calls** comes with Contact Center Inbound Voice or Contact Center Outbound Lines; **Text messages (SMS)** comes with SMS Messaging Channel. |

## Add an address

1. Open **Interaction Center > Management > Omnichannel Addresses** and click **Add Address**.
2. Pick the kind of address, such as **Phone number**.
3. Enter a **Name** people will recognize and the **Phone number** in international format, such as `+17025550100`.
4. Under **Used for**, tick **Voice calls**, **Text messages (SMS)**, or both. The settings for each appear while it is ticked:
   - **Text messages**: the SMS **Provider** that owns the number, and the inbound routing for texts.
   - **Voice calls**: the entry point that answers calls, and the agents who dial out from the number.
5. Click **Save**.

A number is listed once. To use a number for calls and texts, tick both on the same address rather than adding it twice. The kind of address cannot be changed after it is created, and addresses cannot be deleted.

The address list shows each address's kind, what it is used for, its number and its SMS provider as badges under its name.

Numbers that were listed once per channel before addresses had capabilities were merged into one address when the site was upgraded, with the settings of both.

## Inbound routing for calls

When Contact Center Inbound Voice is enabled, addresses used for **Voice calls** show this setting:

| Field | What it does |
| --- | --- |
| **Entry point** | The [entry point](entry-points-and-ivr.md) that answers calls to this number. The entry point decides the queue or agent, the opening hours, the phone menu and voicemail. |

Leave it on **None** to route the number by the entry point that lists it under **Dialed numbers**, or else by the queue mapped to this number. When an entry point is chosen here, it wins over an entry point that lists the number. The section names the entry points that also list the number, so you can tidy them up.

## Inbound routing for texts

When the Messaging workspace is enabled, addresses used for **Text messages** show these settings:

| Field | What it does |
| --- | --- |
| **Routes to** | **Agent** (a personal number: texts go to one person) or **Queue** (a department number). |
| **Agent** | The person who owns the personal number. |
| **Queue (department)** | The queue whose shared inbox receives the texts. |
| **Queue distribution** | **Shared pool (claim to own)**: any agent in the queue can claim a conversation. **Routed (assign via routing strategy)**: each new conversation is assigned to an available agent. Routed needs the *Omnichannel Messaging Routed Distribution* feature. |
| **Auto-reply** | A message sent back automatically, at most once a day per customer. |

See [Messaging workspace](messaging.md).
