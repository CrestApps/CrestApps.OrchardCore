---
sidebar_label: Subjects
sidebar_position: 11
title: Inbound and Outbound Subjects
description: Create a subject for each kind of conversation, choose whether it is inbound or outbound, manual or automated, and configure its AI and live-agent handoff settings.
technical_manual:
  - omnichannel/management
  - omnichannel/sms
  - telephony/telnyx
---

A **subject** is what a call or message is about: *Lead generation*, *Support request*, *Welcome call*. It is a content type with the **Omnichannel Subject** part, and its fields are what the agent fills in during the interaction. Each subject has a direction:

- **Outbound** subjects are for work you start: calling or texting a contact. The channel is chosen when you [load activities](load-inventory.md).
- **Inbound** subjects are for work the contact starts: they called or texted you. An inbound subject can be **manual** (an agent logs it) or **automated** (an AI answers on one of your [omnichannel addresses](channel-endpoints.md)).

Create one subject for each goal of a conversation. You can add more for later stages of the customer journey, for example *Lead Generation - 30 day follow-up* and *New Customer - Welcome*.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Subject Flows |
| **Permission** | Manage subject flows; Edit content types (to create subjects and change their settings) |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

Create an **outbound** subject for calls you make. This one uses a default campaign and adds a *Vehicle of interest* field:

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an outbound subject, attaching the Omnichannel Subject part, choosing its default campaign and adding a field">
  <source src="/img/docs/um-subject-outbound.mp4" type="video/mp4" />
</video>

Create an **inbound** subject for calls customers make. This one is a manual phone subject with a *Question* field:

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an inbound manual phone subject and adding a field">
  <source src="/img/docs/um-subject-inbound.mp4" type="video/mp4" />
</video>

## Create a subject

1. Open **Interaction Center > Management > Subject Flows** and click **Add Subject**. (When there are no subjects yet, the page shows a **Create a new content type** link instead.)
2. Enter the subject's **Display Name**, for example *Support Request*, and click **Create**.
3. On the **Add Parts** page, click **Add & Configure** next to **Omnichannel Subject**. The subject's settings open straight away (described below). Save them.
4. Add the fields the agent should capture, for example a *Vehicle of interest* text field, from **Edit Content Type**.

Later, use the buttons next to the subject on the Subject Flows list:

- **Edit Settings** opens the subject's settings directly.
- **Edit Content Type** opens the whole content type, to add or change fields. Both buttons are shown only when you have the **Edit content types** permission.
- **Manage Flow** opens the [subject flow](subject-flows.md).

Agents fill in a subject's fields while they work an activity, so the usual **Publish**, **Save Draft** and **Preview** buttons are hidden on a subject's editor.

## Subject settings

| Setting | Applies to | What it does |
| --- | --- | --- |
| **Default campaign** | all | The campaign used when an activity is created outside an activity load (by hand, for an inbound call, or by moving activities to this subject), and the campaign activity loads start with. |
| **Direction** | all | **Outbound** (the default) or **Inbound**. |
| **Interaction type** | Inbound | **Manual** (an agent logs it) or **Automated** (an AI profile handles it). |
| **Channel** | Inbound | **Phone** or **SMS**. |
| **Address** | Inbound automated | The number the AI answers on and replies from. |
| **Require a disposition** | all | The agent must pick an outcome to complete the activity. Leave it on unless the subject is a one-way notice, such as an SMS alert, with no outcome to record: the disposition is what runs the subject flow. |

For outbound subjects, the interaction type, channel and address are hidden, because they are chosen when activities are loaded.

When the AI features are enabled, more settings appear:

| Setting | Applies to | What it does |
| --- | --- | --- |
| **AI profile** | Inbound automated | The chat profile that runs the conversation. Only profiles with **Start the conversation automatically** turned on are listed. Automatic activity loads can pick a different profile. |
| **Subject goal** | Inbound automated | What the AI is trying to achieve, in plain words. The AI uses it to decide when to end the conversation and pick a disposition. |
| **Speech-to-text deployment**, **Text-to-speech deployment**, **Voice** | Inbound automated phone | The speech models and voice. **Use site default** uses the defaults from the Artificial Intelligence settings. Save after changing the text-to-speech deployment to refresh its list of voices. |
| **Allow AI to update contact** / **Allow AI to update subject** | Inbound automated | Lets the AI write what it learns back to the records. An opt-out text always updates the contact's **Do not SMS**, even when this is off. |
| **No-response timeout (minutes)**, **Response delay (seconds)**, **Opt-out keywords** | Inbound automated SMS | When to give up on a silent customer, the minimum pause before each reply, and the words that opt the customer out (STOP, STOPALL, UNSUBSCRIBE, CANCEL, END and QUIT by default; one per line or separated by commas). |
| **Allow the AI to hand off to a live agent**, **Handoff queue**, **Escalate when** | Inbound automated, and every outbound subject | Lets the AI pass the conversation to a person in the chosen queue when **The customer asks for a human**, **The customer is a qualified, ready lead**, or **The customer is frustrated or the AI cannot help**. Handoff only happens when a queue is picked and at least one condition is ticked. Handing off an SMS conversation needs the [Messaging workspace](messaging.md). |

Only the settings that apply to the chosen direction, type and channel are shown. The AI settings change as soon as you change the direction, interaction type or channel. The **Live agent handoff** card follows the saved settings, so save after changing the direction or interaction type to show or hide it. Hidden settings keep their values, so switching back and forth loses nothing.

The Subject Flows list shows a badge for each subject's direction, an **Automated** badge with the channel, and **Missing flow** until you add at least one action. Next, [configure the subject flow](subject-flows.md).
