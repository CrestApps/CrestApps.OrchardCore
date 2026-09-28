---
sidebar_label: Business Hours
sidebar_position: 23
title: Business Hours Calendars
description: Define opening hours and holidays once, then use the calendar on queues, entry points, the dialer's calling window, and automated SMS follow-ups.
---

A **business hours calendar** says when you are open. The same calendar can be used by:

- a **queue**, to hold or overflow callers while closed;
- an **inbound entry point**, to pick the closed action (voicemail, overflow, reject);
- a **dialer profile**, as the outbound calling window, checked in the contact's time zone;
- an **automatic inventory load**, so AI follow-up messages are only sent while open.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Business hours |
| **Permission** | Manage Contact Center business hours |
| **Feature** | Contact Center Business Hours (`CrestApps.OrchardCore.ContactCenter.BusinessHours`), enabled automatically by the features that use it |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a business hours calendar with a weekly schedule and holidays">
  <source src="/img/docs/um-business-hours.mp4" type="video/mp4" />
</video>

## Create a calendar

1. Open **Interaction Center > Management > Business hours** and click **Add calendar**.
2. Enter a **Name** such as *Support hours* and an optional **Description**.
3. Pick the **Time zone** the hours are written in. Empty means UTC.
4. In **Weekly schedule**, tick **Open** for each open day and set **From** and **To**. A new calendar starts with Monday to Friday, 09:00 to 17:00.
5. In **Holidays**, enter one date per line in the form `2026-12-25`. The calendar is closed all day on those dates.
6. Leave **Enabled** ticked and click **Save**.

## Rules worth knowing

- The **To** time is exclusive: 09:00 to 17:00 closes at exactly 17:00.
- The same **From** and **To** time means open for 24 hours.
- A **To** time earlier than **From** runs past midnight, for example 22:00 to 06:00.
- Holiday lines that are not valid dates are ignored, so check the list after saving.
- A **disabled** calendar never closes anything: every queue that uses it is treated as always open.
