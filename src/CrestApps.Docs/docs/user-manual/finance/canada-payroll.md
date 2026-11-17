---
sidebar_label: Canadian payroll
title: "Canadian payroll: source deductions, remittances, T4 and RL-1"
description: "Pay people in Canada: register the employer with the CRA and Revenu Québec, enter TD1 and TP-1015.3 claims, let payroll work out federal, provincial and Quebec tax, CPP, EI, QPP and QPIP, remit the source deductions, and file T4 and RL-1 slips."
technical_manual:
  - modules/taxation
  - modules/payroll
---

Canadian source deductions come from **Canada Income Tax** in Taxation, which follows the CRA's payroll deductions formulas (T4127) and Revenu Québec's (TP-1015.F). The slips and the remittances come from **Canada Payroll Filings** in Payroll.

| | |
| --- | --- |
| **Menu** | Commerce > Taxation > Income tax rules, Payroll > Employers, the pay profile's **Tax elections**, Payroll > Liabilities and Filings |
| **Permission** | Manage income tax rules for the rules; Manage employers; Manage pay profiles; Manage payroll filings |
| **Feature** | Canada Income Tax and Canada Payroll Filings |

<AskYourAdmin />

## Set up the year's rules

1. Turn on **Canada Income Tax** and **Canada Payroll Filings**.
2. Run **Canada Income Tax: 2026 rules** from **Tools > Recipes**. It adds, for pay dates in 2026:
   - federal tax with the TD1 claim and the federal credits;
   - the tax of every province and territory, with the Ontario surtax, health premium and tax reduction, the British Columbia tax reduction and Alberta's supplemental credit;
   - the July 1 changes for British Columbia, Newfoundland and Labrador and Prince Edward Island;
   - Quebec tax by Revenu Québec's formulas, and the federal abatement for Quebec;
   - CPP and the second CPP contribution (CPP2), Employment Insurance, and in Quebec QPP, QPP2, QPIP and EI at Quebec's rate, each with the employer's share.
3. Each year comes as a new recipe. Check its amounts against the CRA's and Revenu Québec's publications before the first pay date of the year.

## Register the employer

On each employer under **Payroll > Employers**, set the currency to CAD and add the tax registrations:

- **CA**, agency CRA, with your business number and payroll program account (such as 123456789RP0001) as the account id. Under **Deposits**, choose your remitter type: **Monthly** for a regular remitter, **Quarterly by the 15th**, **Twice a month** (accelerated threshold 1) or **Four times a month** (accelerated threshold 2).
- **CA-QC**, agency Revenu Québec, with your identification number (such as 1234567890TQ0001), when you pay people who work in Quebec, with its remittance frequency. Add the **health services fund rate** Revenu Québec gives you for your total payroll as the registration's rate, from January 1; without one, the lowest rate (1.65%) is used.

On the employer, under **Canada**, choose the **dental benefits** code the T4's box 45 reports: the dental care you offered that employees could have had on December 31. Enter a contact name and phone on the employer too: the T4 XML file needs them.

Provincial and territorial tax, and EI, are collected by the CRA under your CA registration. Quebec tax, QPP and QPIP are collected by Revenu Québec under your CA-QC registration.

## Enter each person's claims

The province of employment is where the person works: choose their work site on the pay profile, with its province on the site's **Payroll** card. It is never where they live: a person whose work site names no province, or several, can't be paid until it names one. On the pay profile, enter the social insurance number (a number that fails its check digit is refused) and add the **Tax elections** the person gave you:

- **CA**, from the federal TD1: the total claim amount (empty for the basic personal amount), any additional tax to deduct each pay, a prescribed zone deduction, and other deductions a tax services office authorized.
- The province or territory, such as **CA-ON**, from its TD1: the total claim amount (empty for the basic personal amount) and, in Ontario, the dependants the Ontario tax reduction counts.
- **CA-QC**, from the TP-1015.3: the total claim amount (empty for the basic personal amount), the deductions of line 19, any additional tax, or exemption.

## What each pay withholds

Payroll works out each pay by the published formulas:

- **CPP** is 5.95% of pensionable earnings above the basic exemption for the pay (3,500 a year divided by the pay periods), up to the year's maximum. **CPP2** is 4% of the year's earnings between the first and second ceilings. The employer pays the same.
- **EI** is a share of insurable earnings up to the year's maximum. The employer pays 1.4 times the employee's premium.
- In Quebec, **QPP** and **QPP2** take the place of CPP and CPP2, **QPIP** is added, and EI is at Quebec's lower rate.
- **Federal and provincial tax** annualize the period's pay, apply the brackets and credits, and divide back by the pay periods. Quebec tax uses Revenu Québec's formulas, and federal tax is reduced by the Quebec abatement.
- A **bonus** or retroactive pay is taxed by the bonus method: the year's tax with the bonus, less the year's tax without it. A bonus paid on its own is added to a year of the person's regular pay (from their latest pay, or their salary). In Quebec, a lump sum is withheld at the marginal rate, or at 7% when the year's income with it is 18,952 or less.
- The employer's **health services fund** contribution in Quebec is the rate on its CA-QC registration times the pay of people who work in Quebec.

Once a maximum is reached, nothing more is taken for that contribution for the rest of the year.

## Remit the source deductions

Open **Payroll > Liabilities and Filings > Liabilities**. The CRA's row (CA) holds federal and provincial tax, CPP and EI, both shares. Revenu Québec's row (CA-QC) holds Quebec tax, QPP and QPIP, both shares, and the health services fund. Each row follows the remitter type of the registration, with its due date. After you remit, record the deposit under **Record a deposit** with the jurisdiction CA or CA-QC.

## File the T4 and RL-1 slips

1. Close the year under **Year end**, then open **Filings**.
2. Prepare **T4 slips and T4 Summary**. Each person paid Canadian source deductions has a T4 with a PDF (a person employed in two provinces has one for each), and the Summary adds the employer's CPP and EI, your remittances and the balance. Box 24 stops at the year's maximum insurable earnings and box 26 at the year's additional maximum pensionable earnings.
3. For people in Quebec, prepare **RL-1 slips and RL-1 Summary** the same way. The RL-1 Summary includes the health services fund.
4. To file the T4 by Internet file transfer, run **Canada Payroll: T4 slips as a T4 XML file** from **Tools > Recipes**. Then publish the pipeline it adds, with a delivery step that keeps the file private, such as an SFTP server. Only a user who may see full tax ids gets the social insurance numbers in the file. The T4 slips data set carries them encrypted, so a preview or a CSV never shows them. The file is refused when a slip has no province of employment, a number fails its check digit, or the employer has no dental benefits code or contact.
5. Mark each filing as filed. People then see their slips under **Tax forms** in **My pay**.

## Not covered yet

- The Record of Employment (ROE) is not prepared.
- Direct deposit pays US dollar ACH files only: pay Canadian employees by check until a Canadian (CPA Standard 005) provider exists.
- The commission (variable pay) methods of the T4127 and the TP-1015.F are not used: commissions are taxed as regular pay or as a bonus.
- Quebec employer contributions to the labour standards and the workforce skills development fund are not worked out.
