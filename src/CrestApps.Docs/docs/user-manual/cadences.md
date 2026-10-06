---
sidebar_label: Cadences
sidebar_position: 16
title: Cadences - Automatic Follow-ups
description: Build a series of follow-up messages that nudge a customer who stops replying to an automated SMS conversation.
technical_manual:
  - omnichannel/cadences
---

A **cadence** is a reusable series of follow-up messages for automated SMS conversations. When a customer stops replying, the cadence sends the next step after the silence you chose, for example a nudge after an hour and another after a day. Each step can be a fixed message or written by the AI. You build a cadence once and pick it on as many automatic activity loads as you like.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Cadences |
| **Permission** | Manage cadences |
| **Feature** | Omnichannel Management; follow-ups are sent by SMS Omnichannel Automation |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a cadence with a defined message step and an AI-generated step">
  <source src="/img/docs/um-cadences.mp4" type="video/mp4" />
</video>

## Create a cadence

1. Open **Interaction Center > Management > Cadences** and click **Add cadence**.
2. Enter a **Name** and optional **Description**, and leave **Enabled** ticked. A disabled cadence never sends follow-ups.
3. Under **Follow-up steps**, click **Add step** for each follow-up:
   - **After (minutes)**: how long the customer must be silent, counted from the last message the automation sent.
   - **Message source**: **Defined message** (your exact text) or **AI-generated** (the AI writes it from the conversation).
   - **Message text**, or **AI guidance (optional)** for an AI step. Leave the guidance empty to let the AI decide what to write.
4. Click **Save**.

The clock restarts every time the automation sends a message, so the delays add up. For example, a step of 60 followed by a step of 1440 sends the first follow-up after an hour of silence and the next one after a further day.

The number of steps is the most follow-ups a customer will ever get. After the last step, the conversation ends on its normal no-response timeout. Each follow-up restarts that timeout, so the customer has the full time to answer it.

Unlike campaigns, cadences can be deleted. Deleting or editing a cadence does not change conversations already in progress.

## Use a cadence

On an **Automatic** [activity load](automated-ai.md#ai-options-on-an-automatic-load), pick the cadence under **Re-engagement**. With *No follow-up cadence*, nobody is followed up.

Pick a **Business hours** calendar on the same load to send follow-ups only while you are open, in the customer's time zone. The field shows only when at least one [business hours calendar](business-hours.md) exists. Without a calendar, follow-ups are sent at any hour.

The cadence is copied onto each activity when the load runs, so later edits do not change conversations already in progress.

## When a follow-up is sent

A follow-up goes out only when all of these are true:

- the load has an enabled cadence, and the conversation has not used all of its steps;
- the last message in the conversation was the automation's;
- the customer has been silent longer than the step's **After (minutes)**;
- the business hours calendar, if any, is open in the customer's time zone.

A customer who replies, or texts STOP, is never nudged. Business hours only hold back follow-ups: a reply to a customer who is texting right now is always sent.

Your administrator can copy cadences to another site through a deployment plan.
