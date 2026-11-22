---
sidebar_label: Employers and payroll access
title: Employers and payroll access
description: "Add the employers that pay people, with their tax registrations and the bank account they pay from, give each team a ledger code and a work site's jurisdictions, and give payroll staff access to one employer or all of them."
technical_manual:
  - modules/payroll
---

An **employer** is the legal entity that pays people. A business with one company has one; a group with several companies has one per company, and payroll staff can be limited to the ones they work for.

| | |
| --- | --- |
| **Menu** | Payroll > Setup > Employers, Settings > Payroll |
| **Permission** | Manage payroll settings and employers (all employers, or one) |
| **Feature** | Payroll |

<AskYourAdmin />

## Add an employer

1. Open **Payroll > Setup > Employers** and choose **Add employer**. Only people who manage payroll settings for every employer can add one.
2. Enter the **Name**, and the **Legal name** printed on statements, checks and forms when it differs.
3. Enter the **Employer tax id**, the **Country pack** (such as US) and the **Currency**. A new employer starts with the country pack and currency of **Settings > Payroll**.
4. Under **Signatory and contact**, enter who signs checks and who to contact about payroll.
5. Under **Tax registrations**, add a row for each tax agency account: the jurisdiction (such as US or US-CA), the agency, the account id, how often taxes are deposited, and a rate the agency assigned with the day it starts. When the agency changes the rate, add a row with the same jurisdiction, agency and account and the new rate's first day. When you have two accounts in one jurisdiction, such as a state's income tax withholding account and its unemployment insurance account, enter under **Taxes** what each covers (IncomeTax, Unemployment), so the unemployment rate is read from the right account and the W-2 shows the withholding account. Leave a row empty to remove it.
6. Under **Where people work**, enter the tax jurisdictions of your place of business, separated with commas, such as US-MI, US-MI-DETROIT. People whose pay profile names no work site, or a work site with no jurisdictions, are taxed as working there. A business with one location doesn't need teams for this.
7. Enter the **Address** printed on statements and checks, and choose **Save**.
8. With Banking on, choose the bank account pay leaves from under **Funding account** > **Pay from**: one of the business's own accounts, added under **Banking > Bank Accounts** with no owner (an employer with its own bank and company id gets its own account, with its own bank file settings).

An employer that no longer pays anyone can't be deleted while it is in use: clear **Runs payroll** instead.

## Teams: ledger codes and work sites

People who manage payroll settings see a **Payroll** card on each team:

- **Ledger code**: the cost center the team's pay costs go to.
- **Tax jurisdictions**: for a work site, where the people who work there are taxed for that work, separated with commas.

## Give payroll staff access

Every payroll permission, except seeing your own pay and changing your own pay details, exists twice in the role editor: once for every employer (under **Payroll**), and once per employer (under **Payroll: Contoso**, for example).

- A payroll administrator of the whole group gets the permissions under **Payroll**.
- A payroll clerk of one company gets that company's permissions only, such as **Prepare pay runs for Contoso**. They see only that employer in the employer list and in payroll data, and every page of another employer is refused.

Run **Payroll: sample roles** under **Configuration > Recipes** to add three roles to start from: Payroll administrator, Payroll clerk (never approves) and Payroll approver. They cover every employer; copy one and pick the per-employer permissions instead for a clerk of one company.

To see a complete employer before you add your own, run the **Payroll sample** recipe: it adds Contoso with its registrations, funding account, check layout, pay schedule and four people to pay (see [Pay runs](./payroll-pay-runs.md#try-it-with-the-sample)).

Data pipelines read data with the access of the person who published them, so a pipeline a clerk of Contoso publishes reads only Contoso's people. The Data Pipelines pages themselves are for every employer: give **Manage data pipelines** only to people who may see all payroll.

## Settings

Under **Settings > Payroll**:

- **The person who prepared a pay run may approve it**: off by default. Turn it on only when one person runs payroll.
- **People may change their own pay in the admin pages**: off by default.
- **A new employer**: the country pack and currency a new employer starts with.
