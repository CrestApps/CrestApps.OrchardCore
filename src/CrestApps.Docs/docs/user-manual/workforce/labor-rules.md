---
sidebar_label: Labor Rules
title: Labor Rules
description: "Combine labor rules such as the most hours a week, rest between shifts and breaks into rule sets for teams and workers, set each to warn, block or need an override, check schedules before publishing, and approve the predictability pay owed for late changes."
technical_manual:
  - modules/workforce
---

Labor rules keep schedules within the limits your teams must follow, such as the most hours a week or the rest between shifts. You pick rules from a catalog, give them their numbers, and put them in a **rule set** for a team, a site or a jurisdiction. Each rule either **warns**, **blocks** the change, or **needs an override**, which lets someone allowed go ahead with a reason that is recorded. Schedules are checked before they are published, and claims, trades and reassignments as they are made. When a published shift changes late, **predictability pay** records the premium the worker is owed.

| | |
| --- | --- |
| **Menu** | Workforce > Labor Rules, Workforce > Rule Overrides, Workforce > Predictability Pay |
| **Permission** | Manage labor rules, Override labor rules, Approve predictability pay, Manage the whole workforce |
| **Feature** | Workforce Labor Rules (it needs Workforce Shift Planning) |

<AskYourAdmin />

:::warning[No rule sets come built in]
Laws change and differ from place to place, so no rule set is set up for you. Check the rules and their numbers with someone who knows the law where your teams work.
:::

## Rule sets

Open **Workforce > Labor Rules** and choose **Add rule set**.

| Field | What it does |
| --- | --- |
| **Name** | The set's name, such as *California hourly staff*. |
| **Jurisdiction** | The place or law the set follows, as a reminder. |
| **Teams** | The teams it applies to, with the teams under them. |
| **Employment types** | Leave empty for everyone, or pick types such as part time. |

A worker gets one rule set: the one given to them alone, or else the nearest team's, starting with their own team and going up. A set limited to some employment types is skipped for other workers, who get the next set up the tree.

### Rules

Choose a rule under **Add rule** and choose **Add rule**. Each rule shows what it checks, its numbers, and how it is enforced:

| Enforcement | What happens |
| --- | --- |
| **Warn** | The change goes ahead and the problem is shown. |
| **Block** | The change is refused. |
| **Needs an override** | The change goes ahead only when someone with **Override labor rules** gives a reason. The reason is recorded under **Rule Overrides**. |

| Rule | What it checks |
| --- | --- |
| **Most hours a day** | Paid hours in a day. |
| **Most hours a week** | Paid hours from Monday to Sunday. |
| **Fewest hours a week** | A week scheduled below a number of hours, such as a worker's contract. |
| **Rest between shifts** | The hours between the end of one shift and the start of the next. |
| **Most days in a row** | Days worked in a row. |
| **Days off a week** | Days with no shift from Monday to Sunday. |
| **Longest shift**, **Shortest shift** | How long a shift lasts, breaks included. |
| **Breaks and meals** | A shift longer than a number of hours needs a number of minutes of breaks or meals. |
| **Young workers** | A worker under an age works only so many hours a day and a week, and not after a time of day. It uses the worker's date of birth. |
| **Skills and certificates** | A shift whose template needs skills goes only to a worker who holds them, with a valid certificate where the skill needs one, on its day. |
| **No work during time off** | A shift during the worker's approved [time off](./time-off.md). |
| **Schedule notice** | A change the employer makes to a published shift with less than a number of days' notice. |
| **Overtime** | Hours past the point where they are paid as overtime, in a week or a day. |
| **Predictability pay** | Records the premium for late changes; see [Predictability pay](#predictability-pay). |

When a worker has a rule set, its **Rest between shifts**, **Most days in a row** and hours rules also guide **Auto-schedule**, in place of the usual 10 hours, 6 days and the worker's own hours.

## Checking a schedule

When the team's rule set has rules, the planner board lists what the draft breaks, as publishing would find it, above the board. The first five are shown; open **more** to see the rest.

- When a rule that **blocks** is broken, the **Publish** button is hidden. Change the shifts first.
- When a rule that **needs an override** is broken, choose **Publish with an override** with the reason. Without **Override labor rules**, someone who has it must publish.
- **Warnings** don't stop publishing; the message after publishing says how many there were.

Claims of [open shifts](./shift-planning.md#open-shifts), [shift trades](./shift-planning.md#shift-trades) and reassignments are checked against the worker's rule set too: a rule that blocks or needs an override refuses them, with the reason.

## Rule overrides

**Workforce > Rule Overrides** lists every broken rule that went ahead with an override, newest first: the rule, the worker, what was broken, the reason, who allowed it and when. Choose **Schedule** to open the schedule it was published in.

## Predictability pay

Add the **Predictability pay** rule to a rule set to record a premium when the employer changes a published shift with less notice than the rule's **Days of notice**:

| Change | Premium |
| --- | --- |
| A shift is added, or given to the worker | **Hours for an added shift** |
| A shift is moved | **Hours for a moved shift** |
| A shift is removed, or taken from the worker | **Share of a removed shift's hours** |

Changes a worker asked for, such as trades, claims and time off, owe nothing.

Open **Workforce > Predictability Pay** to see what is owed for the teams you manage. Choose **Approve** to have it paid, or give the reason it isn't owed, such as an emergency closure, and choose **Waive**. Filter by status to see approved, waived and exported entries.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage labor rules** | Rule sets and the rule override list. |
| **Override labor rules** | Publishing with a reason despite rules that need an override. |
| **Approve predictability pay** | Approving and waiving the predictability pay of the teams you manage. |
