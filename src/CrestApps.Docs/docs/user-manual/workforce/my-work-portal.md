---
sidebar_label: My Work Portal
title: My Work Portal
description: "The My Work portal for workers: their next shift, schedule, time off, open shifts and trades, availability, notifications by push and email, and a calendar feed; and approvals and who's working for managers. It can be installed on a phone or computer."
technical_manual:
  - modules/workforce
---

**My Work** is the workers' own site, at **/my-work**. Workers see their schedule, ask for time off, claim open shifts, trade shifts with colleagues and say when they can work. They hear about changes in the portal, by push notification on their phone or computer, and by email. Managers see what waits for their decision and who is working now. My Work can be installed like an app: on a phone, add it to the home screen.

| | |
| --- | --- |
| **Address** | **/my-work** on your site |
| **Permission** | Use the My Work portal; Request time off for the time off page |
| **Feature** | Workforce My Work Portal (it needs Workforce) |

<AskYourAdmin />

Every signed-in user can open My Work. A user sees their own schedule once a manager links their user account to their worker under **Workforce > Workers** (the **User** field). Until then the home page says so.

## Pages for workers

| Page | What it shows |
| --- | --- |
| **Home** | The time clock (when the worker clocks in), the next shift with its breaks and a countdown, what is on today, and the latest notifications. **Mark all read** clears the count in the menu. |
| **Schedule** | The worker's published shifts and approved time off for the next four weeks, by day. |
| **Timesheet** | The worker's hours for each pay period, with their punches, anything that looks off, a way to ask for a missed punch, and **Submit**. It appears when the [Time Clock](./time-clock.md) is on and their team has a time clock policy. |
| **Time off** | What is available under each policy, a form to ask for time off (with **Check first** to see what it counts and what would happen to it), and the worker's requests, which they can withdraw, or cancel once approved. It appears when [Time Off](./time-off.md) is on. |
| **Shifts** | Open shifts of the worker's teams they can **Claim** (or why they can't), shifts offered to them to **Accept** or **Turn down**, and their own shifts to **Trade**: give one to a colleague or anyone who can take it, or swap it for a colleague's shift. It appears when [Shift Planning](./shift-planning.md) is on. |
| **Availability** | When the worker can work, one line per day, such as *Monday 09:00-17:00*, *Tuesday 08:00-12:00 preferred* or *Saturday unavailable*. New schedules follow it. |
| **Settings** | Push notifications on this device, what the worker hears about and how, and their calendar feed. |

Claims and trades are checked as they are made, the same way as in the admin (see [Open shifts](./shift-planning.md#open-shifts) and [Shift trades](./shift-planning.md#shift-trades)), including the worker's [labor rules](./labor-rules.md).

## Notifications

Workers hear about:

- a schedule with their shifts being published, once per publish, and changes when it is published again;
- a shift of theirs added, moved, removed or given to someone else, outside a publish;
- an open shift they claimed being given to them;
- their time off being approved, declined or canceled;
- a trade offered to them, and their own trades being accepted, turned down or declined.

Every notification goes to the **My Work** inbox on the home page. Under **Settings > What I hear about**, a worker chooses which other ways to use (**Push notifications**, and **Email** when the site sends email) and which topics to hear about. Topics turned off still show in the inbox.

### Push notifications

Under **Settings**, choose **Turn on** and allow notifications when the browser asks. The device is listed below the button; **Turn off** removes it. A device that the push service says no longer exists is removed by itself. On an iPhone or iPad, add My Work to the home screen and open it from there first.

## Calendar feed

**Settings > My calendar app** shows a private address to subscribe to in Google Calendar, Outlook or a phone's calendar. The calendar shows the worker's shifts from four weeks ago to twelve weeks ahead, and their approved time off. Anyone with the address can see the schedule, so **Replace this address** makes a new one and stops the old one working.

## Pages for managers

A user who manages teams also sees:

| Page | What it shows |
| --- | --- |
| **Approvals** | Time off waiting for a decision, trades accepted and waiting for approval, open shifts with claims, submitted timesheets, punches workers asked for, and open attendance exceptions, each with buttons to decide them. **Details** and **Review** open them in the admin. |
| **Who's working** | Who is on shift now in the teams they manage, with their hours and whether they are on a break, and who is off. |

## Installing My Work

My Work works in any browser. To install it, open it and use the browser's **Install** or **Add to home screen** option. It then opens in its own window, like an app. Without a connection, it shows a page saying it needs one.
