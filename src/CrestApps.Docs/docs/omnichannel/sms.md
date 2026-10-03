---
sidebar_label: SMS Automation
sidebar_position: 3
title: CrestApps SMS Omnichannel Automation (AI)
description: AI-driven SMS automation for Omnichannel activities in Orchard Core.
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

- An SMS channel processor and inbound event handler for Omnichannel automated activities. Messages are sent through the Orchard Core SMS provider (for example [Telnyx SMS](../telephony/telnyx#telnyx-sms) or Twilio).
- The Twilio inbound SMS webhook, `POST ~/api/twilio/webhook/sms`, when Orchard Core's Twilio SMS feature is on. The [SMS Messaging Channel](messaging-workspace#setting-up-sms) maps the same webhook, so it works with either one on, and is mapped once with both. Telnyx SMS maps its own webhook (`api/telnyx/webhook/sms`).
- AI chat session orchestration for "automated activities".
- Re-engagement follow-ups through [Cadences](cadences), gated by business-hours calendars.
- A background task that recovers owed replies: when a contact's latest message (from the last 30 minutes) never got an answer, for example because the site restarted mid-reply, the reply is generated and sent.

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `SMS Omnichannel Automation`. It depends on the AI features, Omnichannel Management, Orchard Core SMS, and **Contact Center Business Hours**, which it enables so that background follow-ups can respect business-hours calendars.

For the step-by-step setup with screencasts, see [Automated AI](../user-manual/automated-ai.md) in the user manual.

### Twilio webhook signature

The Twilio webhook verifies Twilio's `X-Twilio-Signature` header with the auth token from the Twilio SMS settings; a request without a valid signature is refused, and a missing auth token answers `400 Bad Request`. Twilio signs the public URL it was configured with, so when the site runs behind a TLS-terminating proxy or load balancer, set the site's **Base URL** (**Settings > General**) to the public address. The signature is then checked against that URL instead of the internal hop's scheme and host.

## Typical setup (high level)

1. Configure Omnichannel Management (contacts, subjects, dispositions, campaigns, and subject flows).
2. Create a subject flow that uses the **SMS** channel and **Automated** interaction type.
3. Create a chat AI profile with **Start the conversation automatically** enabled. The profile's opening message is sent as the first outbound SMS message that starts the conversation. The **Qualify leads by text** and **Customer care by text** starting points under **Artificial Intelligence > Profiles > Add Profile** create such a profile with the opening message already filled in. See [Text messaging and phone call starting points](../ai/profile-templates.md#text-messaging-and-phone-call-starting-points).
4. If the AI feature is enabled, select that initial-prompt chat profile on the subject flow, then configure the subject goal, update permissions, no-response timeout, response delay, and opt-out keywords.
5. Configure your SMS provider webhook to deliver inbound SMS messages to Orchard Core.
6. Load activities via **Load Activities** using the **Automatic** source.
7. The Automated Activities Processor will run in the background and let AI handle the assigned SMS interactions.

## Automated SMS behavior

Automated SMS subject flows use AI profiles as the source of the AI behavior. Only chat profiles with **Start the conversation automatically** enabled can be selected, because that opening message is rendered and sent through the configured SMS endpoint before the contact can reply.

Inbound SMS replies are added to the same AI chat session, the selected profile generates the next response, and the response is sent back through the SMS service. If the contact sends an opt-out keyword such as `STOP`, the activity is cancelled and the contact's **Do not SMS** preference is updated even when **Allow AI to update contact** is disabled.

Use the subject-flow SMS automation settings to control:

- **No-response timeout**: fails an automated SMS activity when the contact stops responding.
- **Response delay**: the minimum wait before each AI SMS reply; replies are paced naturally on top of it. A reply delay chosen on the activity load is saved on each activity when it is loaded and takes precedence; the subject flow's value applies only when the load set none.
- **Opt-out keywords**: customizes the keywords that stop the SMS conversation and update the contact preference.

## Handing off to a live agent

The subject's **Live agent handoff** card (see [Subject Flow](management#subject-flow)) lets the AI escalate an automated SMS conversation to a person: pick the **Handoff queue** and at least one **Escalate when** condition. The SMS handoff is carried out by the [Messaging Workspace](messaging-workspace), so enable the **SMS Messaging Channel** feature: the thread, with its whole automated transcript, moves into the queue's inbox for an agent to pick up. Without the workspace there is nowhere to hand an SMS conversation to.
