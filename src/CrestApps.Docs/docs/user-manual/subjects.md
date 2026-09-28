---
sidebar_label: Subjects
sidebar_position: 11
title: Inbound and Outbound Subjects
description: Create a subject for each kind of conversation, choose whether it is inbound or outbound, manual or automated, and configure its AI and live-agent handoff settings.
---

A **subject** is what a call or message is about: *Lead generation*, *Support request*, *Welcome call*. It is a content type with the **Omnichannel Subject** part, and its fields are what the agent fills in during the interaction. Each subject has a direction:

- **Outbound** subjects are for work you start: calling or texting a contact. The channel is chosen when you [load inventory](load-inventory.md).
- **Inbound** subjects are for work the contact starts: they called or texted you. An inbound subject can be **manual** (an agent logs it) or **automated** (an AI answers on a channel endpoint).

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Subject Flows |
| **Permission** | Manage subject flows; Edit content types (to change settings) |
| **Feature** | Omnichannel Management |

Create an **outbound** subject for calls you make. This one uses a default campaign and adds a *Vehicle of interest* field:

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an outbound subject, attaching the Omnichannel Subject part, choosing its default campaign and adding a field">
  <source src="/img/docs/um-subject-outbound.mp4" type="video/mp4" />
</video>

Create an **inbound** subject for calls customers make. This one is a manual phone subject with a *Question* field:

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an inbound manual phone subject and adding a field">
  <source src="/img/docs/um-subject-inbound.mp4" type="video/mp4" />
</video>

## Create a subject

1. Open **Interaction Center > Management > Subject Flows** and click **Add Subject**.
2. Enter the subject's **Display Name**, for example *Support Request*, and click **Create**.
3. On the **Add Parts** page, click **Add & Configure** next to **Omnichannel Subject**. The subject's settings open straight away (described below). Save them.
4. Add the fields the agent should capture, for example a *Vehicle of interest* text field, from **Edit Content Type**.

Later, click **Edit Settings** next to the subject on the Subject Flows list to change its settings.

## Subject settings

| Setting | Applies to | What it does |
| --- | --- | --- |
| **Direction** | all | **Outbound** (the default) or **Inbound**. |
| **Interaction type** | Inbound | **Manual** (an agent logs it) or **Automated** (an AI profile handles it). |
| **Channel** | Inbound | **Phone** or **SMS**. |
| **Channel endpoint** | Inbound automated | The number the AI answers on. |
| **Default campaign** | all | The campaign used when an activity is created outside an inventory load, and the fallback for loads. |
| **Require a disposition** | all | The agent must pick an outcome to complete the activity. Leave it on unless the subject has no outcome to record. |

When the AI features are enabled, more settings appear:

| Setting | Applies to | What it does |
| --- | --- | --- |
| **AI profile** | Inbound automated | The chat profile that runs the conversation. Only profiles with an initial prompt are listed. Automatic inventory loads can pick a different profile. |
| **Subject goal** | Inbound automated | What the AI is trying to achieve, in plain words. |
| **Speech-to-text**, **Text-to-speech**, **Voice** | Inbound automated phone | The speech models and voice. Empty uses the site's AI defaults. |
| **Allow AI to update contact** / **subject** | Inbound automated | Lets the AI write what it learns back to the records. |
| **No-response timeout (minutes)**, **Response delay (seconds)**, **Opt-out keywords** | Inbound automated SMS | When to give up on a silent customer, the minimum pause before replying, and the words that opt the customer out. |
| **Allow the AI to hand off to a live agent**, **Handoff queue**, **Escalate when** | Inbound automated, and every outbound subject | Lets the AI pass the conversation to a person in the chosen queue when the customer asks for a human, is a qualified lead, or is frustrated. Handing off an SMS conversation needs the [Messaging workspace](messaging.md). |

Only the settings that apply to the chosen direction, type and channel are shown.

The Subject Flows list shows a badge for each subject's direction, an **Automated** badge with the channel, and **Missing flow** until you add at least one action. Next, [configure the subject flow](subject-flows.md).
