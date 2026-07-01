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

- An SMS channel implementation for Omnichannel automated activities.
- Integration points for an SMS provider (e.g. Twilio) to send/receive messages.
- AI chat session orchestration for "automated activities".

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `SMS Omnichannel Automation`.

## Typical setup (high level)

1. Configure Omnichannel Management (contacts, subjects, dispositions, campaigns, and subject flows).
2. Create a subject flow that uses the **SMS** channel and **Automated** interaction type.
3. Create a chat AI profile with **Add initial prompt** enabled. The profile's initial prompt is sent as the first outbound SMS message that starts the conversation.
4. If the AI feature is enabled, select that initial-prompt chat profile on the subject flow, then configure the subject goal, update permissions, no-response timeout, response delay, and opt-out keywords.
5. Configure your SMS provider webhook to deliver inbound SMS messages to Orchard Core.
6. Load activities via Activity Batches using the **Automatic** source.
7. The Automated Activities Processor will run in the background and let AI handle the assigned SMS interactions.

## Automated SMS behavior

Automated SMS subject flows use AI profiles as the source of the AI behavior. Only chat profiles with **Add initial prompt** enabled can be selected, because that initial prompt is rendered and sent through the configured SMS endpoint before the contact can reply.

Inbound SMS replies are added to the same AI chat session, the selected profile generates the next response, and the response is sent back through the SMS service. If the contact sends an opt-out keyword such as `STOP`, the activity is cancelled and the contact's **Do not SMS** preference is updated even when **Allow AI to update contact** is disabled.

Use the subject-flow SMS automation settings to control:

- **No-response timeout**: fails an automated SMS activity when the contact stops responding.
- **Response delay**: waits before sending each AI SMS reply.
- **Opt-out keywords**: customizes the keywords that stop the SMS conversation and update the contact preference.
