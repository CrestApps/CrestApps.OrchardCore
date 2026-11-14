---
sidebar_label: Pay schedules and pay codes
title: Pay schedules and pay codes
description: "Set how often people are paid and when, with the overtime week and pay dates, and the pay codes hours and amounts are paid under, shared by the time clock and payroll."
technical_manual:
  - modules/payroll
---

A **pay schedule** says how often people are paid and on which day. A **pay code** names what a line of pay is for: regular hours, overtime, a bonus, a deduction. The time clock and payroll use the same schedules and codes.

| | |
| --- | --- |
| **Menu** | Payroll > Setup > Pay Schedules, Payroll > Setup > Pay Codes |
| **Permission** | Manage pay setup |
| **Feature** | Pay Setup (turned on with the Workforce Time Clock or Payroll) |

<AskYourAdmin />

## Add a pay schedule

1. Open **Payroll > Setup > Pay Schedules** and choose **Add pay schedule**.
2. Give it a **Name** and choose its **Periods**: every week, every two weeks, every four weeks, twice a month, every month, every quarter or every year.
3. For weekly, every two weeks and every four weeks, set **A period starts on** to any day a period starts. For twice a month, set the day the **second period starts on** (the 16th unless you change it).
4. Set the day the **overtime week starts on**.
5. Under **Pay date**, choose whether people are paid **a number of days after the period ends**, or **on fixed days of the month** (monthly and twice-a-month schedules; 0 is the last day of the month). Choose what happens when that day is a weekend: the business day before, the business day after, or keep the day.
6. Set **Approve by**: how many days before the pay date a pay run must be approved.
7. Choose **Save**. Open the schedule again to see its next four periods and their pay dates.

Changing a schedule doesn't change timesheets or pay runs already made.

## Pay codes

When the time clock or payroll is first turned on, the usual codes are added: regular, overtime, double time, paid time off, predictability pay, holiday, weekend and unpaid.

| Field | What it does |
| --- | --- |
| **Code** | Short and unique, such as REG. |
| **Class** | Earning, deduction, employer contribution, imputed income (taxed, not paid), reimbursement (paid, not taxed), or memo (shown only). |
| **Pays for** | Regular hours, overtime, double time, holiday hours, weekend hours, paid time off, predictability pay, call back, a shift differential, unpaid time, salary, bonus, commission or tips. |
| **Multiplier** | What the hourly rate is multiplied by, such as 1.5 for overtime. Type a dot for decimals. |
| **Premium an hour** | Paid on top of the hours, such as for a shift differential. |
| **Export code** | What an outside payroll system calls it, when that differs from the code. |
| **Shift differential hours** | With the time clock: the hours a shift differential applies in, in the worker's time (an end before the start runs past midnight), and the days it starts on. |

## Turn off instead of delete

A schedule a time clock policy or a timesheet uses, or a code a time off type or a timesheet uses, can't be deleted: the page says what uses it. Clear **In use** to stop choosing it.

## Moving from the time clock's pay periods

Sites that used **Workforce > Time Management > Pay Periods** and **Pay Codes** find the same records, with the same settings, under **Payroll > Setup** after the update. Everyone who could manage time clock policies can manage them there.
