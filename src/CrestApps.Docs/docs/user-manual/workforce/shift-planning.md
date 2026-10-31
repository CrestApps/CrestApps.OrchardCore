---
sidebar_label: Shift Planning
title: Shift Planning
description: "Plan a team's shifts as a draft from templates and rotations, see where the schedule falls short of the people needed, and publish it to the team's calendar, with a log of every change made after publishing."
technical_manual:
  - modules/workforce
---

Shift Planning builds a team's schedule. You describe your usual shifts once as **shift templates**, set up **rotations** that repeat week after week, and say how many people the team needs with **coverage requirements**. A **schedule** covers one to twelve weeks of one team: you fill it as a draft, check it against coverage on the planner board, then **publish** it to the team's Workforce calendar. Workers see their published shifts on the calendar. When you publish again, only the shifts you changed move, and each change is logged.

| | |
| --- | --- |
| **Menu** | Workforce > Schedules, Workforce > Rotations, Workforce > Coverage, Workforce > Shift Templates |
| **Permission** | Plan team schedules, Publish team schedules, Manage shift templates, Manage the whole workforce |
| **Feature** | Workforce Shift Planning (it needs Workforce) |

<AskYourAdmin />

Schedules, rotations and coverage belong to a team, so a manager or scheduler only sees those of the teams they manage, as described in [Workers and Teams](./workers-and-teams.md#who-can-see-what). Shift templates are shared by every team.

## Shift templates

Open **Workforce > Shift Templates** and choose **Add shift template**.

| Field | What it does |
| --- | --- |
| **Name** | The template's name, such as *Day* or *Night*. |
| **Hours** | When the shift starts and ends. An end at or before the start is on the next day, so *22:00 to 06:00* is a night shift. |
| **Breaks and activities** | One per line: a time range, then an activity code, such as *13:00-13:30 Meal*. The hint lists your activity codes. Each must fit inside the shift. |
| **Color** | The color the shift is drawn in on the planner board. |
| **Teams** | The teams that use the template. Leave it empty for every team. |
| **Skills needed** | The skills a worker needs to work the shift. |
| **Enabled** | Untick to stop offering the template. |

Whether a break counts as paid time, or as time the worker is available for work, comes from its [activity code](./workers-and-teams.md#activity-codes).

## Rotations

A rotation is a pattern of shifts that repeats every few weeks, such as *days Monday and Tuesday, then Wednesday the week after*. Open **Workforce > Rotations** and choose **Add rotation**.

| Field | What it does |
| --- | --- |
| **Name** | The rotation's name. |
| **Team** | The team that uses it. Choosing a team reloads the page with its workers and templates. A saved rotation keeps its team. |
| **Weeks** | How many weeks the pattern lasts before it repeats, from 1 to 12. |
| **Pattern** | For each day of each week, the shift template worked that day, or **Off**. |
| **Workers** | The team's workers who follow the rotation. |
| **New workers start** | The day the workers you add now start week 1. Workers already following the rotation keep their day, which is listed under **Workers**. |
| **Enabled** | Untick to stop using the rotation. |

## Coverage

Coverage says how many people the team needs at each time. Open **Workforce > Coverage**, choose the team in the list's filter, and choose **Add requirement**.

| Field | What it does |
| --- | --- |
| **Name** | The requirement's name, such as *Front desk*. |
| **Team** | The team it is for. |
| **Skill** | Optional. The skill the people must have. |
| **Rows** | One per line: a weekday (such as *Monday* or *Mon*) or a date (such as *2026-12-24*), a time range, then the **minimum**, the **target** and, if you want one, the **maximum** number of people. For example *Monday 09:00-17:00 2 3*. Use *24:00* for midnight. A date replaces that weekday's rows for the day, which suits holidays. |
| **Applies** | Optional. The first and last day the requirement applies. |

A line that can't be read is shown back to you with the reason, and nothing is saved until every line is right.

## Plan a schedule

Open **Workforce > Schedules** and choose **Start a schedule**. Choose the **Team**, the **First day** and the number of **Weeks** (1 to 12), and optionally a **Name**. The planner board opens.

The board shows one week at a time; use the arrows above it to move between weeks. Each row is a worker on the team that week, then **Open shifts**, which have nobody yet. The **Hours** column adds up each worker's shifts for the week, less unpaid breaks. A worker who has shifts but isn't on the team that week is marked **Off team**.

- **Apply rotations** adds the shifts of the team's rotations for everyone who follows one. A worker's day that already has a shift is left alone, so you can apply rotations again after changing them.
- **Copy** copies the shifts of another schedule of the same team, moved by the days between the two schedules. Shifts that would fall outside this schedule are left out.
- Point at a day and choose **Add** to add a shift, or choose a shift to change it.

In the shift editor, choose the **Worker** (or **Nobody yet** for an open shift), the **Day** and the **Hours**, and list its **Breaks and activities** as in a template. Choose a **Template** and **Use its times** to fill the hours and breaks from it. **Notes** are shown to the worker on the calendar. **Delete** removes the shift.

A shift must fall inside the schedule's dates, end after it starts, last at most 24 hours, and belong to a worker who is on the team that day.

### Check coverage

The **Coverage** row under the board shows each day against the team's coverage requirements, in half hours. Breaks and meals that don't count toward staffing are left out, so a worker on a meal isn't counted.

| Badge | Means |
| --- | --- |
| **Covered** | Every half hour has at least the target. |
| *n* **under target at** *time* | Every half hour has the minimum, but some have fewer than the target. It shows the worst one. |
| *n* **below the minimum at** *time* | Some half hours have fewer people than the minimum. It shows the worst one. |
| **No requirement** | Nobody is needed that day. |

## Publish

Choose **Publish** to put the schedule on the team's Workforce calendar. Open shifts stay off the calendar until you give them to someone. You need **Publish team schedules**; **Plan team schedules** alone lets you prepare the draft.

After the first publish, the button reads **Publish changes**. Publishing again adds new shifts, moves or reassigns the ones you changed, and removes the ones you deleted. Shifts you didn't touch stay as they are. If a shift can't be published, a warning says which one and why, and the others are still published.

**Change log** lists every shift added, moved, reassigned, removed or whose breaks changed after the first publish, newest first, with the version, who published and when. A schedule that was never published can be deleted from its **Actions** menu. A published one can't: remove its shifts and publish again instead.
