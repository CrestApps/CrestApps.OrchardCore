---
sidebar_label: Income tax
title: Income tax rules, country packs and the pay preview
description: "Set up the taxes on income that payroll withholds and pays, for any country and at every level, with dated income tax rules; add a country's rules and forms with its pack (United States, Canada); choose the tax provider; and preview what a person will be paid."
technical_manual:
  - modules/taxation
  - modules/payroll
---

Taxes on income are set up in **Taxation**, not in Payroll: income tax withheld at every level (federal, state or province, local) and the contributions taken from earnings, such as US Social Security, Medicare and unemployment, or the Canada Pension Plan and Employment Insurance. Payroll asks a **tax provider** to work them out for each pay. The built-in one uses the **income tax rules** on this page; a module for a commercial tax service can replace it.

| | |
| --- | --- |
| **Menu** | Taxes > Income tax rules, Taxes > Tables, Settings > Payroll, and **Preview the next pay** on a pay profile |
| **Permission** | Manage income tax rules for the rules; See pay profiles for the preview |
| **Feature** | Income Tax (turns on Tax Tables); a country pack such as United States Income Tax or Canada Income Tax |

<AskYourAdmin />

## Add an income tax rule

1. Open **Taxes > Income tax rules** and choose **Add income tax rule**.
2. Enter the **Name** shown on pay statements, a **Tax code** that stays the same for every version of the tax (such as US-SS or CA-CPP), and the **Jurisdiction** (such as US, US-CA, CA or CA-ON). The tax applies to employers registered in the jurisdiction; one below a country (with a dash) applies only to people who live or work there.
3. When another agency collects the tax, enter it under **Collected by**: a province's tax that the CRA collects is collected by CA. Under **Not for people in**, list the jurisdictions the tax doesn't apply to, such as CA-QC for the Canada Pension Plan.
4. Choose the **Income taxed** (income tax, pensionable earnings, insurable earnings, or one a country pack adds), the **Kinds of income** it is for when it isn't employment income, and who pays it: the earner or the payer.
5. Choose the **Method**: a rate, a fixed amount, a table, brackets, a threshold, or a country's withholding formulas (such as **United States Form W-4** or **Canada federal**). A country's formulas ask for their tables and amounts in a card of their own. Enter the **Rate (%)** or the **Fixed amount**, or choose the **Tax table** it needs. Tables are under **Taxes > Tables**, with rates as fractions (0.1 for 10%).
6. Set the limits the tax has:
   - **Wage base**: the most income taxed in a year.
   - **Wage threshold**: only a year's income above it is taxed, such as an additional rate on high earners or a pension plan's second tier.
   - **Yearly exemption**: an amount never taxed, taken off each pay in proportion to the pay periods of a year, such as the 3,500 basic exemption of the Canada Pension Plan.
   - **Yearly maximum**: the most tax taken in a year, such as a contribution's maximum.
7. For a payer's share of a contribution, fill in **Share of another tax**: the multiple and the tax code it is a share of, such as 1.4 times CA-EI for the employer's EI premium. The method above is then not used.
8. Check **Work it out on a year's income** for income tax tables written for a year, and **The person's election applies** with the amount **Each allowance** lowers a year's income by.
9. Enter a **Supplemental rate (%)** to withhold bonuses and commissions at a flat rate, and a **Supplemental rate over a year's amount** when a higher rate applies to a year's supplemental pay above an amount whatever the method.
10. Enter the first pay date it applies to under **From**, and choose **Save**.

When a rate changes, give the current version a last day under **To** and add a new version with the same tax code from the next day. Two versions of one tax can't be in effect on the same day.

## Country packs

A country pack adds that country's election forms and withholding formulas, and ships its rules for a year as a recipe under **Tools > Recipes**. Each new year comes as a new recipe: check its amounts against the agency's publications before the first pay date of the year.

### United States

1. Turn on **United States Income Tax** and run **United States Income Tax: federal rules for 2026** from **Tools > Recipes**. It adds the federal income tax, Social Security, Medicare, Additional Medicare and federal unemployment rules, backup withholding on contractors, and the six withholding tables for pay dates in 2026.
2. On each employer, add a registration for the jurisdiction **US**.
3. On each pay profile, add a **Tax election** for **US** from the person's Form W-4: the form's year, the filing status, the Step 2 box, the amounts of Steps 3 and 4 (a year, except extra withholding, which is each pay period), or for a form from 2019 or earlier the allowances. A person with no election is withheld as single with no adjustments.

A bonus paid on its own in a period is withheld on the period's total pay less what was already withheld on, unless the run uses the flat method (22%). Supplemental pay over 1,000,000 in a year is always withheld at 37%. State and local taxes are added as income tax rules by hand. The federal returns and W-2s come with **United States Payroll Filings**: see [Liabilities and filings](./payroll-filings.md).

### Canada

Turn on **Canada Income Tax** and run **Canada Income Tax: 2026 rules**. It adds federal tax, the tax of every province and territory, Quebec tax, CPP and CPP2, EI, QPP, QPP2 and QPIP. See [Canadian payroll](./canada-payroll.md) for the registrations, the TD1 claims, the remittances and the T4 and RL-1 slips.

## Choose the provider and the least net pay

Under **Settings > Payroll**, **Tax provider** says what works out taxes when there is more than one. **Least net pay** keeps net pay from falling below an amount: deductions taken after tax are cut, the largest priority number first.

## Preview what a person will be paid

On a pay profile, choose **Preview the next pay**. The page works out the current period of the person's pay schedule from the hours and amounts there are now, the pay items and the income tax rules. It shows each line, the taxes, net pay and how it is paid, with any warning (such as a deduction cut because pay was too small). Nothing is kept.
