---
sidebar_label: Garnishments
title: Garnishments
description: "Withhold child support, tax levies, student loans and creditors' judgments from pay within the limits the law sets, and send what was withheld to the payee by check or deposit."
technical_manual:
  - modules/payroll
---

A **garnishment order** tells an employer to withhold part of a person's pay and send it to someone else, such as child support to a state disbursement unit. Payroll withholds each order from every pay within the limit the law sets on **disposable earnings** (gross pay less the employee's taxes), and sends what was withheld to the payee when the pay statement is processed.

| | |
| --- | --- |
| **Menu** | Payroll > Garnishments |
| **Permission** | Manage garnishments (for every employer, or for one) |
| **Feature** | Garnishments |

<AskYourAdmin />

## Add an order

1. Open **Payroll > Garnishments** and choose **Add garnishment**.
2. Under **The order**, choose the **Person** (the person and the employer that withholds), the **Kind**, the **Case number** and the **Court or agency**, and the **Pay code** it shows as on statements (**GARN Garnishment** is added for you). Enter the day it was **Received** and the first pay date it applies to under **From**.
3. Under **How much**, enter the **Amount each pay**, a **Percent of disposable earnings**, or both, and the **Total owed** when the order has an end amount.
4. Leave the limits empty to take the kind's: a student loan 15%, a creditor 25% while leaving 217.50 a week. For support, check **Supports another spouse or child** and **Arrears over 12 weeks old** when they apply: the limit is 60%, 50% with another spouse or child, and 5 points more for old arrears (so 50, 55, 60 or 65). An order may ask for less, never more. A federal tax levy needs the **Exempt each pay** amount from its notice.
5. Under **The payee**, enter the payee's name and choose how it is paid: a **Check** (enter the mailing address), a **Deposit to the payee's bank account** (the account is added under **Banking** with the payee as its owner; a staff member's or a user's personal account is refused), or **Outside payroll** when you pay it another way, such as a state's portal.
6. Choose **Save**.

When a person has several orders, support is withheld first, then tax levies, bankruptcy, student loans and creditors; orders of the same kind by the day they were received. A creditor order gets only what is left of its limit after the orders before it.

## What happens in a pay run

The pay run withholds each order as a deduction line with its case number. If a limit holds an order back, the run shows a warning with what it asked and what it took. When the run is processed, a check or a deposit is made to each payee from the employer's funding account, and the order's page shows it under **Sent to the payee**. A check's voucher shows the case number, the court or agency, the person, the pay date and period and the amount, never the rest of the person's pay. Once the total owed is withheld, the order is **Completed**. Two runs never send more than the order still owes: anything withheld above it is flagged on the order to refund to the employee.

If a payment to the payee failed (for example, no funding account yet), fix the cause and choose **Send again** on the order's page.

If a pay statement is voided, its check or deposit is stopped if it has not gone out yet, and the withholding is given back to the order. If it went out already, the withholding stays on the order and the order's page says to ask the payee for a refund.

## Release or suspend an order

To stop an order, choose **Release** in its row's menu: nothing more is withheld, and what was withheld stays on the statements. An order that has withheld nothing yet can be deleted. To pause an order, set its **Status** to **Suspended**.

You can't manage your own garnishment order, unless your administrator allows changing one's own pay in **Settings > Payroll**. The **Garnishments** data set shows case numbers and courts only to people who manage the employer's garnishments.
