---
sidebar_label: Cadences
sidebar_position: 16
title: Cadences - Automatic Follow-ups
description: Build a series of follow-up messages that nudge a customer who stops replying to an automated SMS conversation.
---

A **cadence** is a reusable series of follow-up messages for automated SMS conversations. When a customer stops replying, the cadence sends the next step after the silence you chose, for example a nudge after an hour and another after a day. Each step can be a fixed message or written by the AI.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Cadences |
| **Permission** | Manage cadences |
| **Feature** | Omnichannel Management; follow-ups are sent by SMS Omnichannel Automation |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a cadence with a defined message step and an AI-generated step">
  <source src="/img/docs/um-cadences.mp4" type="video/mp4" />
</video>

## Create a cadence

1. Open **Interaction Center > Management > Cadences** and click **Add cadence**.
2. Enter a **Name** and optional **Description**, and leave **Enabled** ticked.
3. Click **Add step** for each follow-up:
   - **After (minutes)**: how long the customer must be silent, counted from the last message the automation sent.
   - **Message source**: **Defined message** (your exact text) or **AI-generated** (the AI writes it from the conversation).
   - **Message text**, or **AI guidance (optional)** for an AI step.
4. Click **Save**.

The number of steps is the most follow-ups a customer will ever get. After the last step, the conversation ends on its normal no-response timeout.

## Use a cadence

On an **Automatic** [activity load](automated-ai.md#ai-options-on-an-automatic-load), pick the cadence under **Re-engagement**. With *No follow-up cadence*, nobody is followed up. Pick a **Business hours** calendar on the same load to send follow-ups only while you are open, in the customer's time zone.

The cadence is copied onto each activity when the load runs, so later edits do not change conversations already in progress. A customer who replies, or texts STOP, is never nudged.
