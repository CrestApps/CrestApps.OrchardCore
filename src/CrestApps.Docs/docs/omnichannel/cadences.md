---
sidebar_label: "Cadences (re-engagement)"
sidebar_position: 5
title: Cadences — automated re-engagement follow-ups
description: Reusable, business-hours-aware follow-up cadences that re-engage automated conversation contacts who go quiet.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Management |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Managements` |

A **cadence** is a reusable, named series of follow-up messages that re-engages a contact who has gone quiet in an **automated** conversation. You define a cadence once, then select it on any automatic loading campaign. A campaign with no cadence selected never sends follow-ups, so re-engagement is off by default.

Cadences are administered from **Interaction Center → Management → Cadences**, alongside campaigns and dispositions, and follow the same create / edit / delete experience.

## What a cadence contains

A cadence has a **name**, an optional **description**, an **enabled** flag, and an ordered list of **steps**. You can add as many steps as you like — each step is one follow-up message.

Each step has:

- **After (minutes)** — how long the contact must be silent, measured from the automation's *previous* message, before this step's follow-up is sent. Because the clock restarts after every message the automation sends (the opening and each follow-up), successive steps naturally space follow-ups further apart. For example, `60` then `1440` sends the first follow-up after an hour of silence, then the next after a further day.
- **Message** — either:
  - **Defined message** — you provide the exact text, which is sent to the contact verbatim; or
  - **AI-generated** — the AI composes the message from the conversation. You may add optional guidance that steers what it writes; leave it blank to let the AI decide entirely.

The number of steps is the cap on how many follow-ups are ever sent. When the last step has been sent, the cadence stops and the conversation is left to its normal no-response handling — so a cadence can never nudge a contact indefinitely.

## Selecting a cadence on a campaign

On an **Automatic** inventory load, the AI settings include a **Re-engagement** picker. Choose a cadence to enable follow-ups for every conversation loaded from that campaign, or leave it as **No follow-up cadence** to never follow up. The chosen cadence is snapshotted onto each activity when the inventory is loaded, so editing or deleting a cadence later does not disturb conversations already in flight.

The same section also exposes the **Business hours** calendar (see below), which every follow-up respects.

## Business hours

Every follow-up is **background-initiated** — the automation sends it on its own, not in response to a live message. Such sends are only made while the campaign's **business-hours calendar** is open, evaluated in the **contact's local time zone**, so a contact is never followed up after hours. Business-hours calendars come from the [Contact Center](../contact-center/index.md) **Business Hours** feature, which the SMS automation feature brings in automatically; when no calendar is set the cadence still runs but is not restricted by hours.

A **live reply** to a contact who is *actively messaging* is never gated — only the proactive follow-ups a cadence sends are. Likewise, a reply the automation owes to a message the contact just sent is completed normally, not treated as a follow-up.

## How follow-ups are sent

A background task evaluates due follow-ups on a short interval. For each automated conversation it only acts when:

- a cadence is selected and enabled, and the conversation has not yet used all of the cadence's steps;
- the last thing said in the conversation was the automation's (if the contact has replied, a reply is owed instead of a follow-up);
- the contact has been silent longer than the current step's **After (minutes)**; and
- the business-hours calendar is open for the contact.

When all of those hold, the step's message is sent (verbatim for a defined message, or freshly composed for an AI step), the step counter advances, and the no-response window is restarted so the contact has the full timeout to answer the follow-up before the conversation is closed out.
