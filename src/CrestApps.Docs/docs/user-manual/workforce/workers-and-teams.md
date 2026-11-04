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
| **Menu** | Workforce > Workers, Teams, Workforce > Activity Codes |
| **Permission** | View workers, Manage workers, Manage workforce teams, Manage activity codes, View sensitive worker details, Manage the whole workforce |
| **Feature** | Workforce (it needs Calendars, Skills and Teams) |

<AskYourAdmin />

## Build the team tree

Teams are kept by the **Teams** feature, so every feature uses the same tree. Open **Teams** to add them; see [Teams](./teams.md) for the fields every team has. Workforce adds the **Workforce** card to the team editor, and **Workers**, **Add a worker** and **Calendar** to each team's **Actions** menu.

### The Workforce card

| Field | What it does |
| --- | --- |
| **Address** | Where the team works, for a site. |
| **Site coordinates**, **Time clock policy** | Shown when the Time Clock is on. See [Time Clock](./time-clock.md). |
| **Open shifts go to**, **Shift trades** | Shown when Shift Planning is on. See [Team settings](./shift-planning.md#team-settings). |
| **Shifts under approved time off** | Shown when Shift Planning and Time Off are on. |

The team's **Linked role** on the **Team** card is the role whose users can read the team's Workforce calendar, as the team's members can.

A team that still has teams under it, or current workers, can't be deleted. Move them first. Deleting a team keeps its calendar.

## Who can see what

- People with **Manage the whole workforce** see every team and every worker.
- Anyone else sees the workers of the teams where they are a **Manager** or a **Scheduler**, and of the teams under those. They can only add workers to those teams.
- On a team's Workforce calendar, its managers and schedulers (and those of the teams above it) can make changes; its members, and the users in its **Linked role**, can read it.

## Add a worker

Open **Workforce > Workers** and choose **Add worker**, or choose **Add a worker** from a team's **Actions** menu.

**Worker**

| Field | What it does |
| --- | --- |
| **Name** | How the worker is shown. |
| **User account** | Optional. The user the worker signs in as. Without one, the worker can still be scheduled but can't sign in to see their schedule. A user can be only one worker. A worker with an account takes its **Name** and **Email** from it, and they can't be changed here. |
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

## From the user editor

People with an account are managed with their user too. With **Manage workers**, the user editor (**Access Control > Users > Edit**) has a **Workforce** card:

- For a user who isn't a worker yet, tick **Make them a worker** and fill in their employee number, job title, employment, status, hire date, **Main team** and contracted hours. Saving the user makes the worker, with the user's name and email.
- For a worker, the same fields change their worker. A new main team is joined from today, and the teams they were in stay. **Open their worker page** has the rest: teams and their dates, hours, availability, pay rates and certificates.
- Whenever the user is saved, their worker takes the user's name and email.

The **Workers** list stays, for workers without an account (such as people who clock in only at a kiosk) and for HR records kept after an account is closed.

## Find workers

**Workforce > Workers** lists the workers you can see. Search by name, email or employee number, and filter by team (a team includes the teams under it) or status. A badge shows workers with **No account**.

## Activity codes

An activity code says what a worker does during part of a shift: work, a break, a meal, a meeting, training and so on. The usual codes are added the first time Workforce is turned on. Open **Workforce > Activity Codes** to change them or add your own. With the time clock on, each code also has the Time Tracker **stage** its clocked time is kept in.

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
