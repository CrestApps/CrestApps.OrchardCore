---
sidebar_label: Shift Planning
title: Shift Planning
description: "Plan a team's shifts as a draft from templates, rotations or the auto-scheduler, see where the schedule falls short of the people needed, publish it to the team's calendar, offer the shifts with nobody as open shifts, and let workers trade shifts, with a log of every change made after publishing."
technical_manual:
  - modules/workforce
---

Shift Planning builds a team's schedule. You describe your usual shifts once as **shift templates**, set up **rotations** that repeat week after week, and say how many people the team needs with **coverage requirements**. A **schedule** covers one to twelve weeks of one team: you fill it as a draft, by hand, from rotations or with **Auto-schedule**, check it against coverage on the planner board, then **publish** it to the team's Workforce calendar. Workers see their published shifts on the calendar. When you publish again, only the shifts you changed move, and each change is logged.

Shifts with nobody become **open shifts** that the team's workers can claim, and published shifts can be given away or swapped through **shift trades**.

| | |
| --- | --- |
| **Menu** | Workforce > Schedules, Workforce > Open Shifts, Workforce > Shift Trades, Workforce > Rotations, Workforce > Coverage, Workforce > Shift Templates |
| **Permission** | Plan team schedules, Publish team schedules, Manage open shifts, Approve shift trades, Manage shift templates, Manage the whole workforce |
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
| **Skill** | Optional. The skill the people must have. Only workers who hold the skill that day count toward it. |
| **Rows** | One per line: a weekday (such as *Monday* or *Mon*) or a date (such as *2026-12-24*), a time range, then the **minimum**, the **target** and, if you want one, the **maximum** number of people. For example *Monday 09:00-17:00 2 3*. Use *24:00* for midnight. A date replaces that weekday's rows for the day, which suits holidays. |
| **Applies** | Optional. The first and last day the requirement applies. |

A line that can't be read is shown back to you with the reason, and nothing is saved until every line is right.

## Plan a schedule

Open **Workforce > Schedules** and choose **Start a schedule**. Choose the **Team**, the **First day** and the number of **Weeks** (1 to 12), and optionally a **Name**. The planner board opens.

The board shows one week at a time; use the arrows above it to move between weeks. Each row is a worker on the team that week, then **Open shifts**, which have nobody yet. The **Hours** column adds up each worker's shifts for the week, less unpaid breaks. A worker who has shifts but isn't on the team that week is marked **Off team**.

- **Auto-schedule** adds shifts from the team's templates to cover its coverage requirements. See [Auto-schedule](#auto-schedule).
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

### Auto-schedule

**Auto-schedule** fills the draft for you. It looks at every half hour of the schedule, finds where the team has fewer people than its coverage requirements ask for, and gives shifts from the team's templates to the workers who can take them, until no shift would help. It then tries handing shifts to other workers, swapping templates and dropping shifts nobody needs, while that makes the schedule better. It stops after 30 seconds at most.

It keeps to these rules:

- Only active workers who are on the team that day, and not marked unavailable for the whole day, get shifts.
- A shift fits inside the worker's available hours for that day, when they have any, and never overlaps hours they are unavailable. A shift inside their *preferred* hours is favored.
- A worker gets at most one shift a day, and only a template whose skills they hold for the whole schedule.
- A worker rests at least 10 hours between shifts, works at most 6 days in a row, and stays within their **Max hours per week** (or their contracted hours, or 40), and **Max hours per day**.
- The shifts already in the draft stay. They count toward coverage, and so do the worker's shifts on other teams for their own time.

Falling short of the minimum counts far more than falling short of the target, and people beyond the target or the maximum count against a shift. After the run, a message says how many shifts were added and what share of the people wanted and needed the schedule now covers; the share also shows above the board. You can change any shift it added, and run it again after changing the coverage or the templates.

## Publish

Choose **Publish** to put the schedule on the team's Workforce calendar. Open shifts stay off the calendar until you give them to someone. You need **Publish team schedules**; **Plan team schedules** alone lets you prepare the draft.

Shifts with nobody are offered as [open shifts](#open-shifts).

After the first publish, the button reads **Publish changes**. Publishing again adds new shifts, moves or reassigns the ones you changed, and removes the ones you deleted. Shifts you didn't touch stay as they are. If a shift can't be published, a warning says which one and why, and the others are still published.

**Change log** lists every shift added, moved, reassigned, swapped, filled from an open shift, removed or whose breaks changed after the first publish, newest first, with the version, who made the change and when. Changes a worker asked for, through a trade or a claim, are marked **Worker's request**. A schedule that was never published can be deleted from its **Actions** menu. A published one can't: remove its shifts and publish again instead.

## Open shifts

Open **Workforce > Open Shifts** to see the shifts with nobody of the teams you manage. Publishing a schedule posts its shifts with nobody here, and publishing again updates them, or stops offering those you deleted or gave to someone in the draft. Choose **Post an open shift** to offer one that isn't in any schedule, such as cover for someone off sick: choose the team, the day, the hours and, if you want, a template, whose skills a worker then needs.

Choose **View** to open a shift. It lists its claims and **Give it to someone**, with the team's workers who can take it: on the team that day, holding its skills, with no other shift at that time, resting at least 10 hours before and after, and within their weekly and daily hours. A worker who can't take it isn't listed; a claim from one shows why.

How a shift is given depends on the team's **Open shifts go to** setting (see [Team settings](#team-settings)):

| Setting | What happens |
| --- | --- |
| **Whoever a manager approves** | Claims wait. Choose **Approve** on a claim, or give the shift to anyone listed. The other claims are declined. |
| **The first eligible worker to claim** | The first worker who claims it and can take it gets it at once. |
| **The most senior claimant, after a day** | Claims are taken for a day (or until the shift starts, if sooner). Then the claimant with the earliest seniority date, or hire date, who can still take it gets it. If none can, a manager decides. |

A filled shift goes on the team's calendar and into its schedule, so publishing again leaves it alone, and the change log shows **Open shift filled**. **Record a claim** claims for a worker who asked you, and **Stop offering** takes a shift off the list.

## Shift trades

Workers can give a published shift to a colleague, let anyone eligible pick it up, or swap it for a colleague's shift. Open **Workforce > Shift Trades** to see the trades of the teams you manage, and choose **Offer a trade** to offer one on a worker's behalf:

| Field | What it does |
| --- | --- |
| **Worker** | The worker offering one of their shifts. |
| **Trade** | **Give the shift away**, or **Swap it for a colleague's shift**. |
| **Shift** | One of their published shifts in the next 60 days. |
| **Colleague** | Who it is offered to. Leave **Anyone who can take it** for a shift any eligible colleague can pick up. A swap needs a colleague. |
| **Their shift** | For a swap, the colleague's shift wanted in exchange. |
| **Note** | A note to the colleague. |

Choose **View** to open a trade. While it is offered, it lists who could accept it and why the others can't, by the same rules as open shifts. In a swap, both workers are checked for the shift they would get. **Accept for** accepts on a colleague's behalf, **Turn down** does so for the colleague it was offered to, and **Take back** withdraws it for the worker who offered it.

When the team's **Shift trades** setting needs a manager, an accepted trade waits for **Approve** or **Decline**. Otherwise it applies as soon as the colleague accepts. Applying a trade moves the shifts on the calendar and in the schedule, so publishing again leaves them alone, and the change log shows **Reassigned** or **Swapped** as the worker's request. A trade is checked again when it is approved, and refused if a shift changed since it was offered or a rule would now be broken.

## Team settings

When Shift Planning is on, a team's editor under **Workforce > Teams** has two more settings. A team left at **As the parent team** takes its parent's setting; at the top, a manager approves both.

| Field | What it does |
| --- | --- |
| **Open shifts go to** | Whoever a manager approves, the first eligible worker to claim, or the most senior claimant after a day. |
| **Shift trades** | Whether a manager approves trades, or they apply when the colleague accepts. |
