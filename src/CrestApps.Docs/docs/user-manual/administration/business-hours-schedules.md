---
sidebar_label: Business Hours Schedules
title: Business Hours Schedules
description: Enter your opening hours, lunch breaks, holidays and special days once, so service levels, workflows and other features count only the hours you are open.
technical_manual:
  - modules/business-hours
---

A **business hours schedule** says when you are open: the hours of each day of the week, in a time zone, and the holidays and days with special hours. Features that measure time in business hours use it. For example, a service level policy can count its targets only while you are open, so a request that arrives on Friday evening is not late on Saturday.

| | |
| --- | --- |
| **Menu** | Settings > Business Hours |
| **Permission** | Manage business hours schedules |
| **Feature** | Business Hours |

<AskYourAdmin />

## Create a schedule

1. Go to **Settings > Business Hours** and select **Add Schedule**.
2. In **General**, enter a **Name** such as *Support desk hours* and an optional **Description**.
3. Choose the **Time zone** the hours are written in. A new schedule starts with the site's time zone.
4. In **Weekly hours**, tick **Open** for each day you are open and enter **From** and **To**. A new schedule starts with Monday to Friday, 09:00 to 17:00.
5. To close for lunch, enter the morning in **From** and **To**, and the afternoon in **Then from** and the second **To**.
6. In **Holidays and special days**, enter one date per line, written as *2026-12-25*:
   - a date alone, or a date followed by a name, is closed all day: *2026-12-25 Christmas Day*;
   - a date followed by hours has special hours that day: *2026-12-24 09:00-13:00 Christmas Eve*.
7. Leave **Enabled** ticked and select **Save**.

## Fields

| Field | What it does |
| --- | --- |
| **Name** | The name people pick the schedule by. Each schedule needs its own name. |
| **Time zone** | The time zone of the hours and the special days. |
| **Enabled** | When it is not ticked, the schedule is not used: features that use it count every hour of the day. |
| **Weekly hours** | For each day, whether you are open and up to two stretches of opening hours. A closing time earlier than the opening time runs past midnight; the same time for both means open all day. |
| **Holidays and special days** | Dates that don't follow the weekly hours. |

## Copy, edit or delete a schedule

- Select **Edit** on a schedule to change it. Timers already counting on it keep the time they used so far and count the rest in the new hours.
- In the row's menu, select **Copy** to start a new schedule from an existing one.
- Select **Delete** to remove a schedule. Timers counting on it keep the time they used so far and count every hour of the day from then on.

## Use a schedule

- A **service level policy** counts its targets in the schedule you choose in **Counted on**. See [Service Levels](service-levels.md).
- The **Check Business Hours** workflow task takes the **Open**, **Closed** or **Unknown** path, so a workflow can act differently out of hours. See [Workflows](../workflows.md).
- The Contact Center uses schedules on queues, entry points, dialer profiles and activity loads. See [Business Hours in the Contact Center](../business-hours.md).
