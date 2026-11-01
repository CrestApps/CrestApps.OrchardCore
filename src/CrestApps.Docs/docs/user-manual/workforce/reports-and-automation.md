---
sidebar_label: Reports and Automation
title: Workforce Reports and Automation
description: "Get started with Workforce, load the sample, build reports from punches, timesheets, attendance, time off and predictability pay, start workflows from Workforce events, ask the AI assistant who is working or for your schedule, and move settings between sites."
technical_manual:
  - modules/workforce
---

This page covers what helps you set up Workforce and use what it records: the **Get Started** checklist and the sample, reports, workflows, the AI assistant, and moving settings between sites.

| | |
| --- | --- |
| **Menu** | Workforce > Get Started; Reports; Workflows; Configuration > Recipes; Configuration > Import/Export |
| **Permission** | Manage workforce teams for Get Started; the permission of each data set for reports |
| **Feature** | Workforce, and Reports, Workflows, AI or Workforce API for each part |

<AskYourAdmin />

## Get started

**Workforce > Get Started** lists the steps of setting up Workforce in order: the team tree, workers, shift planning, time off, labor rules, the time clock and My Work. Each step shows what is there now, is marked **Done** once it is set up, and has a button to its page. A step whose feature is off says so, with a link to the features.

To try Workforce with sample data, run **Workforce sample** under **Configuration > Recipes**. It adds a region with a clinic and two teams, six workers, early and late shift templates, vacation and sick time, an example rule set, and a time clock with pay periods every two weeks. Link the sample workers to user accounts under **Workforce > Workers** to try My Work as them.

:::warning[The sample rule set is an example]
The sample's labor rules show how rule sets work. Check the rules and numbers that apply where your teams work before you use them.
:::

## Reports

With **Reports** on, the report builder has a **Workforce** data source. Each data set lists only the teams the reader manages, or all of them with **Manage the whole workforce**, and shows each row's worker and team by name.

| Data set | One row per | Needs |
| --- | --- | --- |
| **Punches** | Punch: when, what, from where, at the site or not, corrected or removed | Approve timesheets, with the Time Clock |
| **Timesheet lines** | Line of a timesheet: day, pay code, hours, where they came from, and the timesheet's period and status | Approve timesheets, with the Time Clock |
| **Attendance exceptions** | Exception: kind, day, minutes, points and status | Approve timesheets, with the Time Clock |
| **Time-off requests** | Request: dates, amount, status, when asked for and decided | Approve time off, with Time Off |
| **Predictability pay** | Entry: the change, the notice, the premium and its status | Approve predictability pay, with Labor Rules |

For example, a report of **Timesheet lines** grouped by team and pay code shows overtime hours by team for a pay period.

## Workflows

With **Workflows** on, workflows can start from Workforce events, in the **Workforce** group of the activities:

| Event | Starts when |
| --- | --- |
| **Time Off Requested**, **Approved**, **Declined**, **Canceled** | Time off is asked for, approved (by a manager, an allotment or the waitlist), declined, or canceled or withdrawn |
| **Schedule Published** | A team's schedule is published |
| **Punched In**, **Punched Out** | A worker clocks in or out |
| **Attendance Exception Raised** | A late start, missed punch, no-show or other exception is raised |
| **Attendance Points Threshold Reached** | A worker's points reach their policy's threshold |
| **Timesheet Submitted**, **Timesheet Approved** | A timesheet is submitted or approved |

Each event can be limited to some teams. The workflow's input has the worker and team, and the ids of what happened, so later activities can look it up. For example, an **Attendance Exception Raised** workflow can email a team's manager about each no-show.

## The AI assistant

With **AI** on, these tools can be chosen for an AI profile, under **Workforce**. They run as the person chatting, with the same permissions and teams as in the admin:

| Tool | Ask it |
| --- | --- |
| **Who is working** | "Who's working at the front desk right now?" Lists who is on shift in the teams the person manages, and whether they are clocked in. |
| **My schedule** | "When am I working next week?" |
| **My time-off balance** | "How much vacation do I have?" |
| **Request time off** | "Book me Friday off as vacation." The request follows the same rules and approvals as one made in My Work. |
| **Find cover** | "Who can cover Sam's shift on Tuesday?" Lists colleagues in the same team who can take it, and why the others can't. It doesn't change the schedule. |
| **Explain schedule problems** | "What's wrong with next week's front desk schedule?" Lists the labor rule problems of the draft. |

## Moving settings between sites

Under **Configuration > Import/Export > Deployment Plans**, add the **Workforce settings** step to export shift templates, time-off types, policies, allotments and blackout dates, and the time clock's pay periods, pay codes and policies, for the features that are on. Teams, activities and types are written by name, so they match the teams of the site you import into. Add the **Workforce teams and workers** step to bring the teams too.
