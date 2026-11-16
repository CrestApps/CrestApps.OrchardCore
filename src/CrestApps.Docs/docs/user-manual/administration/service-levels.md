---
sidebar_label: Service Levels
title: Service Levels
description: Set how fast each kind of work must be answered and resolved at each priority, counted in business hours, with a warning before a target is missed.
technical_manual:
  - modules/service-levels
---

A **service level policy** says how long a piece of work may take: how soon it must first be answered and how soon it must be resolved, at each priority. Each policy is for one kind of work, such as work orders. While a record is open, a timer runs for each target, stops while the record waits, and raises a warning before the target is missed and a breach when it is.

| | |
| --- | --- |
| **Menu** | Tools > Service Levels |
| **Permission** | Manage service level policies |
| **Feature** | Service Levels, turned on by the features that use it, such as Work Orders |

<AskYourAdmin />

## Create a policy

1. Go to **Tools > Service Levels** and select **Add Policy**. When more than one kind of work uses service levels, choose the one the policy is for, such as **For Work orders**.
2. In **General**, enter a **Name** such as *Premium support*.
3. In **Counted on**, choose the business hours schedule the targets are counted in, or **Around the clock** to count every hour. A schedule that is never open can't be chosen: no target could be met. If a schedule in use is later changed so that it never opens, the targets on it count around the clock instead, and a ticket shows **No business hours** by the target and notes it in its history. See [Business Hours Schedules](business-hours-schedules.md).
4. In **Warn at**, enter how much of a target may pass before a warning is raised, such as *80* %.
5. In **Targets**, enter the minutes allowed for each metric at each priority. Leave a cell empty for no target. The **Any other priority** row applies to every priority that has no row of its own.
6. Some kinds of work add their own card. For work orders, **Pause in** chooses the statuses in which the timers stop, such as **On hold**.
7. Select **Save**.

Minutes count in the hours you chose: with a 09:00 to 17:00 schedule, a target of 480 minutes is one working day.

## Fields

| Field | What it does |
| --- | --- |
| **Name** | The name people pick the policy by. Each policy of a kind of work needs its own name. |
| **For** | The kind of work the policy is for. It sets the metrics and priorities of the targets and can't be changed. |
| **Counted on** | The business hours the targets are counted in. Closed hours, weekends and holidays don't count. |
| **Warn at** | How much of a target, in percent, passes before the warning. |
| **Targets** | The minutes allowed for each metric (such as **Response** and **Resolution**) at each priority. |

## Edit or delete a policy

- Select **Edit** to change a policy. Records that already have timers keep their due times until their priority or status changes.
- Select **Delete** to remove a policy. A policy that is still used, for example by a work order type or by timers still running or paused, can't be deleted; the message names what uses it.

A record that is reopened after it was resolved, such as a work order moved back from **Completed** to **In progress**, counts its resolution again from the time it had used, and its warning and breach are raised again if it runs out.

## See it on a record

A work order shows its **Respond by** and **Resolve by** times, with **Due soon**, **Late**, **Met** or **Missed**, on its page. See [Work Orders](../field-service/work-orders.md).
