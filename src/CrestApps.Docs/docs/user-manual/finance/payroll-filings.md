---
sidebar_label: Liabilities and filings
title: Payroll tax liabilities, filings and year end
description: "See the payroll taxes owed by deposit period, record the deposits you make, prepare the quarterly and annual returns and the wage statements from the final pay statements, mark them filed or amend them, and close a year."
technical_manual:
  - modules/payroll
---

Payroll works out what you owe and prepares the forms; you deposit the money and file the forms with the agencies, then record what you did here.

| | |
| --- | --- |
| **Menu** | Payroll > Liabilities and Filings |
| **Permission** | Manage payroll filings (for every employer, or for one) |
| **Feature** | Payroll; the United States forms come with United States Payroll |

<AskYourAdmin />

## Liabilities and deposits

1. Open **Payroll > Liabilities and Filings > Liabilities**, choose the employer and the year, and choose **Show**.
2. Each row is a deposit period of a jurisdiction: what the employees' and the employer's taxes owe, what you deposited, and what is left with its due date. Withholding follows the **Deposit frequency** of the employer's registration (monthly, semi-weekly, quarterly or yearly); unemployment taxes are by quarter.
3. After you make a deposit, enter it under **Record a deposit**: the jurisdiction, the taxes, the period's first and last days as shown in the list, the amount, the day you paid and the confirmation number. The row shows it as paid once the deposits cover it.

A deposit counted in a filed return can't be deleted; amend the return instead.

## Prepare and file a form

1. Open **Filings**, choose the employer and choose **Show**.
2. Under **Prepare a filing**, choose the form, the year and, for a quarterly form, the quarter, then choose **Prepare**. The totals come from the final pay statements with pay dates in the period and the deposits you recorded.
3. Check the boxes on the filing's page. For wage statements, each person's statement is listed with a **PDF** to print or check. Correct the pay if needed and choose **Prepare again**.
4. File the form with the agency, then enter the day and the confirmation number and choose **Mark as filed**. A filed form never changes, and each person with a statement in it is told it is ready under **My pay**.

To correct a filed form, open it and choose **Amend**: a new draft is prepared from the statements as they are now. File it, then mark it as filed; the first one is then marked amended.

For the Social Security Administration's wage report, run **United States Payroll: W-2 wage records as an SSA EFW2 file** from **Tools > Recipes**, then publish the pipeline it adds with a delivery step that keeps the file private, such as an SFTP server. Add a registration to the employer with the agency **SSA** and your SSA user id as its account id. Only a user who may see full tax ids gets the social security numbers in the file, and each one read is recorded.

## Contractors and Form 1099-NEC

Pay contractors with a pay run of type **Contractors** and an amount for each invoice; their profile's worker type is **Contractor**. Nothing is withheld, unless the contractor has no tax id on file or you checked **Withhold on a contractor's payments** on their profile because the IRS told you to: then backup withholding (24%) is taken. At year end, prepare **Forms 1099-NEC and 1096** like the wage statements: each contractor paid at least 2,000 in the year (600 before 2026), or withheld on, has a 1099-NEC with a PDF.

## Close a year

1. Open **Year end**, choose the employer and the year, and choose **Show**.
2. The page lists any pay run of the year that is not paid or cancelled; process or cancel each one.
3. Choose **Close the year**. The year's wage statements are prepared under **Filings**, and no pay run with a pay date in the year can be opened, calculated or processed any more, nor any statement of it voided. Correct a closed year's pay in the next year's run and amend the wage statement. **Reopen the year** undoes the close.
