---
sidebar_label: Tax forms
title: Tax forms and their formulas
description: "Build the tax forms your business files without code, payroll forms and sales tax returns alike: their boxes and lines as fields, a formula for each field, yearly values such as thresholds and rates, checks that flag missing details, and how each recipient's statement is printed. Every function a formula can call, with examples."
technical_manual:
  - modules/taxation
  - modules/payroll
---

A tax form is the list of boxes and lines an agency asks for, such as Form 941, Form W-2, a T4 slip or a state's sales and use tax return, with a formula that says how each one is worked out. You can change a form when the agency changes it, or add a new form, without waiting for a new version of the site. The module that files the form prepares it from its own records using your formulas: Payroll from pay statements, Tax Returns from the figures of a return's period.

| | |
| --- | --- |
| **Menu** | **Commerce** → **Taxation** → **Tax Forms** |
| **Permission** | Manage tax forms |
| **Feature** | Tax Forms. Payroll's forms need Payroll; the United States and Canadian payroll forms come with United States Payroll Filings and Canada Payroll Filings. Sales tax returns need Tax Returns, which turns Tax Forms on |

<AskYourAdmin />

## Forms that come ready

You don't have to build the common forms yourself:

- **United States Payroll Filings** adds Forms 941, 940, 945, W-2 and W-3, and 1099-NEC and 1096 the first time it's turned on.
- **Canada Payroll Filings** adds the T4 slips and T4 Summary, and the RL-1 slips and RL-1 Summary.
- **Tax Returns** adds the **Generic return (totals by kind)**, the return a tax registration files with when its filing schedule names no other form.

They're also recipes under **Configuration** → **Recipes**: **United States Payroll: federal tax forms**, **Canada Payroll: T4 and RL-1 tax forms** and **Tax Returns: generic return form**. Running one again puts the shipped fields and formulas back, replacing your changes to those forms, and leaves your other forms alone. Check the figures, such as yearly maximums and rates, against the agency's current instructions before you file.

## Build a form

1. Open **Commerce** → **Taxation** → **Tax Forms** and choose **Add tax form**, or **Edit** on a form to change it. When more than one module prepares forms, **Add tax form** asks which one first, such as **Prepared from Payroll** or **Prepared from Sales tax returns**: the page then shows what that kind of form has.
2. Under **The form**:
   - **Title** is what people see, such as *Form 941 (quarterly federal return)*.
   - **Name** is the short name filings keep, such as `US-941`: letters, digits and dashes. Once a filing has been prepared from the form, the name can't change.
   - **Jurisdiction**: a filer registered in this jurisdiction files the form, such as `US`, `CA` or `CA-QC`. For Payroll, that's the employer's registrations. A sales tax return may leave it empty: a registration chooses its return form itself.
   - **Prepared from**: the module that prepares and files the form, such as **Payroll** or **Sales tax returns**. It decides which functions the formulas can call (see [Functions](#functions)). On a new form, choosing another one opens the page for it.
   - **Filed for**: a year or a quarter. A sales tax return has none: the registration's filing frequency decides its periods.
   - **Filers can prepare it**: untick it to stop offering the form. Turn a form off rather than deleting it once filings have been prepared from it; a form in use can't be deleted.
3. Under **Recipients** (for forms with a statement for each person or payee; a sales tax return has none):
   - **Each recipient gets a statement**: a W-2, a 1099 or a T4 slip. Each statement is kept on the filing, printed, and given to the recipient once the form is filed. Without it, recipient fields are worked out only to be added up on the filing (Form 940 does this for the pay over each person's wage base).
   - **A statement for each jurisdiction under the form's**: a T4 slip for each province a person was employed in.
   - **A recipient is reported when**: a formula over the recipient fields, such as `[1] >= VALUE('ReportingThreshold') OR [4] > 0`. Leave it empty to report anyone with a field that isn't zero.
   - **Note for no tax id**: added to the filing for a reported recipient with no tax id on file. `{0}` is their name, such as `{0}: no tax id on file.`
4. The source's own card, such as **Payroll**, holds what only it needs. For Payroll that's whose pay the form reports (employees, contractors or both), **Only people taxed in** a jurisdiction (`CA-QC` reports people with a Quebec tax line), **List the income taxes below the country** (a W-2's state and local boxes 15 to 20), and the **Pay code prefix** pay codes use to report to the form's fields (`W2:` for `W2:12D`). For a sales tax return, the **Sales tax return** card names the **Amount due field** (see [Sales tax returns](#sales-tax-returns)).
5. Add the **Fields**, the **Yearly values** and the **Checks** (below), and fill in **A recipient's statement**.
6. Choose **Save**. A form whose formulas don't work isn't saved: the page says which field is wrong and why.

## Fields

A field is one of the form's boxes or lines. Fields are in two lists:

- **On each recipient's statement**: worked out from that recipient's records, such as one person's pay statements for the year. W-2 box 1, T4 box 14.
- **On the filing**: worked out from every reported recipient's records, such as all the pay statements of the quarter. Form 941 line 2, the T4 Summary's line 82.

Choose **Add field**, or **Edit** on a field, to open its dialog:

- **Box or line**: the agency's number, such as `1`, `5a`, `12D` or `B.A`. Other formulas read the field by this number in brackets, `[5a]`. No brackets or colons in the number.
- **Label**: what's printed and shown, such as *5a Taxable social security wages*.
- **On**: each recipient's statement, or the filing.
- **Formula**: how the field is worked out. Click a function name under the box to insert it. As you type, the dialog checks the formula against the form's other fields: *✓ The formula works.* or what's wrong, such as *✗ 3: The field [ZZ] does not exist.*
- **Add up on the filing** (recipient fields): the filing gets the total of everyone's field under the same number. That's how a W-3 totals the W-2s and the T4 Summary totals the slips.
- **Pay codes report to it** (Payroll): pay codes can name the field under **Tax form fields** on the pay code, and their amounts are added to the formula's. A field with no formula is then just the amounts of those pay codes, such as W-2 box 12D for 401(k) deferrals. Such a field is left off a statement when no pay code reported to it.
- **On the printed statement** (recipient fields): never, always, or only when it isn't zero.

The order of the lists is only how they're shown and printed: use the arrows to move a field. Fields are worked out in the order their formulas need, so a field can read one listed after it. A field can't read itself, directly or through other fields.

## Formulas

A formula works out a number from the source's records, other fields and yearly values. It's the same formula language used by data pipelines and reports.

| What | How you write it | Example |
| --- | --- | --- |
| Another field of the same list | Its number in brackets | `[5a]`, `[B.A]` |
| A recipient field added up on the filing, from a filing field | Its number in brackets | `[1]` on the W-3 |
| Text | In single quotes | `'IncomeTax'`, `'US-FIT'` |
| A number | Digits, with a dot for decimals | `0.124`, `7000` |
| Arithmetic | `+`, `-`, `*`, `/` and parentheses | `([3] + [5e]) * 2` |
| Comparisons | `=`, `<>`, `<`, `<=`, `>`, `>=` | `[1] >= 2000` |
| Conditions | `AND`, `OR`, `NOT` | `[1] >= 600 OR [4] > 0` |

A missing value, such as a yearly value with no year yet, makes the result missing, and a field whose result is missing is zero. Use `COALESCE` to fall back: `COALESCE(RULE('US-FUTA', 'WageBase'), 7000)`.

Functions everyone uses most:

| Function | What it gives | Example |
| --- | --- | --- |
| `ROUND(number, decimals)` | Rounded, halves away from zero | `ROUND([5a] * 0.124, 2)` |
| `GREATEST(a, b, ...)` | The largest | `GREATEST(0, [12] - [13])`: a balance never below zero |
| `LEAST(a, b, ...)` | The smallest | `LEAST(68900, TAXABLE('CA-EI'))`: up to a maximum |
| `IF(condition, then, else)` | One value or the other | `IF(HASTAX('CA-QC-QPP'), TAXABLE('CA-QC-QPP'), TAXABLE('CA-CPP'))` |
| `COALESCE(a, b, ...)` | The first that isn't missing | `COALESCE(VALUE('Rate'), 0.6)` |
| `IN(value, a, b, ...)` | True when the value is one of them | `IN(DENTALCODE(), 1, 2, 3, 4, 5)` |
| `ABS`, `FLOOR`, `CEILING`, `MOD` | The usual arithmetic | `FLOOR([7])` |

The field dialog's **Every function a formula can call** lists them all, including the text and date functions.

## Functions

### For every form

| Function | What it gives | Example |
| --- | --- | --- |
| `VALUE(name, [fallback])` | A yearly value of the form for the filing's year: the value of the latest year at or before it. The fallback, or nothing, when there's none | `VALUE('ReportingThreshold')` is 600 for 2025 and 2,000 for 2026 when the form has 600 from 1982 and 2,000 from 2026 |
| `TOTAL(number)` | The total of a recipient field over the recipients reported, from a filing field. Use it when the filing field has the same number as the recipient field (the 1096's line 4 totals the 1099s' box 4) | `TOTAL('4')` |
| `RECIPIENTS()` | How many recipients were reported, from a filing field: statements, for a form with one per jurisdiction | `RECIPIENTS()`: W-3 box c, T4 Summary line 88 |
| `ISACCOUNT(number, shape)` | True when a number has a shape: `#` is a digit, `@` a letter, anything else itself. Spaces and dashes are ignored | `ISACCOUNT(ACCOUNT('CA'), '#########RP####')` |
| `FILINGYEAR()`, `FILINGQUARTER()` | The filing's year, and its quarter (1 to 4, or 0 for a yearly form) | `IF(FILINGYEAR() >= 2026, 2000, 600)` |

### Payroll's

These read pay statements: a recipient field reads that person's statements (or the ones of that jurisdiction, for a statement per jurisdiction), a filing field reads every reported person's statements. A voided statement and its reversal cancel out.

| Function | What it gives | Example |
| --- | --- | --- |
| `WAGES(category)` | The wages of a tax category from the pay code lines: earnings taxed for it, less deductions taken before the tax. The categories are `'IncomeTax'`, `'SocialInsurance'` and `'Unemployment'` (and any a country adds, such as `'InsurableEarnings'`) | `WAGES('IncomeTax')`: W-2 box 1, with a 401(k) taken off |
| `TAX(code, ...)` | The amount withheld or owed for taxes with these codes. `*` matches any characters | `TAX('US-FIT')`; `TAX('CA-FIT', 'CA-*-PIT') - TAX('CA-QC-PIT')`: every provincial tax but Quebec's |
| `TAXABLE(code, ...)` | The wages those taxes were worked out on | `TAXABLE('US-MED-EE')`: Medicare wages |
| `HASTAX(code, ...)` | True when there's a line of one of those taxes | `HASTAX('CA-CPP', 'CA-QC-QPP')` |
| `PAYKIND(kind, ...)` | The earnings of pay codes of a kind | `PAYKIND('Tips')` |
| `PAYCODEFIELD(name)` | The amounts of the pay codes that report to a field, by its full name. Needed only to read another form's field; a field's own **Pay codes report to it** is simpler | `PAYCODEFIELD('W2:12D')` |
| `GROSS()` | Gross pay | `GROSS()`: Form 940 line 3, 1099-NEC box 1 |
| `HEADCOUNT(day)` | The number of people paid for a pay period that includes that day of the last month of the period | `HEADCOUNT(12)`: Form 941 line 1 |
| `DEPOSITS(jurisdiction, [group])` | The deposits you recorded for the period to a jurisdiction, of one group (`'Withholding'`, `'Unemployment'`, `'NonPayrollWithholding'`) or all | `DEPOSITS('US', 'Withholding')`: Form 941 line 13 |
| `RULE(code, figure)` | A figure of the income tax rule with that code on the period's last day: `'Rate'`, `'WageBase'`, `'WageThreshold'`, `'YearlyMaximum'`, `'YearlyExemption'`, `'FixedAmount'` or one of its parameters. Nothing when there's no rule | `RULE('US-FUTA', 'WageBase')` |
| `REGIONWAGES()`, `REGIONTAX()` | The wages and the employee's income tax of the states or provinces | `REGIONWAGES()`: W-3 box 16 |
| `LOCALWAGES()`, `LOCALTAX()` | The same for local taxes, such as a city's | `LOCALTAX()`: W-3 box 19 |
| `ACCOUNT(jurisdiction)` | The account number on the employer's registration in a jurisdiction | `ACCOUNT('CA-QC')` |
| `DENTALCODE()` | With Canada Payroll Filings: the employer's dental benefits code (T4 box 45) | `IN(DENTALCODE(), 1, 2, 3, 4, 5)` |

### For sales tax returns

These read the return being worked out: the figures the modules gave for the period (its lines), the registration it's filed under, how late it is and what was paid. A line has a kind, such as `'GrossSales'`, `'ExemptSales'`, `'TaxableSales'`, `'UseTaxPurchases'`, `'Withholding'`, `'Credit'` or `'ExcessCollected'`, an amount (the sales or purchases), a tax, and may have a jurisdiction, a tax type, a category (such as an exemption reason) and the line of the form it goes on. A return's lines are on its page, under **Figures**.

In `AMOUNT`, `TAX` and `HASLINES`, leave a filter out, or give `''` or `'*'`, to match anything; `*` matches any characters, such as `'us-*'`. A line with no jurisdiction counts as the return's own.

| Function | What it gives | Example |
| --- | --- | --- |
| `AMOUNT(kind, [jurisdiction], [taxType], [category])` | The amounts of the lines of a kind | `AMOUNT('GrossSales')`; `AMOUNT('ExemptSales', '', '', 'Resale')`: sales for resale |
| `TAX(kind, [jurisdiction], [taxType], [category])` | The tax of the lines of a kind | `TAX('TaxableSales', 'us-mi')`: Michigan's tax only, when a provider gives other states' lines too |
| `HASLINES(kind, ...)` | True when there's a line of that kind | `IF(HASLINES('TaxableSales'), AMOUNT('TaxableSales'), [GrossSales] - [ExemptSales])` |
| `LINEAMOUNT(line)`, `LINETAX(line)` | The amounts, or the tax, of the lines a module gave for a line of the form | `LINETAX('5g')` |
| `REGISTRATION(figure)` | A figure of the registration: `'Number'`, `'Jurisdiction'`, `'TaxType'` or `'Name'` | `ISACCOUNT(REGISTRATION('Number'), '##-#######')` |
| `DAYSLATE()` | The days after the due date the return is filed; 0 when it's on time. A draft past its due date counts as filed today | `ROUND([6] * VALUE('InterestRate') / 365 * DAYSLATE(), 2)`: interest by the day |
| `MONTHSLATE()` | The months after the due date, a month begun counting as a whole one | `LEAST([6] * 0.05 * MONTHSLATE(), [6] * 0.25)`: 5% a month or part of a month, up to 25% |
| `ONTIME()` | True when the return is filed on or before its due date | `IF(ONTIME(), LEAST(ROUND([6] * 0.005, 2), 15), 0)`: a discount for filing on time |
| `PAID()` | What was paid on the return, with what was paid with the returns it corrects | `PAID()` |
| `PAIDEARLIER()` | What was paid with the returns an amendment corrects | `PAIDEARLIER()`: an amended return's line for the tax paid with the original |
| `AMENDMENT()` | 0 for the original return, 1 for its first amendment | `IF(AMENDMENT() > 0, PAIDEARLIER(), 0)` |
| `ISRECONCILIATION()` | True for the yearly return filed besides the monthly or quarterly ones | `IF(ISRECONCILIATION(), ..., 0)` |
| `PERIODMONTHS()` | The months the return covers: 1, 3 or 12 | `LEAST(ROUND([6] * 0.005, 2), 15 * PERIODMONTHS())`: a monthly cap on a quarterly return |

`FILINGYEAR()` is the year the return's period ends in, and `FILINGQUARTER()` its quarter (0 for a yearly return).

## Sales tax returns

A tax registration's returns (**Commerce** → **Taxation** → **Tax Returns**) are worked out by a tax form prepared from **Sales tax returns**. Its fields are the return's boxes; they're all on the filing, since a return has no recipients. The registration's **Return form** and **Annual return form**, in its filing schedule, say which form its returns use; with none chosen, it uses the **Generic return (totals by kind)**.

- **Amount due field**, on the **Sales tax return** card: the box that is what the return says is due, after credits, discounts, penalty and interest, such as `21`. It's `AmountDue` unless you name another. Payments recorded on the return are taken off it for the return's balance, so don't take `PAID()` off in that field.
- A draft past its due date is worked out as filed today, so it shows its penalty and interest, and a return marked as filed is worked out again as filed that day.
- A check that fails adds its note to the return.
- Changing a form changes drafts when they're prepared again. Returns marked ready or filed keep their boxes.

A form that a module adds in code still works, and a tax form with the same name is used instead of it.

**A state's monthly return** (filing fields, with *DiscountRate* 0.005, *DiscountCap* 15 and *InterestRate* 0.0365 as yearly values, and **Amount due field** `21`):

| Line | Formula |
| --- | --- |
| `1` Gross sales | `AMOUNT('GrossSales')` |
| `2` Sales for resale | `AMOUNT('ExemptSales', '', '', 'Resale')` |
| `4` Sales tax | `TAX('TaxableSales', 'us-mi')` |
| `5` Use tax | `TAX('UseTaxPurchases')` |
| `6` Total tax | `[4] + [5]` |
| `7` Discount | `IF(ONTIME(), LEAST(ROUND([6] * VALUE('DiscountRate'), 2), VALUE('DiscountCap')), 0)` |
| `8` Penalty | `IF(ONTIME(), 0, ROUND(LEAST([6] * 0.05 * MONTHSLATE(), [6] * 0.25), 2))` |
| `9` Interest | `ROUND([6] * VALUE('InterestRate') / 365 * DAYSLATE(), 2)` |
| `10` Credits | `TAX('Credit')` |
| `21` Amount due | `[6] - [7] + [8] + [9] - [10]` |

With 552 of tax and 20 of credits, filed on time, the discount is 2.76 and 529.24 is due. Filed 45 days late, there's no discount, the penalty is 55.20 (two months begun), the interest 2.48, and 589.68 is due. A check of `ISACCOUNT(REGISTRATION('Number'), '##-#######')` notes a registration number that isn't the state's shape.

## Yearly values

Figures that change by year go under **Yearly values**, so a formula doesn't need editing every January. Each value has a **Name**, the first **year** it applies to, and the **value**. A filing uses the latest year at or before its own: with *MaximumInsurableEarnings* at 65,700 from 2025 and 68,900 from 2026, a 2027 filing still uses 68,900 until you add 2027.

## Checks

A check is a formula over the filing fields that must be true; when it isn't, its note is added to the filing, before the notes about recipients. The note doesn't stop the filing from being prepared. It tells whoever files it what to fix first.

| Must be true | Note when it isn't |
| --- | --- |
| `ISACCOUNT(ACCOUNT('CA'), '#########RP####')` | The employer's CA registration needs the CRA business number with its payroll program account. |
| `IN(DENTALCODE(), 1, 2, 3, 4, 5)` | The employer needs its dental benefits code. |
| `[15] = 0 OR [14] = 0` | A balance and an overpayment can't both be shown. |

## A recipient's statement

How each recipient's statement is printed (the PDF they get, and the file name):

- **Called**: what the statement is called where the recipient sees it, such as *Form W-2* or *T4 slip*.
- **Printed title** and **Line under the title**, such as *Copy B, to be filed with the employee's federal tax return*.
- **Recipient's tax id label** and **Masked tax id**: how the last digits are printed, with `{0}` for them, such as `XXX-XX-{0}`.
- **Filer's number label** and **Filer's number from**: the registration whose account number is printed (`CA` prints the CRA business number); empty prints the filer's tax id.
- **File name**: the start of the PDF's name, such as `W-2` for *W-2-2026.pdf*.

The statement prints the recipient fields you marked as printed, two to a row, in the order of the list.

## Worked examples

**Form 941, Social Security** (filing fields, quarterly):

| Line | Formula |
| --- | --- |
| `5b` | `PAYKIND('Tips')` |
| `5a` | `GREATEST(0, TAXABLE('US-SS-EE') - [5b])` |
| `5a.tax` | `ROUND([5a] * VALUE('SocialSecurityRate'), 2)`, with *SocialSecurityRate* 0.124 |
| `7` | `TAX('US-SS-EE', 'US-SS-ER', 'US-MED-EE', 'US-MED-ER', 'US-MED-ADD') - [5e]`: the fractions of cents between what was withheld and the form's rates |
| `14` | `GREATEST(0, [12] - [13])`: the balance due |

**W-2 box 12D** (a recipient field with no formula): tick **Pay codes report to it** and **Add up on the filing**, and choose **Printed when not zero**. On the 401(k) pay code, choose *Forms W-2 and W-3: 12 Code D* under **Tax form fields**. Box 1, `WAGES('IncomeTax')`, already leaves the deferral out because the pay code is a deduction taken before income tax.

**T4 box 26** (pensionable earnings up to the year's maximum, only for someone who paid CPP or QPP): `IF(HASTAX('CA-CPP', 'CA-QC-QPP'), LEAST(VALUE('MaximumPensionableEarnings'), GREATEST(WAGES('SocialInsurance'), IF(HASTAX('CA-QC-QPP'), TAXABLE('CA-QC-QPP'), TAXABLE('CA-CPP')))), 0)`. When the person paid neither, the box is 0.

**Form 1099-NEC threshold**: recipient fields `1` `GROSS()` and `4` `TAX('US-BWH')`, the condition `[1] >= VALUE('ReportingThreshold') OR [4] > 0`, and the yearly value *ReportingThreshold* 600 from 1982 and 2,000 from 2026. The 1096's lines are filing fields `3` `RECIPIENTS()`, `4` `TOTAL('4')` and `5` `TOTAL('1')`.

## When something's wrong

| You see | Why | What to do |
| --- | --- | --- |
| *The field [X] does not exist* | The formula reads a number no field of its list has | Check the number, or add the field. A filing field can read a recipient field only if it's added up on the filing |
| *The function X does not exist* | The function isn't one the form's source has, or its feature is off | Check the spelling, and the **Prepared from** source. `DENTALCODE()` needs Canada Payroll Filings |
| *Field 5a reads itself through the fields it reads* | Two or more fields read each other in a circle | Work one of them out from the records instead |
| *Two recipient fields are numbered 1* | Numbers must be unique within a list | Renumber one |
| Preparing a filing says the form's formulas don't work | A function's feature was turned off after the form was saved | Turn the feature back on, or edit the form |
| A field shows 0 | A yearly value has no year at or before the filing's, or the records have nothing for it | Add the year's value; check the tax codes and categories |
| *A return's amount due is read from field AmountDue* | A sales tax return has no filing field with the number its **Amount due field** names | Add the field, or name the right one on the **Sales tax return** card |
| *A form prepared from Sales tax returns has no recipients* | A sales tax return has a field on each recipient's statement | Make every field a filing field |
| A return's notes say its form's formulas don't work | A formula broke after the form was saved | Edit the form; the return has no boxes until it's prepared again |
