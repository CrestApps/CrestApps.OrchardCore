---
sidebar_label: Automated AI SMS & Voice
sidebar_position: 18
title: Automated AI SMS and Voice Campaigns
description: Set up an AI profile and an automatic inventory load so the AI texts or calls your contacts, follows up, picks the disposition, and hands off to a person when needed.
---

An **automatic** inventory load creates activities that an **AI profile** works by itself: it sends the opening text (or places the call), holds the conversation, and completes the activity with a disposition and a summary. If the subject allows it, the AI hands the customer to a live agent.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Load Inventory > Add Inventory Load > Automatic |
| **Permission** | Manage activity batches |
| **Features** | **SMS Omnichannel Automation** (`CrestApps.OrchardCore.Omnichannel.Sms`) for SMS; **Telnyx AI Voice Agent** (`CrestApps.OrchardCore.Telnyx.AiVoice`) for voice calls; the AI features and a configured AI provider |

## Before you start

1. **An AI profile.** Under **Artificial Intelligence > AI Profiles**, create a profile of type **Chat** with **Add initial prompt** turned on. The initial prompt is the opening message. It can use Liquid, for example `{{ Contact.DisplayText }}`.
2. **A subject and its flow**, with the dispositions the AI may choose and a **When to choose this disposition** hint on each action. See [Subject flows](subject-flows.md).
3. **A channel endpoint** for the number you send from or call from. See [Channel endpoints](channel-endpoints.md).
4. **Optionally a cadence** for follow-ups ([Cadences](cadences.md)) and a **business hours** calendar ([Business hours](business-hours.md)).

## Load automated SMS activities

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an automatic SMS inventory load driven by an AI profile and loading it to generate automated activities">
  <source src="/img/docs/um-load-ai-sms.mp4" type="video/mp4" />
</video>

1. Open **Interaction Center > Management > Load Inventory**, click **Add Inventory Load** and choose **Automatic**.
2. Enter a **Title**, pick the **Subject content type** and the **Campaign**.
3. Pick the **AI profile** (or leave it to use the subject's profile).
4. Pick **SMS** as the **Channel** and the SMS **Channel endpoint**.
5. Set the AI options (below), the contact filters, and **Save**.
6. Choose **Actions > Load batch**.

A background task picks up due automated activities every five minutes and sends the opening message. Replies are answered by the AI after the reply delay. Each send checks the contact's opt-out first.

## Load automated voice calls

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an automatic voice inventory load with an AI profile, background office sound and reply delay">
  <source src="/img/docs/um-load-ai-voice.mp4" type="video/mp4" />
</video>

Follow the same steps with **Phone** as the channel and a phone endpoint. The AI places each call, talks with the person who answers, and hangs up when the conversation is done. Before every call the contact is checked against opt-outs, national do-not-call registries, and the calling window, which needs the **Contact Center Outbound Dialer** feature.

## AI options on an automatic load

| Field | What it does |
| --- | --- |
| **AI profile** | The profile that runs the conversation. Only chat profiles with an initial prompt are listed. |
| **Allow AI to update the subject** / **contact** | Lets the AI save what it learns onto the records. |
| **Play background office sound on calls** | Voice only: a quiet office ambience behind the AI voice. |
| **Reply delay** | **No delay**, **Fixed** (a number of seconds), or **Random** (a base plus or minus a jitter), so replies feel human. |
| **Re-engagement** | The [cadence](cadences.md) that follows up when the customer goes quiet, or *No follow-up cadence*. |
| **Business hours** | Follow-ups are only sent while this calendar is open, in the customer's time zone. |

## When the conversation ends

The AI picks a disposition using your hints, the summary is saved as the activity's notes, and the subject flow runs exactly as if an agent had completed it. Open **Review AI conversation** on the activity to read the full transcript.

If the subject allows **live agent handoff**, the AI can pass the customer to the handoff queue when they ask for a person, look like a qualified lead, or are frustrated. SMS conversations appear in the [Messaging workspace](messaging.md); calls ring an agent in the queue.
