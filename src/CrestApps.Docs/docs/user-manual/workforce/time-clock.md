---
sidebar_label: Time Clock
title: Time Clock
description: "Clock in and out from My Work or a kiosk, set how time is rounded and paid with time clock policies, pay periods and pay codes, approve timesheets, handle attendance exceptions and points, and export locked timesheets for payroll."
technical_manual:
  - modules/workforce
---

The time clock records when workers start and stop work, turns their punches into **timesheets**, and exports the hours for payroll. Workers clock in from **My Work** in a browser or the installed app, or on a shared **kiosk** with a badge number and PIN. A **time clock policy** sets how time is rounded, what counts as late, when meals come off and when overtime starts. Late starts, missed punches and no-shows become **attendance exceptions**, which can earn points.

| | |
| --- | --- |
| **Menu** | Workforce > Timesheets, Attendance, Payroll Export, Time Clock Policies, Pay Periods, Pay Codes, Time Clock Kiosks |
| **Permission** | Manage time clock policies, Approve timesheets, Edit punches, Export payroll, View pay rates, Clock in and out |
| **Feature** | Workforce Time Clock (it needs Workforce) |

<AskYourAdmin />

## Setting it up

1. Under **Workforce > Pay Periods**, choose **Add pay periods**: how often workers are paid (every week, every two weeks, twice a month or every month), the first day of any one period for weekly and two-weekly pay, and the day the overtime week starts. The editor shows the next periods.
2. Under **Workforce > Pay Codes**, check the codes. Regular, overtime, double time, paid time off, predictability pay, holiday and unpaid codes are added when the feature is turned on. Give a code a **payroll code** when your payroll system calls it something else.
3. Under **Workforce > Time Clock Policies**, choose **Add policy** (see below).
4. Under **Workforce > Teams**, give the team its **time clock policy**. Sub-teams use their parent's. A team with no policy, and no parent with one, doesn't clock time.
5. For kiosks, give each worker an **employee number** (their badge number) and a PIN under **Workers > Actions > Time clock**.

## Time clock policies

| Field | What it does |
| --- | --- |
| **Pay periods** | The periods the team's timesheets cover. |
| **Round punch times to** | Not at all, or to 1, 5, 6 or 15 minutes; to the nearest step, or with starts rounded down and ends rounded up so the worker never loses time. |
| **Early grace** | Clocking in this early, or out this late, is paid from the shift's start or to its end. |
| **Late grace** | Clocking in this late, or out this early, is paid as on time and isn't an exception. |
| **Workers can punch from** | The browser, the My Work app, kiosks, the contact center and other systems through the API. Tick none to allow all. Managers can always add and correct punches. |
| **Earliest clock-in** | Optional. Clocking in more than this long before a shift is refused. |
| **Workers clock in at the work site** | Punches from the browser or the app must share a location. A punch farther from the team's **site coordinates** than the **site radius** is recorded and raised as *away from the site*. |
| **A forgotten clock-out** | Isn't paid until a manager fixes it, or is paid to the end of the shift. Either way it is raised as a missed punch. |
| **Take a meal off after** | A day longer than this with no meal punched has the **meal taken off**. |
| **Longest meal** | A longer meal is a *long meal*. |
| **Overtime** | Hours past the weekly limit, or past the daily limit, are overtime; hours past the double-time limit are double time. |
| **Attendance points** | The points each kind of exception earns, how many days they count, and the points at which the worker's line manager is told in My Work. |

Site coordinates are set on the team, under **Workforce > Teams**, when the time clock is on.

:::info[Overtime comes from the policy]
The time clock pays overtime from its policy's limits. The labor rules' Overtime rule warns when a schedule plans too many hours; keep the two in step.
:::

## Clocking in

In **My Work**, the home page shows a **Time clock** card: whether the worker is clocked in, on a meal or on a break, since when, and buttons for what they can do next (**Clock in**, **Start meal**, **End meal**, **Start break**, **End break**, **Clock out**). Punches follow each other: a worker can't clock in twice or end a meal they didn't start. A worker who forgot to clock out can clock in again the next day; the forgotten punch is raised as missed.

### Kiosks

Under **Workforce > Time Clock Kiosks**, choose **Add kiosk**, name it, and optionally limit it to a team. After saving, the kiosk's **private address** is shown once: open it on the device and leave it open. Workers enter their badge number and PIN, then choose what to punch. After five wrong PINs in a row, the kiosk refuses that worker's PIN for 15 minutes. **Give it a new address** when the address is lost or seen by someone it shouldn't be; the old one stops working.

## Timesheets

Each worker has a timesheet for each pay period. It is worked out from their punches:

- punches pair into work, paid breaks and unpaid meals;
- grace and rounding apply;
- days split at midnight in the worker's time zone, so a night shift is paid on both days, and the nights the clocks change are paid the hours really worked;
- hours are regular, overtime or double time by the policy's limits;
- approved time off is added under its type's pay code (or paid time off), and approved predictability pay under the predictability pay code;
- shift differentials add a premium for the hours in their window.

Workers see theirs under **My Work > Timesheet**: the hours each day, their punches, anything that looks off, and the periods before. **Forgot to punch?** asks the manager for a punch they missed. **Submit** sends the timesheet with their confirmation that the hours are right.

Managers open **Workforce > Timesheets**, choose the pay period, and open a timesheet. It shows each day's punches, hours and exceptions, the totals by pay code, and the punches the worker asked for. From there a manager can:

- **Approve** the timesheet, **Send back** a submitted one with what to fix, or **Reopen** an approved one;
- **Add a punch**, or **change or remove** one, with a reason. The original stays in the history;
- **Add hours by hand**, such as holiday hours or a call-back;
- add or decline a punch the worker asked for.

Several timesheets can be approved at once from the list. **Make this period's timesheets** makes the timesheets of every worker of your teams with a policy; others appear when their worker punches, has a shift, or opens them. Timesheets of the last day and a half are worked out every hour, so missed punches and no-shows show up on their own.

**Lock** the approved timesheets of a period once it has ended. Locked timesheets go to payroll and no longer change; add any later change to the next period.

## Attendance

**Workforce > Attendance** lists the exceptions found against the published schedule:

| Exception | When |
| --- | --- |
| **Late start** | Clocked in after the shift started, past the grace. |
| **Early finish** | Clocked out before the shift ended, past the grace. |
| **Missed punch** | Clocked in and never out. |
| **No-show** | A shift with no punches and no approved time off. |
| **Long meal** | A meal longer than the policy allows. |
| **Away from the site** | A punch farther from the site than the radius. |

**Confirm** an exception, or **Excuse** it so it earns no points. When a missed punch is fixed, its exception goes away. Managers can also do this under **Approvals** in My Work. A worker's points are shown under **Workers > Actions > Time clock**.

## Payroll export

Under **Workforce > Payroll Export**, choose the pay period. The counts show how many timesheets are open, submitted, approved and locked. **Export locked timesheets** writes a CSV file with a row per worker and pay code, which you download from the list of exports. Each timesheet is exported once; tick **Export again the ones already exported** to include them again.

**Choose the CSV columns** sets the file's columns and headings: payroll id, employee number, name, pay code, day, hours, rate, amount, period, activity and job. A day, activity or job column gives a row per day, activity or job. Rates come from the worker's **pay rates** under **Workers > Actions > Time clock** (people with the View pay rates permission see them): a rate an hour from a day on, for all work or one activity. The amount is the hours times the rate and the code's multiplier, or the hours times a differential's premium.

## Pay codes

| Field | What it does |
| --- | --- |
| **Code** | Short and unique, such as REG. |
| **Pays for** | Regular hours, overtime, double time, holiday hours, paid time off, predictability pay, call back, a shift differential, or unpaid time. |
| **Multiplier** | What the rate is multiplied by, such as 1.5 for overtime. |
| **Premium an hour**, **Hours**, **On** | For a shift differential: the premium, the window in the worker's time (an end before the start runs past midnight), and the days it starts on. |
| **Payroll code** | What payroll calls it, when that differs. |
