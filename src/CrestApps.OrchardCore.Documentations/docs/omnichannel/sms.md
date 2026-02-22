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

It allows Orchard Core (through the Omnichannel Management CRM) to assign an activity to an AI agent that communicates with a contact over SMS as if it were a real call center agent.

You describe what you want the AI to do (tone, rules, goals), and the AI carries the conversation through SMS until the activity is completed.

## What this module provides

- An SMS channel implementation for Omnichannel automated activities.
- Integration points for an SMS provider (e.g. Twilio) to send/receive messages.
- AI chat session orchestration for "automated activities".

## Enable the feature

1. In Orchard Core Admin, go to `Configuration` → `Features`.
2. Enable `SMS Omnichannel Automation`.

## Typical setup (high level)

1. Configure Omnichannel Management (contacts, subjects, dispositions, campaign).
2. Ensure the campaign/channel is configured to use SMS.
3. Configure your SMS provider webhook to deliver inbound SMS messages to Orchard Core.
4. Load activities via Activity Batches.
5. The Automated Activities Processor will run in the background and let AI handle the assigned SMS interactions.

