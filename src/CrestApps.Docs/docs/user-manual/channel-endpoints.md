---
sidebar_label: Omnichannel Addresses
sidebar_position: 15
title: Omnichannel Addresses
description: List the phone numbers you own, tick what each is used for (calls, texts or both), choose the SMS provider, and pick the agents who call and text from each number.
technical_manual:
  - omnichannel/management
  - omnichannel/messaging-workspace
  - contact-center/voice-routing
---

An **omnichannel address** is an address the business owns, such as a phone number. Each address is listed once, with a checkbox for each thing it is used for: **Voice calls**, **Text messages (SMS)**, or both. Addresses are what agents dial out from, what automatic SMS loads and the Messaging workspace send from, what automated inbound subjects answer on, and where incoming calls and texts are matched.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Omnichannel Addresses |
| **Permission** | Manage omnichannel addresses |
| **Feature** | Omnichannel Channel Endpoints, which turns on with Omnichannel Management, Omnichannel Messaging Workspace or Contact Center Voice. **Voice calls** comes with Contact Center Voice, which the voice features turn on; **Text messages (SMS)** comes with SMS Messaging Channel. |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of adding a phone number used for calls and texts, with the agents who dial and text from it">
  <source src="/img/docs/um-channel-endpoints.mp4" type="video/mp4" />
</video>

## Add an address

1. Open **Interaction Center > Management > Omnichannel Addresses** and click **Add Address**.
2. Under **What kind of address?**, pick the kind, such as **Phone number**. Only kinds that something on your site can use are offered. If none is, the page asks for a feature that uses addresses, such as Contact Center Voice or SMS Messaging Channel.
3. Enter a **Name** people will recognize and the **Phone number** in international format, such as `+17025550100`.
4. Under **Used for**, tick **Voice calls**, **Text messages (SMS)**, or both. The settings for each appear while it is ticked:
   - **Text messages**: the SMS **Provider** that owns the number, and the **Agents who text from this number**. New conversations these agents start in the messaging workspace are sent from it. Each agent texts from one number. Which entry point answers its texts is set on the entry point.
   - **Voice calls**: the **Agents who dial from this number**. Each agent dials from one number. Which entry point answers its calls is set on the entry point.

   Agents on no number's list use the default numbers chosen under **Settings > Contact Center > [Default numbers](contact-center-settings.md#default-numbers)**.
5. Add an optional **Description** and click **Save**.

To add a number set up like an existing one, open the existing address's **Actions** menu and choose **Clone**. The form opens with its kind, what it is used for, its description and its SMS provider. Enter the new number and a name, pick its agents, and click **Save**. The number and the agents who dial or text from it are not copied, because each agent dials and texts from one number.

A number is listed once. To use a number for calls and texts, tick both on the same address rather than adding it twice. The kind of address cannot be changed after it is created, and addresses cannot be deleted.

The address list shows each address's kind, what it is used for, its number and its SMS provider as badges under its name.

Numbers that were listed once per channel before addresses had capabilities were merged into one address when the site was upgraded, with the settings of both.

## Inbound routing for calls

Calls to a number are routed by the [inbound entry point](entry-points-and-ivr.md) that picks it under **Numbers**. The entry point decides the queue or agent, the opening hours, the phone menu and voicemail.

## Inbound routing for texts

Texts to a number are routed by the [text entry point](entry-points-and-ivr.md#text-entry-points) that picks it under **Numbers**. The entry point decides the queue or agent, how a queue hands the conversations out, the opening hours and the auto-replies. See [Messaging workspace](messaging.md).
