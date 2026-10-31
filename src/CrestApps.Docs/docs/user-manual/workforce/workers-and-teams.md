---
sidebar_label: Workers and Teams
title: Workers and Teams
description: "Set up the people who work and the teams they belong to, from regions and sites down to teams and crews, so managers see and plan only their own people, and every team gets a calendar of its shifts and time off."
technical_manual:
  - modules/workforce
---

Workforce keeps the people who work and how they are organized. A **worker** is anyone you schedule: an employee, a part-timer, a contractor or a crew member. A worker can have a user account, so they can sign in to see their schedule, or none at all. **Teams** organize workers from the top down, such as a region, a site, a department, a team and a crew. The managers of a team see and plan its workers and the workers of every team under it.

Every team gets a **Workforce calendar** that holds its published shifts and approved time off. You can open it from the team list.

| | |
| --- | --- |
| **Menu** | Workforce > Workers, Workforce > Teams, Workforce > Activity Codes |
| **Permission** | View workers, Manage workers, Manage workforce teams, Manage activity codes, View sensitive worker details, Manage the whole workforce |
| **Feature** | Workforce (it needs Calendars and Skills) |

<AskYourAdmin />

## Build the team tree

Open **Workforce > Teams** and choose **Add team**. Start at the top, such as your organization or a region, then add the teams under it. From a team's **Actions** menu, **Add a team under it** starts a new team with that team as its parent.

| Field | What it does |
| --- | --- |
| **Name** | The team's name. |
| **Kind** | **Organization**, **Region**, **Site**, **Department**, **Team** or **Crew**. A crew is a team sent out as one unit for field work. |
| **Part of** | The team it belongs to, or **Nothing (top level)**. A team can't be placed under itself or under one of its own teams. |
| **Description** | What the team does. |
| **Time zone** | Where the team works. Its days start and end at midnight in this time zone. |
| **Address** | Where the team works, for a site. |
| **Role that can read the calendar** | Optional. Users in this role can read the team's Workforce calendar, as the team's members can. |
| **Open shifts go to**, **Shift trades** | Shown when Shift Planning is on. See [Team settings](./shift-planning.md#team-settings). |
| **In use** | Untick to stop using a team without deleting it. |

A team that still has teams under it, or current members, can't be deleted. Move them first. Deleting a team keeps its calendar.

## Who can see what

- People with **Manage the whole workforce** see every team and every worker.
- Anyone else sees the workers of the teams where they are a **Manager** or a **Scheduler**, and of the teams under those. They can only add workers to those teams.
- On a team's Workforce calendar, its managers and schedulers (and those of the teams above it) can make changes; its members, and the users in its **Role that can read the calendar**, can read it.

## Add a worker

Open **Workforce > Workers** and choose **Add worker**, or choose **Add a worker** from a team's **Actions** menu.

**Worker**

| Field | What it does |
| --- | --- |
| **Name** | How the worker is shown. |
| **User account** | Optional. The user the worker signs in as. Without one, the worker can still be scheduled but can't sign in to see their schedule. A user can be only one worker. |
| **Job title**, **Email**, **Phone** | Contact details. |
| **Employee number** | Optional, but two workers can't share one. |
| **HR or payroll id** | The worker's id in another system, for exports. |

**Employment**

| Field | What it does |
| --- | --- |
| **Employment** | **Full time**, **Part time**, **Seasonal**, **Temporary**, **Contractor** or **Subcontractor**. |
| **Status** | **Active**, **On leave**, **Suspended** or **Terminated**. Only active workers are scheduled. |
| **Hire date**, **Seniority date**, **Last day worked** | Employment dates. The seniority date, when set, is used instead of the hire date to award open shifts by seniority. |
| **Date of birth** | Only shown to people with **View sensitive worker details**. It is only used for the rules about young workers. |

**Teams**

Each row is one team the worker belongs to, with their **Role** (**Member**, **Lead**, **Scheduler** or **Manager**) and, optionally, the days the membership starts and ends. The first team is the worker's main team. Leave a row on **(none)** to skip it. Save to get more empty rows.

**Hours and availability**

| Field | What it does |
| --- | --- |
| **Time zone** | The worker's time zone, when it isn't their main team's. |
| **Contracted hours a week**, **Fewest hours a week**, **Most hours a week**, **Most hours a day** | Limits scheduling takes into account. |
| **Availability** | When the worker can work, one line per day, such as *Monday 09:00-17:00*, *Tuesday 08:00-12:00 preferred, 13:00-17:00* or *Saturday unavailable*. A time can run past midnight, such as *Sunday 22:00-06:00*. Leave it empty when the worker can work any time. |
| **Home base** | Where a field worker starts and ends the day. |

**Skills**

One line for each enabled skill: choose how well the worker holds it, from **1 - Trainee** to **5 - Expert**, or **Doesn't hold it**. These are the same skills as [Skills and Certifications](./skills-and-certifications.md).

**Certificates**

After the worker is saved, the editor lists their certificates. Add one with **Certification**, **Number**, **Issued on**, **Valid until** (leave it empty to work it out from the certification) and **Checked**, then choose **Add**. The certificate also appears in **Workforce > Certificates**.

## Find workers

**Workforce > Workers** lists the workers you can see. Search by name, email or employee number, and filter by team (a team includes the teams under it) or status. A badge shows workers with **No account**.

## Activity codes

An activity code says what a worker does during part of a shift: work, a break, a meal, a meeting, training and so on. The usual codes are added the first time Workforce is turned on. Open **Workforce > Activity Codes** to change them or add your own.

| Field | What it does |
| --- | --- |
| **Name** | The activity's name. |
| **Short code** | Shown on schedules where there is little room, such as *BRK*. |
| **Kind** | What kind of activity it is. Breaks, meals, meetings, training, coaching, offline work, travel and time off keep a worker from being booked during them. |
| **Color** | The color it is drawn in. |
| **Paid** | Whether the time is paid. |
| **Counts as work** | Whether the time counts toward hours rules, such as the most hours a week. |
| **Counts toward staffing** | Whether the worker counts as available to take work during it. |
| **Enabled** | Untick to stop using a code. |

## Shifts and appointments

When **Scheduling** is on, a staff member whose hours come from **Shifts on a calendar** can use their team's Workforce calendar as a **Shift calendar**; someone on two teams picks both teams' calendars. They can then be booked during their shifts, but not during the breaks, meals, meetings and training inside them.
