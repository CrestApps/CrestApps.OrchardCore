---
sidebar_label: Payroll
title: Payroll
description: "Payroll for Orchard Core. Pay Setup owns pay schedules (periods, the overtime week and pay dates) and pay codes (class, open kind, multiplier, premium, export code), shared by Workforce's time clock, with usage checks, recipe and deployment steps, and the migration that moved the time clock's pay periods and pay codes."
user_manual:
  - user-manual/finance/pay-setup
  - user-manual/finance/payroll-employers
  - user-manual/finance/payroll-garnishments
  - user-manual/finance/payroll-filings
  - user-manual/finance/income-tax
  - user-manual/finance/canada-payroll
---

The **Payroll** module (`CrestApps.OrchardCore.Payroll`) runs payroll. Its first feature, **Pay Setup**, owns the two records both payroll and time keeping need: pay schedules and pay codes. Workforce's time clock depends on Pay Setup, and Payroll never references Workforce.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Payroll.PaySetup` | Pay Setup | none (turned on by Workforce Time Clock or Payroll) |
| `CrestApps.OrchardCore.Payroll` | Payroll | Pay Setup, Staff, Staff Compensation, Approvals, Addresses, Taxation's Income Tax (uses Teams, Banking, Recipes, Deployment and CrestApps Recipes when they are on; reads teams only while the Teams feature is on) |
| `CrestApps.OrchardCore.Payroll.MultipleEmployers` | Multiple Employers | Payroll |
| `CrestApps.OrchardCore.Payroll.UnitedStates` | United States Payroll Filings | Payroll, Taxation's United States Income Tax |
| `CrestApps.OrchardCore.Payroll.Canada` | Canada Payroll Filings | Payroll, Taxation's Canada Income Tax |

Projects: `CrestApps.OrchardCore.Payroll.Abstractions` (models, managers, `IPaySetupUsage`, `AddPayCodeKind`), `CrestApps.OrchardCore.Payroll.Core` (stores, managers, handlers, index providers and schema, the pay code seeder), `CrestApps.OrchardCore.Payroll` (the module).

## Pay schedules

A `PaySchedule` (catalog entry, `IPayScheduleManager`) says how often people are paid and when:

| Member | Meaning |
| --- | --- |
| `Frequency` | `Weekly`, `Biweekly`, `FourWeekly`, `SemiMonthly`, `Monthly`, `Quarterly`, `Yearly`. |
| `AnchorDate` | A day a period starts on, for weekly, every-two-weeks and every-four-weeks schedules; its month starts quarterly and yearly periods. |
| `SecondPeriodStartDay` | Twice a month: the day the second period starts (2 to 28, 16 by default). |
| `WeekStart` | The day the overtime week starts. |
| `PayDateRule`, `PayDateOffsetDays`, `FirstPayDay`, `SecondPayDay` | Pay a number of days after the period ends, or on fixed days of the month (0 is the last day) for monthly and twice-a-month schedules. |
| `NonBusinessDayShift` | A pay date on a day off moves to the business day `Before` or `After`, or stays (`None`). |
| `ApprovalLeadDays` | Days before the pay date a pay run must be approved. |

`PeriodOn(date)`, `WeekOf(date)` and `PayDateFor(periodEnd, isBusinessDay)` are pure. `PayDateFor` treats Saturdays and Sundays as days off unless the caller passes its own test, such as one that also reads a holiday calendar.

## Pay codes

A `PayCode` (catalog entry, `IPayCodeManager`, codes unique and upper case) has a `Code`, a `Name`, a `Class` (`Earning`, `Deduction`, `EmployerContribution`, `ImputedIncome`, `Reimbursement`, `Memo`), a `Kind`, a `Multiplier` (0 to 10), a `PremiumPerHour`, an `ExportCode` (`PayrollCode` returns it or the code) and `Enabled`.

The kind is an open string. `PayCodeKinds` holds the built-in ones (`Regular`, `Overtime`, `DoubleTime`, `Holiday`, `Weekend`, `PaidTimeOff`, `PredictabilityPay`, `CallBack`, `ShiftDifferential`, `Unpaid`, `Salary`, `Bonus`, `Commission`, `Tips`); a module adds its own:

```csharp
services.AddPayCodeKind("Mileage", LocalizationSource.Create<Startup>("Mileage"));
```

Modules keep their own settings on a code in `Properties`. Workforce keeps a shift differential's window there (`WorkforcePayCodeSettings`: `WindowStart`, `WindowEnd`, `Weekdays`), with its own card on the pay code editor and its own check that a shift differential has a window. The time clock only picks earning and memo codes for lines of hours.

When Pay Setup is turned on, it adds the usual codes (`REG`, `OT`, `DT`, `PTO`, `PP`, `HOL`, `WKND`, `UNPAID`) to a site that has none.

## Records in use are kept

A schedule or code another module uses can't be deleted; turn it off instead. A module tells Pay Setup what it uses:

```csharp
services.AddPaySetupUsage<MyPaySetupUsage>();

public sealed class MyPaySetupUsage : IPaySetupUsage
{
    public Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default) => ...;

    public Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default) => ...;
}
```

The admin pages list what uses the record, and the stores refuse the delete too. The time clock reports the policies and timesheets that name a schedule, and the time off types and timesheet lines that name a code.

## Pay inputs

Modules hand hours and amounts to payroll through `IPayInputSource` (`AddPayInputSource<T>()`): `GetInputsAsync(PayInputQuery)` returns `PayInput` rows (staff member, pay schedule and period, pay code, day, hours or amount, rate, cost allocation, the record it comes from, and whether a pay run or an export already took it); `MarkConsumedAsync` and `ReleaseAsync` record that a consumer (a pay run, or a data pipeline run) took the inputs of some records, or gave them back. `IPayInputService` reads every source and tells each source about its own records. Workforce's time clock is the first source: one input per locked timesheet, pay code, day, activity and job.

With Data Pipelines on, Pay Setup adds Orchard's **Payroll** data source with the **Pay inputs** data set (`PayInputs`), read only by people with `ExportPayrollData`. It holds the inputs not taken yet; the parameter `IncludeExported=true` adds the others; filters on `PayScheduleId` and `PeriodStart` narrow the read. `Amount` is not rounded (its own amount, or hours times the code's premium, or times the rate and the multiplier; unpaid time pays nothing): sum, then round.

The **Mark pay inputs exported** step (`MarkPayInputsExported`) passes the rows on unchanged and, once the whole run succeeds, records the records they come from (`SourceKind`, `SourceId`) as taken by the run (`pipeline:{runId}`). A preview, a failed or a cancelled run marks nothing. The publisher of the pipeline must have `ExportPayrollData`.

The recipe **Payroll: export pay inputs to a CSV file** (`PayrollPayInputsExport`) adds the draft pipeline **Pay inputs export**: read the pay inputs, mark them, group by person, period, pay code and rate summing the hours and amounts, round, sort, choose the columns (EmployeeId, Name, PeriodStart, PeriodEnd, PayCode, Hours, Rate, Amount) and write a CSV file. Add a delivery step and publish it.

## Admin

**Payroll** → **Management** → **Pay Schedules** and **Pay Codes**, under `ManagePaySetup`. The schedule editor shows the next four periods and their pay dates, counted from today in the site's time zone.

| Permission | Allows |
| --- | --- |
| `ManagePaySetup` (Manage pay setup) | Pay schedules and pay codes. |
| `ExportPayrollData` (Export payroll data) | Reading payroll data in data sets and pipelines. |

## Recipes and deployment

The `PaySchedules` step (`Schedules`, matched by id then name) and the `PayCodes` step (`Codes`, matched by id then code), both described to CrestApps Recipes, and the **Pay schedules** and **Pay codes** deployment steps.

```json
{
  "name": "PayCodes",
  "Codes": [
    { "Code": "NIGHT", "Name": "Night shift", "Class": "Earning", "Kind": "ShiftDifferential", "PremiumPerHour": 2.25,
      "Properties": { "WorkforcePayCodeSettings": { "WindowStart": "22:00:00", "WindowEnd": "06:00:00", "Weekdays": [] } } }
  ]
}
```

Workforce's `WorkforceTimeClock` step still reads its `PayPeriods` and `PayCodes` keys and saves them through these managers, so older recipes keep working. Its deployment step keeps exporting the pay schedules its policies name, and leaves pay codes to the `PayCodes` step.

The **Payroll sample** recipe (`PayrollSample`, `payroll-sample.recipe.json`) sets up a payroll to try a pay run on. It turns on Payroll, United States Payroll Filings, Garnishments, My Pay and Banking's Bank Accounts, Checks, Disbursements and Direct Deposit, runs the **United States Income Tax: federal rules for 2026** recipe through Orchard's `Recipes` step, adds the income tax rule **Texas unemployment (SUI)** (`US-TX-SUI`: the employer, at the rate of its `US-TX` registration, on the first 9,000 of a year's wages), and then adds, with fixed ids so a second run updates instead of duplicating:

- the employer **Contoso** in Austin, Texas, with its `US` and `US-TX` registrations (the Texas one with a 2.7% rate) and the funding account **Contoso operating account**, a business account with **Bank file (ACH)** settings (`NachaOriginSettings`) of test values;
- the check layout **Contoso paychecks** (blank stock, check on top, no check line until a font is uploaded);
- the pay schedule **Every two weeks** (anchored on 4 January 2026, paid six days after the period, on Fridays) and the pay codes `401K` (a deduction exempt from income tax, a percent of earnings, with a limit of 24,500 a year and W-2 box 12D) and `CSUP` (child support, a garnishment);
- four staff members with their pay (Staff Compensation) and pay profiles: Avery Lee (salary), Jordan Smith (paid by the hour, overtime eligible), Riley Chen (salary, a 401(k) item with a 50% employer match up to 3% of pay, and a child support order of 150 each pay paid by check) and Sam Rivera (salary, paid by check); the others are paid by deposit to verified accounts of their own.

Tax ids are fictitious (987-65-4320 to 987-65-4323, numbers the Social Security Administration never issues), and account numbers are test values on routing number 021000021. No recipe step adds pay inputs, so open a pay run of Contoso for the current period and add Jordan's hours (such as 80 regular and 4 overtime) as the run's inputs before you calculate it. The sample has no Canadian employer: a Canadian employee needs a work site team with a province in its **Payroll** card, which no recipe step sets yet.

## Employers

An `Employer` (catalog entry, `IEmployerManager`, name unique) is the legal entity that pays people: `Name`, `LegalName` (`PrintedName` falls back to the name), its own `TaxId`, `Address` (the shared address editor), `LogoPath` (media library), `CountryPack`, `Currency`, `FundingBankAccountId` (with Banking on, one of the bank accounts the employer owns: `PartyRef("Employer", id)`), signatory and contact, `Registrations` (`EmployerTaxRegistration`: jurisdiction, agency, account id, deposit frequency, the tax `Categories` it covers when the employer has several accounts in one jurisdiction, such as `IncomeTax` for a state's withholding account and `Unemployment` for its unemployment insurance account, and dated `Rates`, `RateOn(day)`), `WorkJurisdictions` (where its people work when their pay profile names no work site team or the team has no jurisdictions, such as `US-MI` and `US-MI-DETROIT`: a business with one location needs no Teams feature) and `Status`. Employers are a party kind (`EmployerPartySource`). A module that points at an employer registers an `IEmployerUsage`; an employer in use can't be deleted (the page and the store both refuse).

The `PayrollEmployers` recipe step (`Employers`, matched by id then name) and the **Employers** deployment step carry them; Payroll's settings use Orchard's `Settings` step (`PayrollSettings`) and a site settings deployment step.

### The company

The Payroll feature is the company's own payroll. `IPayrollCompanyService.GetCompanyAsync()` returns the site's company: the employer `PayrollSettings.CompanyEmployerId` names, or the oldest employer. **Payroll** → **Management** → **Company** (`CompanyController`, `ManagePayrollSettings`) edits it with the employer editor; its first save adds the employer and sets `CompanyEmployerId`. `PayRunCompanyEmployerHandler` and `PayProfileCompanyEmployerHandler` give a new run or profile the company as its `EmployerId` when nothing chose one (on initialize, validate and create, before the handlers that check the employer), so the base feature's editors never show an employer and a recipe, workflow or import may leave it out.

### Multiple Employers

The **Multiple Employers** feature (`CrestApps.OrchardCore.Payroll.MultipleEmployers`) is for a site that pays the people of more than one employer. It adds nothing to the data, only the UI, through drivers and shapes the base feature leaves room for:

- `PayrollEmployerOptions.MultipleEmployers` is set, so pages name the employer of what they show (rows of pay profiles and garnishments, the pay run page, a statement, the Payroll card of the staff profile, pay dates on the calendar) and the menu shows **Management** → **Employers** (`EmployersController`) instead of **Company**. The company carries a **Your company** badge.
- `PayRunEmployerDisplayDriver` and `PayProfileEmployerDisplayDriver` add the employer picker (shape `PayrollEmployer_Edit`, at the top of the editor's card) and its row badge (`PayrollEmployerName_SummaryAdmin`). A person has one pay profile per employer (`PayProfileHandler`), so the staff profile offers **Add pay profile** until they have one with every active employer the user manages; without Multiple Employers, until they have one.
- `PayrollEmployerFilterDisplayDriver` renders the employer picker of the payroll pages' filters. Pages build an `IDisplayManager<PayrollEmployerFilter>` display with the display type `Toolbar` (the pay run list's toolbar), `Field` (among a filter form's fields: liabilities, year end) or `Card` (a filter card of its own: filings); without the feature it renders nothing.
- `PayrollNewEmployerSettingsDisplayDriver` adds **Settings** → **Payroll** → **New Employers** (`DefaultCountryPack`, `DefaultCurrency`).
- `PayrollEmployerPermissionProvider` lists each employer's copy of the per-employer permissions.

`PayrollMultipleEmployersMigrations` turns the feature on, after the request that ran the migration, for a site that already has more than one employer.

## Permissions per employer

`PayrollPermissions` holds the Payroll feature's permissions. Every one in `PayrollPermissions.PerEmployer` also exists per employer through `PayrollPermissionTemplates.For(permission, employerId)` (named `{Permission}_{employerId}`, implied by the global permission; a per-employer "view" is also implied by the same employer's "do" permissions). With Multiple Employers on, `PayrollEmployerPermissionProvider` lists each employer's copies, so the role editor shows a group per employer, such as **Payroll: Contoso**.

`IPayrollScopeService.GetScopeAsync(user, permission)` returns a `PayrollScope`: every employer with the global permission, otherwise the employers of the per-employer permissions the user holds. `AuthorizeAsync(user, permission, employerId)` checks one employer. Pages, data sets, reports and exports all use it. `IPayrollEmployeeDirectory` maps employers to the staff members they pay (pay profiles answer it; until then it knows none, so data limited to some employers is empty, never too wide).

The Pay inputs data set and the Mark pay inputs exported step honor the scope: a reader of some employers reads their people's inputs only.

| Permission | Allows |
| --- | --- |
| `ManagePayrollSettings` | Employers, registrations, ledger codes, tax setup and settings. Adding or deleting an employer needs it for every employer |
| `ViewPayProfiles` / `ManagePayProfiles` | See or change pay profiles, pay items and elections |
| `ViewTaxIds` | See a full tax id (every view is recorded) |
| `ManageGarnishments` | Garnishment orders |
| `PreparePayRuns`, `ApprovePayRuns`, `ProcessPayRuns`, `VoidPayStatements` | Each step of a pay run |
| `ViewPayRuns` | See runs and their statements |
| `ViewPayrollReports` / `ExportPayrollData` | Reports and data sets; exports |
| `ManagePayrollFilings` | Liabilities, deposits, filings, year-end |
| `ViewPayOfManagedStaff` | A manager sees the pay of the staff they manage (off by default, not per employer) |
| `ViewOwnPay`, `ManageOwnPayDetails` | One's own pay and details (the Staff role gets them) |

The recipe **Payroll: sample roles** adds Payroll administrator, Payroll clerk (no approval) and Payroll approver.

## Pay profiles and pay items

An `EmployeePayProfile` is what one employer needs to pay one staff member: there is one per staff member and employer (`IPayProfileManager`, the store refuses a second). It holds the pay schedule, the worker type (employee or contractor), the overtime status, the status (active, on leave, terminated), a legal name and home address for forms, a work site team, where the cost goes when an input says nothing (`DefaultAllocation`), how far retro pay may reach back, the tax elections and the split of net pay.

- **Tax id.** `TaxId` is a `ProtectedValue` (`ISensitiveValueProtector`, purpose `PayrollConstants.TaxIdPurpose`): the page shows only its last characters. `TaxIdHash` is a keyed hash (HMAC with a key kept in the Payroll collection, protected by the tenant's data protection keys) of the digits and letters, so the same number typed with or without dashes is found on another profile (`IPayProfileService.FindSameTaxIdAsync`) without being readable. The Payroll migration makes the key; when it is still missing, `PayrollTaxIdHasher` makes it on first use through the scope's own session, in the same unit of work, so hashing a tax id in a recipe or an import that already saved records works on every database, SQLite included (a second session was a second writer there, which SQLite refuses). `RevealTaxIdAsync` returns the clear number and records the read; the page needs `ViewTaxIds` for the employer and sends the number with `Cache-Control: no-store`.
- **Elections.** `TaxElection` rows (jurisdiction, first day, values, who entered them and when) are never edited: a new row from a later day replaces the old one from that day on, and `ElectionOn(jurisdiction, day)` returns the row in effect. The values an editor asks for come from an `ElectionFieldSet` per jurisdiction (`AddElectionFieldSet`); the generic set (`*`) asks for a filing status, allowances, extra withholding and an exemption. Tax engines add their own sets.
- **Net pay.** `Distribution` is an ordered list of `NetPayRow`: a deposit to one of the person's bank accounts (Banking, owner `PartyRef("Staff", id)`) or a check, for an amount, a percent or what is left. The last row takes what is left and only the last; percents can't pass 100.
- **Blockers.** `IPayProfileService.GetBlockersAsync` lists what keeps a profile from being paid: no employer or an inactive one, no pay schedule, no tax id, no election for a jurisdiction of the employer's registrations or of the work site team that has a tax in effect today that reads an election (the tax provider in use says which, through `IIncomeTaxProvider.GetElectionJurisdictionsAsync`; a provider that can't tell, or no provider chosen, asks for every one), no split of net pay, no pay rate or salary on the staff member's Pay card. Banking adds a missing or unverified bank account. A module adds its own with `IPayProfileBlockerProvider`. The profile page and the staff member's **Payroll** card show them.
- **Changes.** `IPayProfileObserver` hears every tax id reveal and change, new election, change of the net pay split and save. With Audit Trail on, each is written to the Audit Trail (category **Payroll**) with the tax id's last characters only.
- **Separation of duties.** `MayChangePayOfAsync` refuses a user changing the pay of the staff member linked to their own user, unless **Settings** → **Payroll** allows it.

An `EmployeePayItem` is a standing amount on a profile: a pay code, an amount or a percent (one of them), the first and last day, a goal (such as a loan) with what was paid toward it, a period and a year limit, an employer match and its limit, and arrears. `AppliesOn(day)` is false before the first day, after the last and once the goal is paid. An item copies its profile's staff member and employer; a profile with items, and a pay code or employer that profiles or items use, can't be deleted.

Pay profiles are reached from the staff member: the **Pay** section of the staff profile has a **Payroll** card listing the person's profiles of the employers the user may see (`ViewPayProfiles`), with their blockers and **Add pay profile**; **Pay items** opens from a profile's row and page. There is no separate employees list: blockers also show on each pay run.

Pay is private. `MyPayController`, `PayProfilesController`, `PayPreviewsController`, `PayItemsController` and `GarnishmentsController`, and the pay run's statements, statement, stub, void and reissue actions carry `[ConfirmPassword]` (see [Users: confirm the password before private pages](./users.md#confirm-the-password-before-private-pages)): the user types their password, and pay pages open without asking again for 10 minutes. The pay cards on a staff profile use `StaffPayPrivacy`, so the **Pay** section shows one prompt until then. On the person's own profile, `StaffPayPrivacy.DecideAsync(httpContext, staffMember)` checks `IsJustConfirmedAsync`, so their own pay asks every time the profile opens; the prompt returns to the profile's **Pay** section.

The **Employees** data set (`PayrollEmployees`) reads profiles for `ViewPayrollReports`, limited to the user's employers, with the tax id's last characters only. The `PayrollPayProfiles` and `PayrollPayItems` recipe steps import profiles by `EmployeeNumber` (or `StaffMemberId`), `EmployerName` (or `EmployerId`) and `PayScheduleName`, with a `TaxId` in clear that the step protects; items name their `PayCode`. The **Pay profiles** deployment step writes both steps; it writes nothing when run without a user, and writes tax ids only when **Include the tax ids** is checked and the user may see them (each one is recorded as a reveal).

```json
{
  "name": "PayrollPayProfiles",
  "Profiles": [
    {
      "EmployeeNumber": "1001",
      "EmployerName": "Contoso",
      "PayScheduleName": "Every two weeks",
      "WorkerType": "Employee",
      "TaxId": "987-65-4321",
      "Elections": [ { "Jurisdiction": "US", "EffectiveFrom": "2026-01-01", "Values": { "FilingStatus": "Single" } } ],
      "Distribution": [ { "Method": "Check", "Split": "Remainder" } ]
    }
  ]
}
```

## Pay code rules

The Payroll feature adds a **Payroll** card to the pay code editor (shown to `ManagePayrollSettings` for every employer) that keeps `PayrollPayCodeSettings` in the code's properties. `code.GetPayrollSettings()` returns them, or the defaults for the code's class and kind (`PayrollPayCodeSettingsExtensions.DefaultsFor`) when none are stored:

| Setting | Meaning |
| --- | --- |
| `Calculation` | `HoursByRate`, `FixedAmount`, `PercentOfEarnings` (of `BaseCodeIds`, or every earning) or `PercentOfDisposableEarnings` (deductions) |
| `TaxTreatment` | `Taxed` or `Exempt` per tax category; a category left out takes the class's default (`DefaultTreatment`: earnings, imputed income and deductions taxed; employer contributions, reimbursements and memo lines exempt) |
| `Supplemental`, `CountsInRegularRate`, `CountsAsHoursWorked`, `InRetroPay` | Flags the engine reads |
| `PeriodLimit`, `YearLimits` (dated, `YearLimitOn(day)`), `LifetimeLimit` | Limits of the code |
| `Priority`, `Arrears` (`None`, `CarryForward`), `Proration` (`None`, `WorkingDays`, `CalendarDays`) | Order, shortfall and part-period rules |
| `FormBoxes` | Fields of tax forms the code reports to: the fields of the Payroll tax forms whose **Pay codes report to it** is on (such as `W2:12D`), and any a module registers with `AddPayrollFormBox` |

Tax categories are Taxation's open list: `IncomeTaxCategories` holds `IncomeTax`, `SocialInsurance` and `Unemployment`, and a country pack adds its own with `AddIncomeTaxCategory(name, displayName)` (Canada adds `InsurableEarnings`). A pay code's treatment is set for each.

Two pure rules in `Payroll.Core` (`CrestApps.OrchardCore.Payroll.Core.Calculation`) carry the matrix and the limits for the engine:

- `PayCodeTaxMatrix.WagesFor(category, lines)`: earnings and imputed income taxed for the category, less deductions exempt from it, never below zero. A deduction exempt from income tax only lowers income tax wages.
- `PayLimits.Apply(wanted, code, item, usage, payDate)`: the code's period, yearly (in effect on the pay date) and lifetime limits and the pay item's period and yearly maximums and goal, against what the accumulators say was already taken (`PayLimitUsage`). The lowest room left wins; a reached limit leaves nothing; the result names the limit that cut the line.

When Payroll is turned on, `PayrollPayCodeSeeder` adds SAL, BONUS, COMM, TIPS, REIMB and LOAN with their settings, skipping a code already used. A pay code another code takes a percent of can't be deleted (`PayCodeBaseUsage`). The `PayCodes` recipe and deployment steps carry the settings in `Properties`:

```json
{
  "name": "PayCodes",
  "Codes": [
    {
      "Code": "RET", "Name": "Retirement plan", "Class": "Deduction", "Kind": "Contribution",
      "Properties": {
        "PayrollPayCodeSettings": {
          "Calculation": "PercentOfEarnings",
          "TaxTreatment": { "IncomeTax": "Exempt" },
          "YearLimits": [ { "EffectiveFrom": "2026-01-01", "Amount": 23500 } ],
          "Priority": 20
        }
      }
    }
  ]
}
```

## The engine

A pay is calculated by `IPayCalculator` from a `PayCalculationContext`: the profile, the employer, the schedule, the period and pay date, the staff member's pay (`StaffPayRates`) and hire and termination dates, the inputs, the pay items, the pay codes, the accumulators, the supplemental method, the least net pay, a gross-up and the home and work jurisdictions. `PayCalculationContextFactory` builds one from what the site holds. The calculation is pure: it fills the context's `Lines`, `Warnings` and `Payments`, and keeps nothing.

It runs the `IPayCalculationStep`s in order (`PayCalculationOrder`); a module adds one with its own order, or replaces a built-in one by registering a step with the same order:

| Order | Step | What it does |
| --- | --- | --- |
| 100 | Earnings | The salary for the period (`SalaryOn` each counted day, prorated by the salary code's rule for a hire, a termination or a salary change), hours times the rate times the code's multiplier (or the premium an hour of a code with one), amounts, recurring earnings and imputed income from pay items, and the earning of a gross-up |
| 200 | Regular rate | In each overtime week, pay for hours worked at their straight rate plus other pay that counts (premiums, bonuses and commissions of the week, or spread over the period's weeks by hours) divided by the hours: an overtime line paid below it gets the premium it requires. Exempt profiles have none |
| 300 | Deductions before tax | Deductions not taxed for at least one category, by priority, within their limits (`PayLimits`) and with their arrears |
| 500 | Taxes | Regular and supplemental wages of each tax category from the treatment matrix, worked out by the tax provider |
| 600 | Deductions after tax | The other deductions, by priority, within their limits |
| 700 | Disposable earnings | Deductions that are a percent of gross less the employee's taxes |
| 800 | Net check | Below the least net pay, deductions taken after tax are cut, the largest priority number first; what a deduction that carries arrears could not take is owed (`ArrearsAdded`) |
| 900 | Contributions | Employer contributions, the employer's match of a deduction item (at most a percent of earnings), and reimbursements |
| 1000 | Distribution | Net pay split by the profile's rows: amounts, percents of net pay (`Money.Allocate`, so they add up exactly), and what is left; with no row, a check |

A gross-up (`PayGrossUp`: an earning and a target net) repeats the steps, raising the earning by what net pay misses until it is within a cent.

Lines are `PayStatementLine`s (type, pay code or tax code and category, hours, rate, amount, jurisdiction, payer, taxable wages, what a limit or the net check cut, arrears, pay item, cost allocation, source inputs). A `PayStatement` keeps them with its run, period, pay date, gross, net, payments, a snapshot of names and addresses, `VoidOf`/`VoidedBy` and a status (`Draft`, `Final`, `Paid`, `Voided`); one that is not a draft can't be deleted.

**Accumulators** (`IPayAccumulatorService`) are never stored: they are the sums of `PayrollPayStatementLineIndex` rows, one per line of each statement that is not a draft, by pay code (`PayAccumulators.CodeKey`) and tax (`TaxKey`): the period, month, quarter, year and lifetime amounts, the year's hours and a tax's year-to-date taxable wages. A void is a statement with negative lines, so the two cancel out. The rows keep amounts as whole minor units and hours as hundredths, in integer columns.

**Preview the next pay** on a pay profile runs the engine for the current period of its schedule with the inputs and items there are now, and shows the lines, net pay, its split and the warnings, without keeping anything (`ViewPayProfiles` for the employer).

## Pay runs

A `PayRun` is one payroll of one employer (`IPayRunManager`): a number from the `Payroll:PayRuns` sequence, a `Title`, a pay schedule (or named people, `StaffMemberIds`, paid whatever their schedule), a type (`Regular`, `Supplemental`, `OffCycle`, `Final`, `Correction`, `Contractor`, and `Reversal` and `Historical` for later slices), a period, a pay date, the supplemental method, the totals and issues of its last calculation, a version stamp, the approval request and a status on `PayRunStatuses.Transitions`: Draft, Calculating, Calculated, PendingApproval, Approved, Processing, Paid, PartiallyPaid, Failed, Canceled. A regular, final or correction run pays salaries, pay items and the pay input sources; the others pay only their own inputs, so unless they name their people they pay only the people with inputs (no empty statements).

A run added without a title is named by `PayRunTitleHandler` from its type, period or pay date, schedule and people (`PayRunTitles.Build`): "Pay for Nov 16 – Nov 29, 2026 (Weekly)", "Pay for Nov 16 – Nov 29, 2026: Jane Doe", "Final pay for Jane Doe, paid Dec 5, 2026". A run added before titles is shown as "Pay run {number}" (`PayRunTitles.Name`). `PayRunPeopleHandler` refuses a run that names someone with no pay profile with its employer. The run's editor offers **All staff on the pay schedule** or **Only the staff members I choose** (Staff's `StaffPicker`).

`IPayRunService`:

- `CalculateAsync(run, user, staffMemberIds)` calculates a draft `PayStatement` per active profile of the employer on the schedule (or per named person), with the source inputs and the run's own `PayRunInput`s, under a distributed lock on the run; a subset recalculates only those people. Issues are the profiles' blockers and the calculation's warnings; the totals are gross, both sides' taxes, deductions, contributions, reimbursements, net and cash needed. A run waiting for approval or approved goes back to Calculated, and its pending request is cancelled: an approval is for one calculation (the request carries the run's version).
- `SubmitAsync(run, user)` asks Approvals' `PayRunApproval` policy (seeded by the migration with the `ApprovePayRuns` permission as approver) when the run is calculated and has no blocker. The request is limited to the employer (`AccessScope`, and the `PayRun` source access answers by the payroll scope), adds the employer's own `ApprovePayRuns` permission to the request's approvers, and names the preparer as a subject who may not decide unless **Settings** → **Payroll** allows it (which also sets the policy's requester and subject rules).
- `PayRunApprovalTypeHandler`, the **Pay run** approval type (`PayRun`), adds the approvers of the run's employer to every stage, refuses an approval of a run that changed since it was asked, and takes the decision: approved for the current version moves the run to Approved; rejected or cancelled returns it to Calculated. The `PayRunApproval` policy serves the PayRun type and can have stages.
- `CancelAsync` cancels an open run and removes its drafts.
- `ProcessAsync(run, user)` processes an approved run (or a partly paid or failed one again) under the run's lock: each draft gets a number from the `Payroll:PayStatements` sequence, the printed addresses (`IAddressFormatter`) and the status Final; pay items move (`PaidTowardGoal`, and `ArrearsBalance` for a code that carries arrears forward, the value before kept on the line as `ArrearsBefore`); the lines' source inputs are marked consumed under `payrun:{runId}`; each payment of net pay is paid by the `IPayStatementPaymentMethod` of its `NetPayMethod`, keeping its reference or its problem; a statement whose payments are all paid is Paid; the run is Paid, PartiallyPaid or Failed. `IPayStatementObserver`s hear each issued statement.
- `VoidStatementAsync(statement, user, reason)` stops the payments that have not gone out (`IPayStatementPaymentMethod.VoidAsync`), creates a numbered reversing statement (`VoidOf`) with the opposite lines, so the accumulators cancel, moves the pay items back, releases the source inputs, marks the original Voided (`VoidedBy`) and updates the run's totals.
- `ReissuePaymentAsync(statement, index, user, reason)` stops a payment and pays it again; `PayStatementPayment.Attempt` goes up, and is part of the idempotency key of the new disbursement.

Payment methods light up with Banking's features (see [Banking](./banking.md)); Payroll has no payment feature of its own, they are startups of the Payroll feature:

- With Banking's **Direct Deposit** (`CrestApps.OrchardCore.Banking.DirectDeposit`) on, `DirectDepositPaymentMethod`: an ACH `Disbursement` from the employer's `FundingBankAccountId` to the row's bank account, source kind `PayStatement`, access scope the employer, effective on the pay date, idempotency key `payroll:{statementId}:{index}` (plus `:{attempt}` after a reissue).
- With Banking's **Checks** (`CrestApps.OrchardCore.Banking.Checks`) on, `PaycheckPaymentMethod` (a check on the funding account) and `PayStatementCheckVoucherSource`, which prints the statement as the check's voucher in sections (`CheckVoucherSection`): earnings with hours, rate, current and year-to-date amounts and gross pay, then taxes and deductions with current and year-to-date amounts, then net pay and how it was split. A deposit with no Direct Deposit is paid by check.

`PayStatementSourceAccess` (source kind `PayStatement`) limits those disbursements and checks to the employers whose runs a user may see (`ViewPayRuns`) or process (`ProcessPayRuns`). `PayStatementNotificationObserver` sends the `PayStatementReady` notification type (registered when Notifications is on) to the staff member's user, with a link to the statement in My Pay (`MyPayUrls`) and no amount, and removes it when the statement is voided. `PayStubDocument` writes a statement as a PDF with Pdf.Core's standard fonts; `PayStubService` gives it the reader's texts.

`PayRunInputImporter` saves rows (run number, employee number, pay code, hours or amount, rate, date, note) as run inputs and refuses a row with the reason; the **Save pay inputs** pipeline step (a destination: rows in, **Rejected** out with an `Error` field, like **Save content items**) uses it, so a file of bonuses read with **Read a media file** goes straight into a run. The run is named by a `PayRun` field or the `PayRun` run parameter; the user the pipeline runs for must prepare the employer's runs. Runs already calculated are calculated again for the people whose inputs changed.

**Payroll** → **Pay Runs** lists the runs of the employers the user may see (`ViewPayRuns`) by title, with the number as a badge, and with an employer filter when Multiple Employers is on. A run's page shows its totals, issues, inputs (add or remove, `PreparePayRuns`; nobody enters their own pay), the register of statements and **Calculate**, **Submit for approval** and **Cancel the run** in the action bar; people who may approve see **Approve** and **Reject** there (decided through Approvals), and people who may process an approved run see **Process and pay** (`ProcessPayRuns`). A statement's page shows its number, lines and payments with their references and problems, **Download the stub** (`ViewPayRuns`), and **Reissue** and **Void** (`VoidPayStatements`).

## My Pay

The **My Pay** feature (`CrestApps.OrchardCore.Payroll.SelfService`, depends on Payroll) gives a person their own pay in the **Pay** section of their own staff profile (`MyPayProfileDisplayDriver`, the `MyPayProfile` shape, built by `MyPayPageBuilder`), linked from **Staff** → **My Profile** (`IStaffHomeLinkProvider`, shown with `ViewOwnPay`). `~/Admin/my-pay` sends a staff member there and tells a user not linked to one. The card shows only on the person's own profile, never on someone else's. `MyPayController` reads only the records of the staff member linked to the signed-in user (`IStaffDirectory.FindByUserIdAsync`): someone else's statement or profile, and a draft statement, are not found.

- The section lists the person's statements that are not drafts (an `<admin-list>` of `MyPayStatementRow`s with SummaryAdmin and SummaryAdminGrid rows), their pay profiles, and a link to Banking's **My bank accounts** when Banking is on and they may manage their own accounts.
- A statement page (the lines through the shared `PayStatementLines` partial, the payments with their references) and its stub (`PayStubService`).
- **Pay details** (`ManageOwnPayDetails`): the pay profile editor in the `MyPay` group (`PayrollConstants.MyPayEditorGroup`), in which `PayProfileDisplayDriver` shows and reads only the tax elections (a new one counts from today by default) and the net pay split; nothing else of the profile changes. A save tells the `IPayProfileObserver`s ("Changed in My Pay"), so Audit Trail records it.
- `PayDetailsChangedNotificationObserver` sends the `PayDetailsChanged` notification type to the person whenever their elections or net pay split change, whoever changed them: a security notice that names who changed it, never an account number.

## Integration

- **Observers.** `IPayRunObserver` hears each step of a run (Opened through `IPayRunService.OpenAsync`, Calculated, Submitted, Approved, Rejected, Paid, PartiallyPaid, Failed, Canceled) after it is saved; `IPayStatementObserver` hears issued and voided statements; `IPayProfileObserver` profile changes. Workflows, webhooks and the audit trail hang off them.
- **Workflows** (with Workflows on): the events Pay Run Opened, Calculated, Submitted for Approval, Approved, Rejected, Processed, Paid, Failed and Canceled (`Note` is the reason), Pay Statement Issued and Voided, and Pay Profile Changed, started after the change is committed; the tasks Open Pay Run (once per employer, schedule, type and period: `Exists` otherwise), Add Pay Input (as a file row, through `PayRunInputImporter`), Calculate Pay Run (`Blocked` when a person has a blocker; with **Write the run's amounts to the output** (`IncludeTotals`), Gross, Net and CashNeeded are written only when the workflow's user may view the employer's payroll reports, never for a timer; the run events carry no amounts), Submit Pay Run for Approval, Approve and Reject Pay Run (as the person the workflow runs for, under the policy) and Process Pay Run (`Paid`, `PartiallyPaid`, `Failed`). A returned deposit starts Disbursements' **Disbursement Returned** event (source kind `PayStatement`) and a changed bank account Banking's **Bank Account Changed** event. The **Automation: open and calculate pay runs on schedule** recipe adds, turned off, a workflow that opens, calculates and submits the period's run every weekday morning and tells the payroll administrators when it has blockers. Stubs are not stored by a task: they hold people's pay and are written on request (the media library serves its files publicly).
- **Data source** `Payroll` (with Data Pipelines Core): the data sets Pay inputs, Employees, Pay runs, Pay statements, Earnings, Deductions, Taxes, Employer contributions, Payments of net pay (no bank account) and Year-to-date balances, read by `PayrollReportReader` for the employers in the user's `ViewPayrollReports` scope (implied by `ExportPayrollData`; seeing runs on their pages is not enough), filtered by pay date, and Journal entries for the user's `ExportPayrollData` scope (see [Payroll journal](#payroll-journal)).
- **Reports** (Workforce & Payroll, with Reports on): Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution and Year-to-date Earnings, for `ViewPayRuns`, exported or attached to a subscription only with `ExportPayrollData` (an export reads only the employers of both scopes). A report runs for `ReportContext.User` when it has one (a subscription runs as its owner, and is not sent once the owner may no longer view it) and otherwise for the request's user; with neither it reads nothing. The dashboard widgets **Payroll Cash Requirement** and **Payroll Summary**.
- **Webhooks** (with Webhooks on): `payroll.payrun.approved`, `payroll.payrun.processed` (ids, dates, status and the number of statements, never an amount), `payroll.statement.issued` and `payroll.statement.voided` (ids and dates, never a person's pay).
- **Calendar** (with Calendars on): the `Payroll.PayDates` source shows the pay date of each run of the employers a person may see, and a staff member's own coming pay dates on their personal calendar; all day and not busy.
- **Audit Trail**: pay run submitted, approved or rejected, processed, cancelled, and pay statement voided, with the run or statement number; never a person's pay.
- **Notification types**: Pay statement ready, Pay details changed, Deposit of pay returned (with Banking's Direct Deposit) and Bank account changed (a staff member's account added or its number changed, with Banking on). None carries an amount or an account number.
- **Personal data**: a person's pay profiles (the last characters of the tax id, never an account number) and final statements are exported; anonymizing changes none of them, since the employer must keep payroll records as long as tax law requires.
- **Pay history**: the opt-in `PayrollPayHistory` deployment step exports final statements (without runs, bank accounts, disbursements and checks) and the recipe step imports them into a historical run per employer and pay date, paid and read-only (a statement imported as history can't be voided or paid again), skipping statements already there. The step calls `IPayHistoryImporter` (`CrestApps.OrchardCore.Payroll.Abstractions`; `PayHistoryImporter` in Core), which an app calls from code too, such as a background job that brings over another system's statements: `ImportAsync(IEnumerable<PayHistoryEntry> entries, string importedBy = null, CancellationToken)`, each entry a `PayStatement` with an optional `EmployeeNumber`, returns a `PayHistoryImportResult` (`Imported`, `Skipped`, `Errors`). It writes to the stores directly, so pay run and statement observers aren't told, and the changes are saved with the unit of work.

## Taxes

Taxes on income belong to Taxation's **Income Tax** feature (see [Taxation: Income tax](./taxation.md#income-tax)): the income tax rules, their page (**Commerce** → **Taxation** → **Income Tax Rules**, Manage income tax rules), the provider seam, election forms, withholding methods and the country packs. Payroll depends on it and never owns a rule.

The taxes step (`TaxesPayCalculationStep`) maps a statement to Taxation's neutral `IncomeTaxRequest`: the employer's registered jurisdictions and its registrations (each account id and the rate it has on the pay date, `Registrations`), the income kind (employment, or contractor for a contractor's profile), the person's home and work jurisdictions (home: `IPayrollHomeJurisdictionResolver`, by default `PayrollHomeJurisdictionResolver`, gives the country, the country with its region and the city of the home address by the convention `{Country}-{Region}-{CITY}`, the city upper-case with letters and digits only, such as `US`, `US-MI`, `US-MI-DETROIT`, plus the code of every Taxation tax jurisdiction the address matches that starts with the country, such as a county; work: the work site team's jurisdictions, else the employer's `WorkJurisdictions`, `PayrollWorkSites`), the elections in effect on the pay date (the values stay on `EmployeePayProfile.Elections` as Payroll data; the fields come from the country pack), the periods a year of the schedule, the regular and supplemental wages of each tax category from the treatment matrix, the supplemental method, the year's wages before the pay from the pay codes' totals, and what each tax came to earlier (`TaxesToDate`, from the accumulators). It maps the result back to tax lines: the payer, the taxable and flat wages, and the jurisdiction the tax is remitted to (`PayStatementLine.RemittedTo`, when another agency collects it). **Settings** → **Payroll** names the provider (`PayrollSettings.TaxProvider`; empty uses the only one there is, or the only one besides the built-in income tax rules, so a tax engine turned on is the one used; with several engines and none named, nothing is withheld until one is chosen, `TaxesPayCalculationStep.ChooseProvider`) and the least net pay. With no provider, nothing is withheld and the calculation says so. A contractor has nothing withheld, except contractor withholding.

A second pay in a period (a bonus paid on its own, a correction) is withheld, for an annualized rule or a withholding method that doesn't work out supplemental pay itself, on the period's total less what the period's earlier pays were taxed on (`PayAccumulator.PeriodTaxableWages`), the IRS aggregate method for supplemental pay paid separately; extra withholding is added once a period. Canada's methods use the CRA's bonus method instead. A pay with no regular earnings of its own (a bonus paid alone) carries a period of the person's regular pay (`IncomeTaxRequest.RegularIncomePerPeriod`: the regular earnings of their latest earlier statement, else their salary over the periods of a year): the bonus method adds the bonus to a year of it, and the aggregate method adds it to that period when no earlier pay of the period had regular pay, instead of withholding as if the bonus were all the year's pay.

## Retro pay, tips, earlier weeks, gross-up and an extra pay date

- **Retro pay** (`RetroPayCalculationStep`, order 110, regular runs only): for each earlier final statement of the year, an hours line of a code with **In retro pay** paid below the rate now on file for its period (times the code's multiplier), and a full period's salary below the salary now on file, owe the difference, less what earlier retro lines (`PayStatementLine.RetroFor`) already paid for that statement. Only raises are paid. The context factory loads the person's final statements with the employer (`PayCalculationContext.History`) for regular runs and for amounts earned in earlier weeks.
- **Tip credit** (`TipCreditPayCalculationStep`, order 120): for a profile marked **Paid a tipped wage** (`EmployeePayProfile.Tipped`), each week's cash wage of the hours worked plus the tips that count (at most `PayrollSettings.MaximumTipCredit` an hour) must reach `PayrollSettings.MinimumHourlyWage` for those hours; what falls short is paid on the code of kind `MinimumWageMakeup` (seeded `TIPMU`). Tips of a week are the tip lines dated in it, undated ones spread by hours. Cash tips the person kept are reported on `CTIPS` (imputed income of kind `Tips`): taxed, not paid.
- **Overtime of earlier weeks** (`EarnedRegularRatePayCalculationStep`, order 210): an amount of a code that counts in the regular rate with `EarnedFrom`/`EarnedTo` before the period (a run input's **Earned from** and **Earned to**) raises the regular rate of those weeks: the amount divided by all hours worked in the statements of those weeks, times each overtime hour's multiplier less one, paid as the premium of the overtime code with the most hours (29 CFR 778.209-210).
- **Gross-up**: a run input marked **The amount is what the person takes home** (`PayRunInput.IsNet`) is grossed up: the pay is worked out without it, then with the earning raised until net pay is that much more (one a person a run; others are left out with a warning).
- **An extra pay date**: a schedule's `ExtraPayPeriod` is `Standard` (each pay is the salary divided by the usual number of pay dates, so a year with 27 every-two-weeks pay dates pays one more) or `DivideByPayDates` (the salary is divided by that year's pay dates, `PayCalculationContextFactory.PayDatesIn`). Taxes keep annualizing by the usual number.

## Garnishments

**Garnishments** (`CrestApps.OrchardCore.Payroll.Garnishments`, on Payroll) keeps `GarnishmentOrder` records (`IGarnishmentOrderManager`, in the Payroll collection, `PayrollGarnishmentOrderIndex` by employer, profile, staff member and status): the pay profile (its employer and staff member follow it on every save), the kind (`Support`, `FederalTaxLevy`, `StateTaxLevy`, `Bankruptcy`, `StudentLoan`, `Creditor`, `Other`), case number, agency, a deduction pay code, an amount and/or a percent of disposable earnings each pay, a limit percent, the pay left each week (`ProtectedWeeklyAmount`), the exempt amount of a levy, the total owed and what was withheld, a priority, the received, first and last pay dates, the status (`Active`, `Suspended`, `Completed`, `Released`), and the payee (name, mailing address, paid by `Check`, `BankAccount` with a Banking account owned by the payee, or `Outside`). `GarnishmentDefaults` gives each kind its United States federal limit and priority, which an order may change: support 50% (to 65%), a student loan 15%, a creditor 25% and 30 times the federal minimum wage a week (217.50) left; support first, then tax levies, bankruptcy, student loans and creditors. The feature seeds the `GARN` pay code (kind `Garnishment`, cut last by the net check).

The calculation step (`GarnishmentsPayCalculationStep`, order 750, after the deductions that are a percent of disposable earnings) takes each order in effect on the pay date, lowest priority number first, then by the day received: disposable earnings are gross pay less the employee's taxes; support orders share their own percent; a tax levy takes what is left above its exempt amount; a bankruptcy order takes its amount; every other order shares the creditor limit (the smaller of its percent and what is left above the pay a week, less what the orders before it took); an order stops at its total owed. A line (`PayStatementLine.GarnishmentOrderId`) is added per order, with a warning when a limit held one back.

`GarnishmentStatementObserver` (an `IPayStatementObserver`) moves `Withheld` when a statement is issued, under the order's lock (`Payroll:Garnishment:{order}`) and capped at what the order still owes (an excess is flagged on the remittance), and sends the withholding to the payee from the employer's funding account, by a check (Checks) or an ACH disbursement (Disbursements). The payment's source is the remittance (source kind `PayrollGarnishment`, id `{order}:{statement}`, scoped by the employer like a statement; `GarnishmentCheckVoucherSource` prints only the case, the person, the pay date, the period and the amount; idempotency key `payroll-garnishment:{statement}:{order}`, with `:{attempt}` on a retry). Each is recorded in `Remittances` with its reference or problem; `RetryAsync` (the order page's **Send again**, `ManageGarnishments`) sends a failed one again. A void stops the payment, then gives the withholding back and reopens a completed order; a payment that can't be stopped keeps the withholding on the order, flagged. With Workflows on, `IGarnishmentObserver` starts the events **Garnishment Withheld** (after the payment to the payee was made or failed; `Problem` says why), **Garnishment Order Completed** (the order owes nothing more) and **Garnishment Remittance Voided** (the statement was voided and the payment stopped), with the input `OrderId`, `EmployerId`, `StaffMemberId`, `Type`, `CaseNumber`, `Agency`, `PayeeName`, `Status`, `Withheld`, `TotalOwed`, `Remaining`, `StatementId`, `StatementNumber`, `PayDate`, `Amount`, `Currency`, `Method`, `ReferenceId`, `Reference`, `Problem` and `Change`. **Garnishment Order Created** starts when an order is added (input `OrderId`, `EmployerId`, `StaffMemberId`, `ProfileId`, `Type`, `CaseNumber`, `Agency`, `Status`, `TotalOwed`, `PayeeName`, `PaymentMethod`, `StartDate`). Support limits follow `GarnishmentDefaults.SupportLimitPercent` (60, or 50 with `OtherDependents`, plus 5 for `ArrearsOver12Weeks`); a federal levy needs `ExemptAmount`; a payee's account is never a staff member's or a user's.

Pages: **Payroll** → **Garnishments** (`ManageGarnishments`, per employer) lists the orders of the employers a user manages, with an editor of three cards (the order, how much, the payee) plus the payee's address and what was sent; nobody manages their own order unless the setting allows changing one's own pay; an order that withheld anything is released, never deleted. The `PayrollGarnishments` recipe step (`Orders`, matched by id) and the **Garnishment orders** deployment step (fails closed without a user, for `ExportPayrollData`; case numbers and payee accounts only with **Include sensitive data**; never what was withheld or sent) carry them, and the **Garnishments** data set (for `ExportPayrollData`) has one row per order, with the case number and court only for `ManageGarnishments` of the employer.

## Contractors

A pay profile of worker type **Contractor** is paid by a **Contractors** pay run (or with others) from the amounts entered, with no pay rate needed and nothing withheld, except contractor withholding: the tax category `IncomeTaxCategories.ContractorWithholding` (registered by United States Income Tax) applies only to contractors, and only when the profile's `BackupWithholding` is on or no tax id is on file (`IncomeTaxRequest.ContractorWithholding`); employees never have it. The United States recipe adds `US-BWH`, backup withholding at 24%. Backup withholding is its own liability (`TaxLiabilityGroups.NonPayrollWithholding`, deposited apart) and is filed on **Form 945** (`US-945`: box 2 backup withholding, box 4 deposits, the balance or overpayment), never on Form 941. Contractors are left out of Forms W-2, 941 and 940; **Forms 1099-NEC and 1096** (`US-1099NEC`) list each contractor paid at least the year's threshold (2,000 from 2026, 600 before: the form's `ReportingThreshold` yearly value) or withheld on, with box 1 (payments) and box 4 (backup withholding) and the 1096 count and totals, a contractor with no tax id noted, a 1099-NEC PDF per contractor (also in My Pay) and the same filing, amending and notice as the W-2.

## Liabilities, filings and year end

Payroll calculates and prepares; the business deposits and files (decision 4).

- **Liabilities** (`ITaxLiabilityService`, derived, never stored): the tax lines of the final statements (both sides, reversals with their pay date) by the jurisdiction they are remitted to (`RemittedTo`, else their own) and group (`TaxLiabilityGroups.Withholding`, `Unemployment`) into periods of the registration's `DepositFrequency` (`DepositSchedules.Monthly`, by the 15th of the next month; `SemiWeekly`, Wednesday to Friday by the next Wednesday and Saturday to Tuesday by the next Friday; `Quarterly`, by the end of the next month; `QuarterlyMidMonth`, by the 15th; `TwiceMonthly`, the 1st to the 15th by the 25th and the rest by the next 10th; `FourTimesMonthly`, by the third working day after each part; `Annual`; US unemployment, category `Unemployment`, always by quarter), with the `TaxDeposit` records the business enters (`ITaxDepositManager`), the balance and the due date. Due dates are not moved off weekends and holidays, except that the working days of `FourTimesMonthly` skip weekends.
- **Filings** (`PayrollFiling`, `IPayrollFilingManager`, `IPayrollFilingService`): a filing is prepared from a [tax form](#tax-forms) (`FormsForAsync` lists the enabled forms of the jurisdictions an employer is registered in) over the period's final statements, pay codes, profiles and deposits. A filing is prepared as a draft (prepared again on request), marked filed with the day and the agency's confirmation (never changed again; people with a statement in it get the `TaxFormReady` notification, linking to My Pay), and amended by a new draft that names it (`AmendsId`); filing the amendment marks the first `Amended`. Rule C: a filed form and a deposit counted in one are kept.
- **Workflows** (with Workflows on): `IPayrollFilingObserver` hears each step, and starts **Payroll Filing Prepared**, **Payroll Filing Filed** and **Payroll Filing Amended** (the new draft; `AmendsId` names the filing it corrects) with the input `FilingId`, `EmployerId`, `Form`, `Year`, `Quarter`, `Period`, `Status`, `FiledOn`, `Reference`, `AmendsId`, `People` (a count), `Change` and `Actor`, and **Payroll Year Closed** and **Payroll Year Reopened** with `EmployerId`, `Year`, `Change` and `Actor`. **Tax Deposit Recorded** starts when a deposit is added (input `DepositId`, `EmployerId`, `Jurisdiction`, `Group`, `PeriodStart`, `PeriodEnd`, `Amount`, `PaidOn`, `Reference`, `RecordedBy`).
- **Year end** (`YearEndBlockersAsync`, `CloseYearAsync`, `ReopenYearAsync`): a year closes once every pay run of it is paid or cancelled; closing prepares the year's per-person forms and adds the year to `Employer.ClosedYears`, after which no run with a pay date in it is opened, calculated or processed and no statement of it is voided.
- Pages: **Payroll** → **Liabilities and Filings** (`ManagePayrollFilings` per employer): **Liabilities** (the periods and the deposits as `<admin-list>`s, a deposit form), **Filings** (the list, **Prepare**, a filing's page with its boxes, the people's statements as PDFs, **Mark as filed**, **Prepare again**, **Amend**), **Year end**. My Pay lists a person's filed forms with a download. Data sets **Tax liabilities**, with United States Payroll Filings **W-2 wage records**, and with Canada Payroll Filings **T4 slips**; the `PayrollTaxDeposits` and `PayrollFilings` recipe steps and the **Tax deposits and filings** deployment step (fails closed without a user, `ExportPayrollData`; each filing's people, with their names, addresses and boxes, only with **Include people's wage statements** for `ViewTaxIds` of the employer).
- **United States forms**: **Form 941** (lines 1 to 15: employees paid in the pay period of the 12th of the quarter's last month, wages, income tax, Social Security and Medicare wages with the form's rates, the fractions-of-cents adjustment against what was withheld and matched, deposits, balance or overpayment), **Form 940** (payments, exempt payments, payments over the federal unemployment wage base, taxable wages and tax at the `US-FUTA` rule's rate, deposits) and **Forms W-2 and W-3** (boxes 1 to 7, boxes 8, 10, 11 and 12 from pay codes that report to `W2:12D` and the like; boxes 15 to 20 from the employee's income tax lines of each jurisdiction below the country, the form's **List the income taxes below the country**, `PayrollFilingSums.JurisdictionAmounts`: the state and the employer's state id from its registration there (the one for `IncomeTax` first), the state wages and income tax, and for each local jurisdiction its wages, income tax and locality name (the code after the region, `DETROIT` for `US-MI-DETROIT`), kept on `PayrollFilingPerson.JurisdictionAmounts` and printed as rows on the W-2 PDF; the W-3 totals, with state and local wages and tax in boxes 16 to 19 from `REGIONWAGES()` and the like, and count). A state or local tax with nothing withheld (an exempt person) has no line, so its wages aren't on the W-2. The **SSA EFW2** file format (`efw2`, `Efw2DataFileFormat` over `UnitedStatesEfw2`) writes the W-2 wage records data set as RA, RE, RW, RT and RF records of 512 characters for one tax year (no RS state records yet); the submitter's SSA user id is the account id of an employer registration whose agency is `SSA`. The full social security number is read only for `ViewTaxIds` of the employer, each one read is recorded, and the data set carries it encrypted (`ProtectedSsn`, a data protector that expires in a day, `FileTaxIds`), so a preview, a CSV or any other file shows ciphertext and only the EFW2 format reads it back; a file with a row without one is refused, never written with zeros. The data set reads one W-2 filing an employer and year: the filed one, or the draft (a first one being prepared, or an amendment when the `Draft` parameter is `true`). RA position 500 is `L` (self-prepared), the retirement plan indicator follows box 12 codes D, E, F, G, H and S, and a box the file doesn't write (other box 12 codes, boxes 8 and 11, in the `OtherBoxes` field) refuses the file with the people and codes named. The **United States Payroll: W-2 wage records as an SSA EFW2 file** recipe adds the draft pipeline (no destination: add one that keeps the file private).
- **Canadian forms**: see [Canada](#canada).

## Tax forms

Payroll's forms are Taxation [tax forms](taxation.md#tax-forms) whose source is **Payroll** (`PayrollConstants.TaxFormSource`); building them and every formula function are in the user manual's [Tax forms](../user-manual/finance/tax-forms.md). Payroll adds:

- **The source's functions** (`PayrollTaxFormFunctions`, with `PayrollTaxFormData`): `WAGES`, `TAX`, `TAXABLE`, `HASTAX` (tax codes take `*`), `PAYKIND`, `PAYCODEFIELD`, `GROSS`, `HEADCOUNT`, `DEPOSITS`, `RULE`, `ACCOUNT`, `REGIONWAGES`, `REGIONTAX`, `LOCALWAGES` and `LOCALTAX`, and with Canada Payroll Filings `DENTALCODE` (`CanadaTaxFormFunctions`).
- **Recipients**: each reported person (grouped by pay profile), or each person and province for a form with a statement per jurisdiction (`PayrollFilingSums.ByJurisdictionUnder`); a recipient field reads the person's final statements of the period, a filing field every reported statement. Fields that accept reported amounts add the lines of the pay codes whose **Tax form fields** name them (`PayCodeFieldPrefix` and the field's number, such as `W2:12D`).
- **The Payroll card** of the tax form editor (`PayrollTaxFormSettings` in the form's properties): whose pay the form reports (`Workers`), `PeopleJurisdiction`, `ListJurisdictionAmounts` (a W-2's boxes 15 to 20, `PayrollFilingPerson.JurisdictionAmounts`) and `PayCodeFieldPrefix`.
- **Filings**: `PayrollTaxFormFiling` wraps each enabled form of the Payroll source as an `IPayrollFilingForm` and keeps the result as a `PayrollFiling` (boxes, people, notes); a person's statement prints as a PDF from the form's statement settings (`PayrollTaxStatementDocument`). `PayrollTaxFormUsage` tells the Tax Forms page which forms have filings.
- **The country forms** ship as recipes, **United States Payroll: federal tax forms** (`payroll-united-states-tax-forms.recipe.json`) and **Canada Payroll: T4 and RL-1 tax forms** (`payroll-canada-tax-forms.recipe.json`). Turning on United States or Canada Payroll Filings, or updating a site that had it on, adds the forms of its recipe that the site doesn't have yet (the feature's migration, with Taxation's `TaxFormRecipeSeeder`). A module can still add a form in code with `services.AddPayrollFilingForm<T>()`; a tax form of the same name takes its place.

## United States

**United States Payroll Filings** (`CrestApps.OrchardCore.Payroll.UnitedStates`) adds Forms 941, 940, 945, W-2 and W-3, 1099-NEC and 1096 as tax forms from its recipe, and registers the W-2 wage records data set and the EFW2 file format. It depends on Taxation's **United States Income Tax**, which has Form W-4 as the election of the `US` jurisdiction and its `UnitedStatesFormW4` withholding method (IRS Publication 15-T, Worksheet 1A), backup withholding, and the **United States Income Tax: federal rules for 2026** recipe (`US-FIT`, `US-SS-EE` and `US-SS-ER`, `US-MED-EE` and `US-MED-ER`, `US-MED-ADD`, `US-FUTA`, `US-BWH`). Form 940 reads the `US-FUTA` rule's wage base and rate from Taxation's rules. State and local taxes are income tax rules: a state that withholds by its guide's annualized method uses United States Income Tax's **State income tax (withholding certificate)** rule, with each person's state certificate as the election of the state; a city's tax is a flat rate rule with residency, an exclusive group and the state certificate's exemptions (see Taxation's [United States: state and local taxes](taxation.md#united-states-state-and-local-taxes)); or they come from a tax engine's provider.

## Canada

**Canada Payroll Filings** (`CrestApps.OrchardCore.Payroll.Canada`) depends on Taxation's **Canada Income Tax**, which works out federal, provincial and territorial tax, Quebec tax, CPP, CPP2, EI, QPP, QPP2 and QPIP (see [Taxation: Country packs](./taxation.md#country-packs)).

- **Province of employment**: an employee of an employer registered in `CA` needs exactly one province of employment, from their work site's jurisdictions (`CanadaProvinceOfEmploymentBlockers`, a pay profile blocker, with Taxation's Canada Income Tax on). Taxes never fall back to where the person lives.
- **Registrations**: the employer's `CA` registration carries the CRA business number with its payroll program account (`123456789RP0001`) and the remitter type as its deposit frequency; its `CA-QC` registration carries the Revenu Québec identification number (`1234567890TQ0001`). Provincial tax and EI at Quebec's rate are remitted under `CA` (their rules are collected by `CA`); Quebec tax, QPP and QPIP under `CA-QC`. EI and QPIP use the `InsurableEarnings` category, so they are remitted with the other source deductions.
- **T4 slips and T4 Summary** (`CA-T4`, a tax form of the Canada recipe): per person, boxes 14 (income tax wages), 16 and 16A (CPP, CPP2), 17 and 17A (QPP, QPP2), 18 (EI), 22 (federal tax and the provincial taxes the CRA collects, never Quebec's), 24 (EI insurable earnings, within the year's maximum insurable earnings), 26 (CPP or QPP pensionable earnings before the basic exemption, within the YAMPE as RC4120 asks since 2024; the form's `MaximumInsurableEarnings` and `MaximumPensionableEarnings` yearly values hold each year's figures), 55 and 56 (QPIP); the Summary adds 19 (employer's EI), 27 and 27A (employer's CPP and CPP2), 80 (total deductions), 82 (remittances to `CA`), 84 or 86 (overpayment or balance) and 88 (slips). A person employed in two provinces in the year has a slip for each, and box 10 (the province of employment, from the statements' provincial tax or QPP) is stored on the slip when the filing is prepared (`PayrollFilingPerson.Jurisdiction`). A missing or malformed RP account, or a missing dental benefits code, is noted by the form's checks. Box 45 (dental benefits, codes 1 to 5) is the employer's **Canada** card (`CanadaEmployerSettings.DentalBenefitsCode`).
- **RL-1 slips and RL-1 Summary** (`CA-RL1`, a tax form of the Canada recipe): per person, boxes A, B.A, B.B, C, E, G, H and I; the Summary adds the employer's QPP, QPP2 and QPIP, its health services fund contribution (`HSF`, the `CA-QC-HSF` tax), what is owed, the remittances to `CA-QC` and the balance.
- **T4 XML**: the **T4 slips** data set (for `ExportPayrollData`; one row per slip; the full SIN only for `ViewTaxIds`, each read recorded, and only encrypted, in `ProtectedSin`) and the `t4xml` file format (`T4XmlDataFileFormat` over `CanadaT4Xml`) write the CRA's Internet file transfer XML: `Submission`, `T619`, `Return`, `T4Slip` with `T4_AMT`, and `T4Summary` with `T4_TAMT`, for one employer and one tax year. The file always writes boxes 24 and 26 (zero included), box 45 (`empr_dntl_ben_rpt_cd`) and leaves out optional elements with nothing in them. A slip without a full SIN, with a SIN that fails its check digit or without a province of employment, or an employer without an RP account, a contact name and phone or a dental benefits code, refuses the file. The required elements are tested one by one; the file is not validated against the CRA's XSD. The **Canada Payroll: T4 slips as a T4 XML file** recipe adds the draft pipeline.
- T4 and RL-1 slips print as PDFs and show in My Pay like W-2s.
- A SIN entered on the pay profile of a person whose employer is Canadian (its country pack is `CA` or it is registered in `CA`) must pass the mod-10 check digit (`CanadaSin`).
- Not yet: the Record of Employment, Canadian direct deposit (CPA Standard 005; pay by check), the commission methods, and Quebec's other employer contributions (labour standards, workforce skills).

## Inputs from other modules, balances and the employee review

A pay run takes the inputs of every `IPayInputSource` for its people and period, and marks what it took when it is processed (a void gives it back). Besides the time clock's locked timesheets:

- **Time-off payouts** (Workforce Time Off, with Pay Setup on): each ledger entry of kind `Payout` on or before the period's last day becomes hours on the time-off type's pay code (else the first pay code of kind `PaidTimeOff`), at the staff member's rate; a type kept in days pays days times its hours per day. Source kind `TimeOffPayout`; the entry is marked with the run.
- **Tips** (Point of Sale, with Pay Setup and Staff on): the tips a cashier declares when closing a shift (`CardTips`, `CashTips` on `RegisterShift`, from the close API or the shift page) become an amount on `TIPS` (paid) and on `CTIPS` (cash tips reported, taxed and not paid), for the staff member who opened the shift, on the day it closed in the site's zone. Source kind `RegisterShift`; tips payroll took can't change.
- **Commissions** (Orders, with Pay Setup and Staff on): each paid order a staff member made (`Order.StaffUserId`, at a register or as a draft) on a day of the period earns their `StaffPayRates.CommissionRate` (Staff compensation) percent of the subtotal less discounts and refunds, on the first pay code of kind `Commission`. Source kind `Order`.

`IPayStatementBalanceSource` gives the balances a statement shows; a calculation stores them on the statement (`PayStatement.Balances`) as of the pay date, shown on the statement page, in My Pay and on the PDF stub. Workforce Time Off gives each paid time-off type the worker may see.

With **Employees review their coming pay before the run is submitted** (`PayrollSettings.EmployeeReview`), a calculation sends each person whose net pay changed since they were last asked a **Review your coming pay** notice (`PayReviewRequested`); My Pay lists their draft statements of calculated runs, and the review page (`my-pay/review/{id}`) takes **This looks right** or **Ask about it** with a note (`PayStatementReviewService`, stored as `PayStatement.Review`). A question tells the run's preparer (`PayReviewQuestioned`). The run page shows each answer on its statement, lists open questions, and asks before **Submit for approval** while one is open; an answer is out of date once a calculation changes the net pay. Nothing is blocked.

## AI tools

The **Payroll AI Tools** feature (`CrestApps.OrchardCore.Payroll.AI`, depends on Payroll and AI Services) is off by default: once on, pay goes to the site's AI provider whenever a profile uses a tool. It registers two tools with `AddCoreAITool`, in the **Payroll** category of an AI profile's tools:

| Tool | Name | Mirrors | Answers |
| --- | --- | --- | --- |
| Explain my pay statement | `payrollMyPayStatement` | My Pay, `ViewOwnPay` | The person's own final statement (the latest, or `number`): each line with its code, type, hours, rate and amount, gross and net, how net pay was paid (deposit or check, never the account), the balances, and the change of each kind of line since their statement before it (`PayStatementComparison`). |
| What changed since the last pay run | `payrollPayRunChanges` | The pay run page, `ViewPayRuns` for its employer | A run (`run`, its number) against the latest paid run before it of the same employer and pay schedule: the change in totals, who joined or left, and whose net pay changed most (25 at most) with the lines that made the difference. A run of an employer the person may not see is not found. |

Neither tool returns a tax id or a bank account number, and both run as the signed-in person.

## Payroll journal

Payroll keeps no ledger: it turns each line of a final statement into a debit and a credit (`PayrollJournal.EntriesFor`) and offers them as the **Journal entries** data set of the `Payroll` data source, for the employers in the user's `ExportPayrollData` scope, filtered by pay date. An earning debits wages expense and credits net pay owed; a deduction debits net pay owed and credits deductions payable; an employee's tax debits net pay owed and credits taxes payable; an employer's tax debits its expense and credits taxes payable; an employer contribution debits benefits expense and credits benefits payable; a reimbursement debits its expense and credits net pay owed. Imputed income and memo lines move no money and have no entry. A void's reversing statement has negative amounts, which swap the sides, so it cancels the original entries. Each entry carries the ledger code of the line's department team (`PayrollTeamSettings.LedgerCode`).

The accounts are `PayrollJournalSettings` under **Settings** → **Payroll** (card **Journal accounts**, for `ManagePayrollSettings`): one per kind, and rows that give a pay code (by its code) or a tax (by its tax code) other accounts, an empty side keeping the default. The settings travel with Orchard's `Settings` recipe step (schema `PayrollJournalSettings`). The **Payroll: export the payroll journal to a CSV file** recipe adds a draft data pipeline that groups the entries by pay date, employer, account and department and writes a CSV file; add the delivery (SFTP, web API, email) and a workflow timer.

## Teams and settings

With Teams on, people with `ManagePayrollSettings` get a **Payroll** card on the team editor: `PayrollTeamSettings` with the team's `LedgerCode` and a work site's `Jurisdictions`.

`PayrollSettings` (**Settings** → **Payroll**): `AllowPreparerToApprove` and `AllowChangingOwnPay` (both off: separation of duties; a run submitted to the **Pay run approval** policy takes `AllowPreparerToApprove` as the settings are at that moment, whether they were saved on the page, by the `Settings` recipe step, by a deployment or by code), the tax provider and the least net pay, and the default country pack and currency of a new employer.

## Index table names

Payroll keeps its records in the `Payroll` collection, and its index types carry the module's name, so no application module that keeps an index of the same generic name (such as `PayRunIndex`) in a collection of its own named Payroll shares a table with it: `PayrollPayScheduleIndex`, `PayrollPayCodeIndex`, `PayrollEmployerIndex`, `PayrollPayProfileIndex`, `PayrollPayItemIndex`, `PayrollPayStatementIndex`, `PayrollPayStatementLineIndex`, `PayrollPayRunIndex`, `PayrollPayRunInputIndex`, `PayrollTaxDepositIndex`, `PayrollGarnishmentOrderIndex` and `PayrollFilingIndex`, in the tables `Payroll_PayrollPayRunIndex` and so on. A site made before the rename keeps its rows: the Pay Setup (`UpdateFrom1`), Payroll (`UpdateFrom10`) and Garnishments (`UpdateFrom1`) migrations make each new table, copy the rows of the old one (`Payroll_PayRunIndex` and so on) in one statement, and drop the old table.

## Upgrading from the time clock's pay periods and pay codes

Before Pay Setup, the time clock kept `PayPeriodCalendar` and `PayCode` in the Workforce collection. The time clock's migration (`UpdateFrom2`) moves each one to the Payroll collection with the same id, so policies, timesheets and lines keep pointing at them:

- a pay code's old kind number becomes its name, and its window goes into `WorkforcePayCodeSettings`; unpaid codes become `Memo`, the others `Earning`;
- a code Pay Setup seeded a moment before with the same code is replaced by the old one;
- a record already in the Payroll collection with the same id is left as it is;
- the old documents and index tables are removed, so a second run moves nothing;
- roles with **Manage time clock policies** get **Manage pay setup**.
