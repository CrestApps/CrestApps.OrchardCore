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
| **Menu** | **Commerce** → **Taxation** → **Income Tax Rules**, **Commerce** → **Taxation** → **Tables**, **Settings** → **Payroll**, and **Preview the next pay** on a pay profile |
| **Permission** | Manage income tax rules for the rules (only Administrators have it until it is given to another role); See pay profiles for the preview |
| **Feature** | Income Tax (turns on Tax Tables); a country pack such as United States Income Tax or Canada Income Tax |

<AskYourAdmin />

## Add an income tax rule

1. Open **Commerce** → **Taxation** → **Income Tax Rules** and choose **Add income tax rule**.
2. Choose the kind of rule. It can't be changed later, and the form shows only the fields that kind needs:
   - **Flat rate**: a rate on pay, such as Social Security, Medicare, unemployment insurance, CPP or EI.
   - **Share of another tax**: a multiple of another tax of the same pay, such as the employer's EI premium.
   - **Tax table or brackets**: a table or progressive brackets, such as a state income tax.
   - **Fixed amount**: the same amount each pay.
   - A country's own withholding, listed only when its country pack is on: **Federal income tax (Form W-4)** and **State income tax (withholding certificate)** for the United States; **Federal income tax (TD1)**, **Provincial or territorial income tax (TD1)** and **Quebec income tax (TP-1015.3)** for Canada.
3. Enter the **Name** shown on pay statements, a **Tax code** that stays the same for every version of the tax (such as US-SS or CA-CPP), and the **Jurisdiction** (such as US, US-CA, CA or CA-ON). The tax applies to employers registered in the jurisdiction; one below a country (with a dash) applies only to people who live or work there.
4. When another agency collects the tax, enter it under **Collected by**: a province's tax that the CRA collects is collected by CA. Under **Not for people in**, list the jurisdictions the tax doesn't apply to, such as CA-QC for the Canada Pension Plan.
5. Choose the **Income taxed** (income tax, pensionable earnings, insurable earnings, or one a country pack adds), the **Kinds of income** it is for when it isn't employment income, and who pays it: the earner or the payer. For a local tax, choose who it **Applies to** (anyone there, residents only, or nonresidents who work there), put rules of which only one may apply in the same group under **Only one of the group** (the lowest **priority** wins), and under **Election of** name the jurisdiction whose withholding certificate the rule reads, such as US-MI for a Michigan city.
6. Fill in the kind's own card:
   - **Flat rate**: the **Rate (%)**, or **Use the payer's own rate** to take the rate on the employer's registration (such as an unemployment insurance rate). For a city tax with an amount per exemption, enter **Each exemption** (such as 600): the exemptions are read from the person's certificate. Add the limits the tax has: the **Wage base** (the most income taxed in a year), the **Wage threshold** (only a year's income above it is taxed, such as an additional rate on high earners or a pension plan's second tier), the **Yearly exemption** (an amount never taxed, taken off each pay in proportion to the pay periods of a year, such as the 3,500 basic exemption of the Canada Pension Plan) and the **Yearly maximum** (the most tax taken in a year).
   - **Share of another tax**: how many **Times** which tax code, such as 1.4 times CA-EI, and its **Yearly maximum**.
   - **Tax table or brackets**: how it is **Worked out** (a table, brackets or a threshold), the **Tax table** (under **Commerce** → **Taxation** → **Tables**, with rates as fractions: 0.1 for 10%), whether to **Work it out on a year's income**, whether **The person's election applies** with what **Each allowance** lowers a year's income by, and the rates for **Bonuses and commissions**.
   - **Fixed amount**: the amount **Each pay** and its **Yearly maximum**.
   - A country's withholding: the tables and amounts its formulas need, whether **The person's election applies**, and, when its formulas don't handle them, the rates for **Bonuses and commissions**.
7. Enter the first pay date it applies to under **From**, and choose **Save**.

When a rate changes, give the current version a last day under **To** and add a new version with the same tax code from the next day. Two versions of one tax can't be in effect on the same day.

## Verify a rule

Each rule in the list shows **Verified** or **Not verified**. After you check a rule's amounts against the agency's publications, open the row's actions and choose **Verify**. Any later change to the rule sets it back to **Not verified**. A rule that is not verified is still used. Verifying needs the **Verify income tax rules** permission, which only Administrators have until it is given to another role.

## Country packs

A country pack adds that country's election forms and withholding formulas, and ships its rules for a year as a recipe under **Tools** → **Recipes**. Each new year comes as a new recipe: check its amounts against the agency's publications before the first pay date of the year.

### United States

1. Turn on **United States Income Tax** and run **United States Income Tax: federal rules for 2026** from **Tools** → **Recipes**. It adds the federal income tax, Social Security, Medicare, Additional Medicare and federal unemployment rules, backup withholding on contractors, and the six withholding tables for pay dates in 2026.
2. On each employer, add a registration for the jurisdiction **US**.
3. On each pay profile, add a **Tax election** for **US** from the person's Form W-4: the form's year, the filing status, the Step 2 box, the amounts of Steps 3 and 4 (a year, except extra withholding, which is each pay period), or for a form from 2019 or earlier the allowances. A person with no election is withheld as single with no adjustments.

For a nonresident alien, check **Nonresident alien** on the Form W-4 election: the amount of Publication 15-T is added to the year's wages before withholding.

A bonus paid on its own in a period is withheld on the period's total pay less what was already withheld on, unless the run uses the flat method (22%). Supplemental pay over 1,000,000 in a year is always withheld at 37%.

State and local taxes are added as income tax rules. For a state that withholds by the annualized method of its withholding guide (such as Michigan, Massachusetts or Virginia), add a **State income tax (withholding certificate)** rule for the state's jurisdiction (such as US-MI) with its brackets table and amounts: the no-tax threshold, the standard and head of household deductions, the exemption amounts, and for Massachusetts the Social Security and Medicare it deducts (US-SS-EE, US-MED-EE, US-MED-ADD) up to 2,000 a year. On each pay profile, add a **Tax election** for the state from the person's state certificate: exemptions, exemptions for age or blindness, head of household, extra withholding or exempt. A city tax, such as Detroit's, is a **Flat rate** rule for the city's jurisdiction (US-MI-DETROIT) with its resident and nonresident rates as two rules in one group. The person's city comes from their home address, and where they work from their work site or the employer. The federal returns and W-2s come with **United States Payroll Filings**: see [Liabilities and filings](./payroll-filings.md).

### Canada

Turn on **Canada Income Tax** and run **Canada Income Tax: 2026 rules**. It adds federal tax, the tax of every province and territory, Quebec tax, CPP and CPP2, EI, QPP, QPP2 and QPIP. See [Canadian payroll](./canada-payroll.md) for the registrations, the TD1 claims, the remittances and the T4 and RL-1 slips.

## Choose the provider and the least net pay

Under **Settings** → **Payroll**, **Tax provider** says what works out taxes when there is more than one. **Least net pay** keeps net pay from falling below an amount: deductions taken after tax are cut, the largest priority number first.

## Preview what a person will be paid

On a pay profile, choose **Preview the next pay**. The page works out the current period of the person's pay schedule from the hours and amounts there are now, the pay items and the income tax rules. It shows each line, the taxes, net pay and how it is paid, with any warning (such as a deduction cut because pay was too small). Nothing is kept.
