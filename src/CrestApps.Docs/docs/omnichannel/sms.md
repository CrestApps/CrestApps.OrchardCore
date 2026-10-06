---
sidebar_label: SMS Automation
sidebar_position: 3
title: CrestApps SMS Omnichannel Automation (AI)
description: AI-driven SMS automation for Omnichannel activities in Orchard Core.
user_manual:
  - user-manual/automated-ai
  - user-manual/subjects
  - user-manual/load-inventory
  - user-manual/cadences
  - user-manual/entry-points-and-ivr
---

| | |
| --- | --- |
| **Feature Name** | SMS Omnichannel Automation |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Sms` |

Provides a way handle automated activities using the SMS channel.

## Overview

The `CrestApps.OrchardCore.Omnichannel.Sms` module enables **AI-driven SMS automation** for Omnichannel activities.

It allows Orchard Core (through the Omnichannel Management Customer Relationship Management (CRM) experience) to assign an activity to an AI agent that communicates with a contact over SMS as if it were a real call center agent.

You describe what you want the AI to do (tone, rules, goals), and the AI carries the conversation through SMS until the activity is completed.

## What this module provides

- An SMS channel processor and inbound event handler for Omnichannel automated activities. Messages are sent through the Orchard Core SMS provider (for example [Telnyx SMS](../telephony/telnyx.md#telnyx-sms) or Twilio).
- The Twilio inbound SMS webhook, `POST ~/api/twilio/webhook/sms`, when Orchard Core's Twilio SMS feature is on. The [SMS Messaging Channel](messaging-workspace.md#setting-up-sms) maps the same webhook, so it works with either one on, and is mapped once with both. Telnyx SMS maps its own webhook (`api/telnyx/webhook/sms`).
- AI chat session orchestration for "automated activities".
- Re-engagement follow-ups through [Cadences](cadences.md), gated by business-hours calendars.
- A background task that recovers owed replies: when a contact's latest message (from the last 30 minutes) never got an answer, for example because the site restarted mid-reply, the reply is generated and sent.
- With the [SMS Messaging Channel](messaging-workspace.md#setting-up-sms) also on, an **AI agent** routing choice on text entry points, so an AI profile answers the texts customers send to a number (`EntryPointAIAgentStartup`). See [Let the AI answer incoming texts](../user-manual/automated-ai.md#let-the-ai-answer-incoming-texts).

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `SMS Omnichannel Automation`. It depends on the AI features, Omnichannel Management, Orchard Core SMS, and **Contact Center Business Hours**, which it enables so that background follow-ups can respect business-hours calendars.

For the step-by-step setup with screencasts, see [Automated AI SMS and voice](../user-manual/automated-ai.md) in the User Manual.

### Twilio webhook

The Twilio settings editor (**Settings > Communication > SMS**, Twilio tab) shows the address to give Twilio as a read-only **Webhook URL** field. In the Twilio console, set the phone number's *A message comes in* to *Webhook*, *HTTP POST*, and that address. The same field ships with the SMS Messaging Channel, so it shows when either feature is on.

### Twilio webhook signature

The Twilio webhook verifies Twilio's `X-Twilio-Signature` header with the auth token from the Twilio SMS settings; a request without a valid signature is refused, and a missing auth token answers `400 Bad Request`. Twilio signs the public URL it was configured with, so when the site runs behind a TLS-terminating proxy or load balancer, set the site's **Base URL** (**Settings > General**) to the public address. The signature is then checked against that URL instead of the internal hop's scheme and host.

## Typical setup (high level)

Configure [Omnichannel Management](management.md) (contacts, subjects, dispositions, campaigns and subject flows), create a **Chat** AI profile with **Start the conversation automatically** enabled (the **Qualify leads by text** and **Customer care by text** [starting points](../ai/profile-templates.md#text-messaging-and-phone-call-starting-points) ship with this module), connect the SMS provider's inbound webhook (above), and load activities with the **Automatic** source. The `AutomatedActivitiesProcessorBackgroundTask` (every five minutes) sends each due activity's opening message, and the AI handles the replies. The browser steps, with screencasts, are in [Automated AI SMS and voice](../user-manual/automated-ai.md#load-automated-sms-activities).

## Automated SMS behavior

Automated SMS subject flows use AI profiles as the source of the AI behavior. Only chat profiles with **Start the conversation automatically** enabled can be selected, because that opening message is rendered and sent through the configured SMS endpoint before the contact can reply.

Inbound SMS replies are added to the same AI chat session, the selected profile generates the next response, and the response is sent back through the SMS service. If the contact sends an opt-out keyword such as `STOP`, the activity is cancelled and the contact's **Do not SMS** preference is updated even when **Allow AI to update contact** is disabled.

Use the subject-flow SMS automation settings to control:

- **No-response timeout**: fails an automated SMS activity when the contact stops responding.
- **Response delay**: the minimum wait before each AI SMS reply; replies are paced naturally on top of it. A reply delay chosen on the activity load is saved on each activity when it is loaded and takes precedence; the subject flow's value applies only when the load set none.
- **Opt-out keywords**: customizes the keywords that stop the SMS conversation and update the contact preference.

## Handing off to a live agent

The subject's **Live agent handoff** card (see [Subject Flow](management.md#subject-flow)) lets the AI escalate an automated SMS conversation to a person: pick the **Handoff queue** and at least one **Escalate when** condition. The SMS handoff is carried out by the [Messaging Workspace](messaging-workspace.md), so enable the **SMS Messaging Channel** feature: the thread, with its whole automated transcript, moves into the queue's inbox for an agent to pick up. Without the workspace there is nowhere to hand an SMS conversation to.
