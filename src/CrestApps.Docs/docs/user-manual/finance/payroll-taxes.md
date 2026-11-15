---
sidebar_label: Payroll taxes
title: Payroll taxes and the pay preview
description: "Set up the taxes payroll withholds and pays with tax rules for each jurisdiction, choose the tax provider, and preview what a person will be paid."
technical_manual:
  - modules/payroll
  - modules/taxation
---

Payroll works out taxes through a **tax provider**. The built-in one, **Payroll Tax Tables**, uses tax rules you set up for each jurisdiction; a module for a commercial tax service can replace it.

| | |
| --- | --- |
| **Menu** | Payroll > Setup > Tax rules, Taxes > Tables, Settings > Payroll, and **Preview the next pay** on a pay profile |
| **Permission** | Manage payroll settings and employers (for every employer) for tax rules; See pay profiles for the preview |
| **Feature** | Payroll Tax Tables (turns on Tax Tables) |

<AskYourAdmin />

## Add a tax rule

1. Open **Payroll > Setup > Tax rules** and choose **Add tax rule**.
2. Enter the **Name** shown on pay statements, a **Tax code** that stays the same for every version of the tax (such as US-SS), and the **Jurisdiction** (such as US or US-CA). The tax applies to employers registered in the jurisdiction; one below a country applies only to people who live or work there.
3. Choose the **Wages taxed** (income tax, social insurance, unemployment insurance, or one a country pack adds) and who pays it: the employee or the employer.
4. Choose the **Method**: a rate, a fixed amount, a table, brackets, a threshold, or a country's withholding form (such as **United States Form W-4**), which then asks for its tables and amounts in a card of its own. Enter the **Rate (%)**, the **Fixed amount** or choose the **Tax table** it needs. Tables are under **Taxes > Tables**, with rates as fractions (0.1 for 10%).
5. Optionally enter a **Wage base** (the most wages taxed in a year), a **Wage threshold** (only a year's wages above it are taxed, such as an additional rate on high earners), check **Work it out on a year's wages** for income tax tables written for a year, and **The person's election applies** with the amount **Each allowance** lowers a year's wages by.
6. Enter a **Supplemental rate (%)** to withhold bonuses and commissions at a flat rate, and a **Supplemental rate over a year's amount** when a higher rate applies to a year's supplemental pay above an amount whatever the method.
7. Enter the first pay date it applies to under **From**, and choose **Save**.

When a rate changes for a new year, give the current version a last day under **To** and add a new version with the same tax code from the next day. Two versions of one tax can't be in effect on the same day.

## United States federal taxes

1. Turn on **United States Payroll** and run **United States Payroll: federal tax rules for 2026** from **Tools > Recipes**. It adds the federal income tax, Social Security, Medicare, Additional Medicare and federal unemployment rules and the six withholding tables for pay dates in 2026.
2. On each employer, add a registration for the jurisdiction **US**.
3. On each pay profile, add a **Tax election** for **US** from the person's Form W-4: the form's year, the filing status, the Step 2 box, the amounts of Steps 3 and 4 (a year, except extra withholding, which is each pay period), or for a form from 2019 or earlier the allowances. A person with no election is withheld as single with no adjustments.

A bonus paid on its own in a period is withheld on the period's total pay less what was already withheld on, unless the run uses the flat method (22%); supplemental pay over 1,000,000 in a year is always withheld at 37%. Each new year comes as a new recipe: check its amounts against the IRS's publications before the first pay date of the year. State and local taxes are added as tax rules by hand.

## Choose the provider and the least net pay

Under **Settings > Payroll**, **Tax provider** says what works out taxes when there is more than one, and **Least net pay** keeps net pay from falling below an amount: deductions taken after tax are cut, the largest priority number first.

## Preview what a person will be paid

On a pay profile, choose **Preview the next pay**. The page works out the current period of the person's pay schedule from the hours and amounts there are now, the pay items and the tax rules, and shows each line, the taxes, net pay and how it is paid, with any warning (such as a deduction cut because pay was too small). Nothing is kept.
