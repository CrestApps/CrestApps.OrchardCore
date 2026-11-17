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
| **Feature** | Payroll; Banking's Direct Deposit to pay to bank accounts, and its Checks to pay by check |

<AskYourAdmin />

## Try it with the sample

To try a pay run without setting everything up, run the **Payroll sample** recipe under **Configuration > Recipes**. It turns on Payroll and Banking, adds the 2026 federal income tax rules, and sets up the employer Contoso (with its bank account, a check layout and a pay schedule every two weeks) and four staff members with pay profiles: two on a salary paid by deposit, one paid by the hour, and one paid by check. One of them saves in a 401(k) and has a child support order. Every tax id and account number in it is made up.

Then add a pay run for Contoso and leave the period empty to take the current one. Jordan Smith is paid by the hour, so enter Jordan's hours as inputs of the run, such as 80 hours of **REG** and 4 of **OT**, and choose **Calculate**.

## Add a pay run

1. Open **Payroll > Pay Runs** and choose **Add pay run**.
2. Choose the **Employer** and the **Pay schedule**: everyone of the employer with an active pay profile on the schedule is paid.
3. Choose the **Type**: a regular run pays salaries, hours, pay items and inputs; a supplemental or off-cycle run pays only the inputs you enter, such as bonuses. Choose how bonuses are withheld.
4. Leave the period and pay date empty to take the schedule's current period, or enter them, and choose **Add**.

## Enter hours and amounts

Under **Inputs of this run**, choose the person and the pay code and enter hours or an amount, such as a bonus. Hours locked in the time clock are read when the run is calculated, so they need no input. You can't enter your own pay.

To enter many at once, such as a file of bonuses, use a data pipeline: read the file with **Read a media file**, rename its columns to EmployeeNumber, PayCode and Amount (or Hours) with **Select fields** if needed, add a PayRun column with the run's number, and connect **Save pay inputs**. Rows that can't be saved, such as an unknown employee number or a row of your own pay, go to its **Rejected** output with the reason: write them to a file to fix them. Running the pipeline again doesn't add a row twice: a row with the same values, or with the same value in a **Key** column, updates the input it made.

## Calculate and review

Choose **Calculate**. The page shows the totals (gross, taxes, deductions, net pay and the cash the employer needs), anything that blocks the run (such as a person with no tax election), and the register with a statement per person. Open a statement to see its lines. Fix a blocker, then calculate again; a changed input is calculated again for that person on its own.

## Approve

Choose **Submit for approval**. People who approve pay runs for the employer see **Approve** and **Reject** on the run's page; the person who prepared the run can't approve it unless **Settings > Payroll** allows it. Any change after the approval, such as a new input, returns the run to calculated: submit it again.

**Cancel the run** removes its statements; a cancelled run can't be calculated again.

## Process and pay

Once the run is approved, someone who processes pay runs for the employer chooses **Process and pay**. Each statement becomes final and gets a number, pay items with a goal (such as a loan) move toward it and keep what a cut left owed, and the hours the time clock locked are marked as paid. Net pay is paid as each person's pay profile splits it:

- a deposit row becomes a payment in **Disbursements** from the employer's funding account (choose it on the employer under **Funding account**). Make the bank file there, as for any other payment;
- a check row becomes a check in the **Check register**, on the same account, with the statement printed as the stub of the check. Print it there.

A deposit is paid by check when Banking's Direct Deposit is off and its Checks is on. Payroll has nothing of its own to turn on for either. When a payment can't be made, such as an employer with no funding account, the run is **partly paid** and the statement shows why: fix it and choose **Pay what is left**.

Each person whose staff record has a user is told their statement is ready, by the notification center and the methods they chose (it holds no amount). **Download the stub** on a statement's page gives its PDF.

## Void or reissue

A final statement never changes. On its page, someone who voids pay statements for the employer can:

- **Reissue** a payment, such as a lost check: say why, and the old check is voided (or the deposit cancelled if it was not sent to the bank yet) and a new one is made;
- **Void** the statement: say why, and a reversing statement with the opposite amounts cancels it in the year's totals, its pay items go back, its time clock hours can be paid again and its checks and deposits are stopped where they have not gone out. Pay what is owed in a new run.

## Take-home amounts, earlier weeks and retro pay

- To pay a bonus as a take-home amount, open **A take-home amount, or an amount earned in earlier weeks** under the inputs, check **The amount is what the person takes home** and enter the amount: the run works out the gross so the taxes are paid on top. One such amount a person a run.
- For a bonus or commission earned in weeks already paid, enter **Earned from** and **Earned to**: the run also pays the overtime of those weeks again on the higher regular rate the amount gives.
- When a pay rate or salary is raised back to a day already paid, the next regular run pays the difference for each earlier statement of the year as **retro pay**, once.
- For a person marked **Paid a tipped wage** on their pay profile, a week whose cash wage and tips fall short of the minimum wage set under **Settings > Payroll** is made up with a **Tip credit make-up** line. Cash tips the person kept are entered on the **CTIPS** pay code: they are taxed but not paid.
- A pay schedule says how a salary is paid in a year with an extra pay date (27 every two weeks): the usual amount each time, or the year's salary divided by that year's pay dates.

## Reports, automation and history

- **Reports > Workforce & Payroll** has the Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution and Year-to-date Earnings reports; export them or e-mail them on a schedule (an export or an attachment needs **Export payroll data**; a subscription stops sending once the person who saved it may no longer view the report). The Payroll Cash Requirement and Payroll Summary widgets can go on the dashboard.
- Data pipelines read the **Payroll** data source (pay runs, statements, earnings, deductions, taxes, contributions, payments, year-to-date balances) for people who may view payroll reports, for example to send a file to an accounting system.
- **From other modules.** A regular run also takes, for its people and period: time off paid out on a balance (Workforce Time Off: the hours on the time-off type's pay code), tips cashiers declared when closing a register shift (Point of Sale: tips added on card are paid, cash tips kept are reported for tax), and commission on the orders a staff member made (Orders: their **Commission on sales** percent, set on the staff member's Pay card, of what the order came to after discounts and refunds). Each is paid once; voiding the statement gives it back. Statements show time-off balances as of the pay date.
- **Employee review.** With **Employees review their coming pay before the run is submitted** (Settings > Payroll), each employee with a user is told to look at their pay in My pay once the run is calculated. The register shows each answer (**Employee says it's right**, **Employee asked about it**, or **Pay changed since review**), the run lists open questions, and **Submit for approval** asks you to confirm while one is open.
- **AI assistant.** With **Payroll AI Tools** turned on (Tools > Features) and an AI profile that uses them, people can ask an assistant to explain their own pay statement, and payroll staff to say what changed in a pay run since the last one; each answer shows only what the person may see on the pages. Turn it on only if pay may go to your AI provider.
- **Payroll journal.** Under **Settings > Payroll**, **Journal accounts**, enter the ledger account of each kind of amount (net pay owed, wages expense, deductions payable, taxes payable and so on) and, if a pay code or a tax goes elsewhere, a row with its code and its accounts. Then run the **Payroll: export the payroll journal to a CSV file** recipe (Tools > Recipes): it adds a draft data pipeline that writes the journal, a row per pay date, account and department with the debit and the credit. Add how the file reaches your accounting system, publish it and schedule it. A voided statement reverses its entries.
- The **Automation: open and calculate pay runs on schedule** recipe (Tools > Recipes) adds a workflow, turned off, that opens and calculates the period's run every weekday morning and submits it for approval; set its employer and pay schedule and turn it on. Workflows can also start when a run is opened, calculated, submitted, approved, rejected, processed or cancelled.
- Pay dates show on the calendar: the runs of your employers, and each person's own pay dates on their calendar.
- To start the year's totals from another system, import its pay statements with the **Pay history** step of a recipe; they land in historical pay runs that can't be changed.

