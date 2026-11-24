---
sidebar_label: Business Hours
sidebar_position: 23
title: Business Hours in the Contact Center
description: Pick a business hours schedule on queues, entry points, the dialer's calling window, and automated SMS follow-ups. The hours themselves are kept under Content → Business Hours.
technical_manual:
  - contact-center/agents-queues-dialer
  - contact-center/voice-routing
  - modules/business-hours
---

The Contact Center uses your **business hours schedules** to know when you are open. A schedule is kept once, under **Content** → **Business Hours**, and the same schedule can be used by:

- a **queue**, to hold or overflow callers while closed;
- an **inbound entry point**, to pick the closed action (voicemail, overflow, reject);
- a **dialer profile**, as the outbound calling hours, checked in the contact's time zone;
- an **automatic activity load**, so AI follow-up messages are only sent while open;
- other features, such as ticket service levels and workflows.

| | |
| --- | --- |
| **Menu** | **Content** → **Business Hours** |
| **Permission** | Manage business hours schedules |
| **Feature** | Business Hours, enabled automatically with Contact Center Business Hours |

<AskYourAdmin />

## Create the hours

Create a schedule under **Content** → **Business Hours**: a name, a time zone, the hours of each day (with a lunch break if you need one), and holidays or days with special hours. See [Business Hours Schedules](administration/business-hours-schedules.md) for every field.

## Use the hours

- On a **queue**, pick the schedule in **Business hours** on the **Hours and overflow** card. Leave it empty to route around the clock.
- On an **entry point**, pick the schedule in **Business hours**. Leave it empty to stay open around the clock.
- On a **dialer profile**, pick the schedule in **Outbound calling hours** when the calling window is enforced.
- On an **activity load**, pick the schedule in **Business hours** to send follow-ups only while open.

## Rules worth knowing

- The **To** time is exclusive: 09:00 to 17:00 closes at exactly 17:00.
- The same **From** and **To** time means open for 24 hours.
- A **To** time earlier than **From** runs past midnight, for example 22:00 to 06:00.
- A **disabled** schedule never closes anything: every queue and entry point that uses it is treated as always open. A dialer profile that uses a disabled schedule as its calling hours does the opposite: no call is placed, because the window is never open.

## Calendars from earlier versions

Before version 3.0, the Contact Center kept its own business hours calendars under **Interaction Center** → **Management** → **Business hours**. When a site is upgraded, each calendar is copied to a business hours schedule with the same name, so every queue, entry point, dialer profile and activity load keeps its hours. A calendar whose name another schedule already has gets a number added, such as *Support (2)*. Roles that could manage the calendars can manage the schedules.
