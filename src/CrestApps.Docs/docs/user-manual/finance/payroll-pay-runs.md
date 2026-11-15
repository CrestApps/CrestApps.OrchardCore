---
sidebar_label: Pay runs
title: Pay runs
description: "Add a pay run, enter one-time hours and amounts or a file of bonuses, calculate it, have someone else approve it, then process it to pay by deposit and check, and void or reissue a statement."
technical_manual:
  - modules/payroll
---

A **pay run** pays the people of one employer for one period. Payroll works out each person's statement from the hours the time clock locked, their salary and pay items, and the hours and amounts entered for the run; someone other than the person who prepared it approves it.

| | |
| --- | --- |
| **Menu** | Payroll > Pay Runs |
| **Permission** | Prepare pay runs, Approve pay runs, Process pay runs, Void pay statements, See pay runs (all employers, or one) |
| **Feature** | Payroll; Direct Deposit to pay to bank accounts; Paychecks to pay by check |

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

## Process and pay

Once the run is approved, someone who processes pay runs for the employer chooses **Process and pay**. Each statement becomes final and gets a number, pay items with a goal (such as a loan) move toward it and keep what a cut left owed, and the hours the time clock locked are marked as paid. Net pay is paid as each person's pay profile splits it:

- a deposit row becomes a payment in **Disbursements** from the employer's funding account (choose it on the employer under **Funding account**). Make the bank file there, as for any other payment;
- a check row becomes a check in the **Check register**, on the same account, with the statement printed as the stub of the check. Print it there.

A deposit is paid by check when Direct Deposit is off and Paychecks is on. When a payment can't be made, such as an employer with no funding account, the run is **partly paid** and the statement shows why: fix it and choose **Pay what is left**.

Each person whose staff record has a user is told their statement is ready, by the notification center and the methods they chose (it holds no amount). **Download the stub** on a statement's page gives its PDF.

## Void or reissue

A final statement never changes. On its page, someone who voids pay statements for the employer can:

- **Reissue** a payment, such as a lost check: say why, and the old check is voided (or the deposit cancelled if it was not sent to the bank yet) and a new one is made;
- **Void** the statement: say why, and a reversing statement with the opposite amounts cancels it in the year's totals, its pay items go back, its time clock hours can be paid again and its checks and deposits are stopped where they have not gone out. Pay what is owed in a new run.

## Reports, automation and history

- **Reports > Workforce & Payroll** has the Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution and Year-to-date Earnings reports; export them or e-mail them on a schedule. The Payroll Cash Requirement and Payroll Summary widgets can go on the dashboard.
- Data pipelines read the **Payroll** data source (pay runs, statements, earnings, deductions, taxes, contributions, payments, year-to-date balances), for example to send a file to an accounting system.
- The **Automation: open and calculate pay runs on schedule** recipe (Tools > Recipes) adds a workflow, turned off, that opens and calculates the period's run every weekday morning and submits it for approval; set its employer and pay schedule and turn it on.
- Pay dates show on the calendar: the runs of your employers, and each person's own pay dates on their calendar.
- To start the year's totals from another system, import its pay statements with the **Pay history** step of a recipe; they land in historical pay runs that can't be changed.

