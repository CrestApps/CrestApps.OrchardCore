---
sidebar_label: Time Tracker
title: Time Tracker
description: "Set up the stages people clock into and the categories their time counts toward, clock in and switch stage through the day, correct time with a reason, and report the hours of each category."
technical_manual:
  - modules/time-tracker
---

Time Tracker records what people do through their working day, such as Work, Break and Lunch, and adds the time up into categories you choose, such as Payable, Billable or Vacation.

| | |
| --- | --- |
| **Menu** | Time Tracker > My Time, Time Entries, Stages, Categories; Reports > Workforce & Payroll > Hours by Category |
| **Permission** | Track one's own time for My Time; View anyone's time and the time report; Edit anyone's time; Manage time stages and categories |
| **Feature** | Time Tracker; Audit Trail to record every change |

<AskYourAdmin />

## Set up categories and stages

1. Go to **Time Tracker > Categories** and select **Add category** for each total you want in the report, such as *Payable*, *Billable* and *Break*. **Each minute counts** is 1 for most; 1.5 counts the time half again, such as for an overtime premium.
2. Go to **Time Tracker > Stages** and select **Add stage** for each thing people do, such as *Work*, *Lunch* or *Training*. Under **How its time counts**, tick the categories its time adds to: *Work* to both *Payable* and *Billable*, *Lunch* to *Break*.
3. Untick **People pick it on their clock** for stages that only come from somewhere else, such as *On a call* from the Contact Center.

## Track your time

Go to **Time Tracker > My Time**. Choose a stage in the list and select **Clock in** to start. When you change what you are doing, such as going to *Lunch*, choose the stage and select **Switch**, and switch back to *Work* when you return; your time moves from one to the next without a gap. Select **Clock out** at the end of the day.

**Today** shows the day's stages as a bar and a list, and **This week** your hours in each category by day. An entry a manager changed says so.

## Correct someone's time

Go to **Time Tracker > Time Entries**, find the entry by user name, stage or day, and select **Edit**. Change the stage, the start or the end, and enter the **Reason**. The entry keeps what it was, who changed it and why, and the Audit Trail records it when it is on. To add time that wasn't recorded, select **Add time**; to remove an entry, enter a reason under **Remove this time**. Time can't overlap other time of the same person.

## Time from the time clock

With the Workforce time clock on, the punches of a worker whose team uses a time clock policy keep their day here: their **Time Entries** say **From the Time Clock**, open read-only, and are corrected with punches on the worker's timesheet. Their **My Time** says their time is kept by the time clock and shows its buttons, **Clock in**, **Start break**, **Start meal**, the matching **End** buttons and **Clock out**, the same as in My Work. A worker whose team has no time clock policy has no clock to punch, so they clock in and switch stages on My Time as anyone else does. See [Time Clock](./time-clock.md).

## Time from the Contact Center

With **Contact Center Time Tracker** on, agents' states are kept here while they are signed in: available, on a call, wrap-up, breaks and the rest, each in its stage. For a worker on the time clock, these take the place of their clocked work for that time.

Go to **Time Tracker > Contact Center Stages** to choose the stage of each state. **Not tracked** leaves a state out. Under **Reasons**, give a reason a stage of its own, such as *Lunch* for a lunch break; **As its state** uses the state's stage. The usual states get a stage when the feature is turned on. These entries are read-only here; they come from the agent state log.

## Time off

With Workforce Time Off on, every day of approved time off is kept here once the day comes, in the stage of its type, such as *Vacation*. It shows as **From approved time off** and is changed on the time off itself. See [Time Off](./time-off.md#time-off-in-time-tracker).

## Hours by category

**Reports > Workforce & Payroll > Hours by Category** shows a row for each person, with a column for each category and the total, for the date range you choose (this week, Monday to Sunday, at first). Days are counted in the site's time zone, whatever your own time zone is, and the report's heading names it. Search by user name with **Person**, and tick **A row per day** to see each day apart, such as *Mon 2 Nov* (an export writes the date as `2026-11-02`). It is a report of the **Reports** feature, so it needs Reports on, and it exports as CSV (and Excel with **Reports (OpenXml)**). A stage's time adds to each of its categories, so the columns don't add up to the total.
