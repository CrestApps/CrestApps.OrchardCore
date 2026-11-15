---
sidebar_label: Pay runs
title: Pay runs
description: "Add a pay run, enter one-time hours and amounts or a file of bonuses, calculate it, review the register, and have someone else approve it."
technical_manual:
  - modules/payroll
---

A **pay run** pays the people of one employer for one period. Payroll works out each person's statement from the hours the time clock locked, their salary and pay items, and the hours and amounts entered for the run; someone other than the person who prepared it approves it.

| | |
| --- | --- |
| **Menu** | Payroll > Pay Runs |
| **Permission** | Prepare pay runs, Approve pay runs, See pay runs (all employers, or one) |
| **Feature** | Payroll |

<AskYourAdmin />

## Add a pay run

1. Open **Payroll > Pay Runs** and choose **Add pay run**.
2. Choose the **Employer** and the **Pay schedule**: everyone of the employer with an active pay profile on the schedule is paid.
3. Choose the **Type**: a regular run pays salaries, hours, pay items and inputs; a supplemental or off-cycle run pays only the inputs you enter, such as bonuses. Choose how bonuses are withheld.
4. Leave the period and pay date empty to take the schedule's current period, or enter them, and choose **Add**.

## Enter hours and amounts

Under **Inputs of this run**, choose the person and the pay code and enter hours or an amount, such as a bonus. Hours locked in the time clock are read when the run is calculated, so they need no input. You can't enter your own pay.

To enter many at once, such as a file of bonuses, use a data pipeline: read the file with **Read a media file**, rename its columns to EmployeeNumber, PayCode and Amount (or Hours) with **Select fields** if needed, add a PayRun column with the run's number, and connect **Save pay inputs**. Rows that can't be saved, such as an unknown employee number, go to its **Rejected** output with the reason: write them to a file to fix them.

## Calculate and review

Choose **Calculate**. The page shows the totals (gross, taxes, deductions, net pay and the cash the employer needs), anything that blocks the run (such as a person with no tax election), and the register with a statement per person. Open a statement to see its lines. Fix a blocker, then calculate again; a changed input is calculated again for that person on its own.

## Approve

Choose **Submit for approval**. People who approve pay runs for the employer see **Approve** and **Reject** on the run's page; the person who prepared the run can't approve it unless **Settings > Payroll** allows it. Any change after the approval, such as a new input, returns the run to calculated: submit it again.

**Cancel the run** removes its statements; a cancelled run can't be calculated again.
