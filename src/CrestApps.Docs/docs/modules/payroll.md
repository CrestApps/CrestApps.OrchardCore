---
sidebar_label: Payroll
title: Payroll
description: "Payroll for Orchard Core. Pay Setup owns pay schedules (periods, the overtime week and pay dates) and pay codes (class, open kind, multiplier, premium, export code), shared by Workforce's time clock, with usage checks, recipe and deployment steps, and the migration that moved the time clock's pay periods and pay codes."
user_manual:
  - user-manual/finance/pay-setup
  - user-manual/finance/payroll-employers
  - user-manual/finance/payroll-garnishments
  - user-manual/finance/payroll-filings
---

The **Payroll** module (`CrestApps.OrchardCore.Payroll`) runs payroll. Its first feature, **Pay Setup**, owns the two records both payroll and time keeping need: pay schedules and pay codes. Workforce's time clock depends on Pay Setup, and Payroll never references Workforce.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Payroll.PaySetup` | Pay Setup | none (turned on by Workforce Time Clock or Payroll) |
| `CrestApps.OrchardCore.Payroll` | Payroll | Pay Setup, Staff, Staff Compensation, Approvals (uses Teams, Banking, Recipes, Deployment and CrestApps Recipes when they are on) |

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

**Payroll > Setup > Pay Schedules** and **Pay Codes**, under `ManagePaySetup`. The schedule editor shows the next four periods and their pay dates, counted from today in the site's time zone.

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

## Employers

An `Employer` (catalog entry, `IEmployerManager`, name unique) is the legal entity that pays people: `Name`, `LegalName` (`PrintedName` falls back to the name), its own `TaxId`, `Address` (the shared address editor), `LogoPath` (media library), `CountryPack`, `Currency`, `FundingBankAccountId` (with Banking on, one of the bank accounts the employer owns: `PartyRef("Employer", id)`), signatory and contact, `Registrations` (`EmployerTaxRegistration`: jurisdiction, agency, account id, deposit frequency and dated `Rates`, `RateOn(day)`) and `Status`. Employers are a party kind (`EmployerPartySource`). A module that points at an employer registers an `IEmployerUsage`; an employer in use can't be deleted (the page and the store both refuse).

The `PayrollEmployers` recipe step (`Employers`, matched by id then name) and the **Employers** deployment step carry them; Payroll's settings use Orchard's `Settings` step (`PayrollSettings`) and a site settings deployment step.

## Permissions per employer

`PayrollPermissions` holds the Payroll feature's permissions. Every one in `PayrollPermissions.PerEmployer` also exists per employer through `PayrollPermissionTemplates.For(permission, employerId)` (named `{Permission}_{employerId}`, implied by the global permission; a per-employer "view" is also implied by the same employer's "do" permissions). `PayrollPermissionProvider` lists each employer's copies, so the role editor shows a group per employer, such as **Payroll: Contoso**.

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

- **Tax id.** `TaxId` is a `ProtectedValue` (`ISensitiveValueProtector`, purpose `PayrollConstants.TaxIdPurpose`): the page shows only its last characters. `TaxIdHash` is a keyed hash (HMAC with a key kept in the Payroll collection) of the digits and letters, so the same number typed with or without dashes is found on another profile (`IPayProfileService.FindSameTaxIdAsync`) without being readable. `RevealTaxIdAsync` returns the clear number and records the read; the page needs `ViewTaxIds` for the employer and sends the number with `Cache-Control: no-store`.
- **Elections.** `TaxElection` rows (jurisdiction, first day, values, who entered them and when) are never edited: a new row from a later day replaces the old one from that day on, and `ElectionOn(jurisdiction, day)` returns the row in effect. The values an editor asks for come from an `ElectionFieldSet` per jurisdiction (`AddElectionFieldSet`); the generic set (`*`) asks for a filing status, allowances, extra withholding and an exemption. Tax engines add their own sets.
- **Net pay.** `Distribution` is an ordered list of `NetPayRow`: a deposit to one of the person's bank accounts (Banking, owner `PartyRef("Staff", id)`) or a check, for an amount, a percent or what is left. The last row takes what is left and only the last; percents can't pass 100.
- **Blockers.** `IPayProfileService.GetBlockersAsync` lists what keeps a profile from being paid: no employer or an inactive one, no pay schedule, no tax id, no election for a jurisdiction of the employer's registrations or of the work site team, no split of net pay, no pay rate or salary on the staff member's Pay card. Banking adds a missing or unverified bank account. A module adds its own with `IPayProfileBlockerProvider`. The profile page and the staff member's **Payroll** card show them.
- **Changes.** `IPayProfileObserver` hears every tax id reveal and change, new election, change of the net pay split and save. With Audit Trail on, each is written to the Audit Trail (category **Payroll**) with the tax id's last characters only.
- **Separation of duties.** `MayChangePayOfAsync` refuses a user changing the pay of the staff member linked to their own user, unless **Settings > Payroll** allows it.

An `EmployeePayItem` is a standing amount on a profile: a pay code, an amount or a percent (one of them), the first and last day, a goal (such as a loan) with what was paid toward it, a period and a year limit, an employer match and its limit, and arrears. `AppliesOn(day)` is false before the first day, after the last and once the goal is paid. An item copies its profile's staff member and employer; a profile with items, and a pay code or employer that profiles or items use, can't be deleted.

**Payroll > Employees** lists the profiles of the employers the user may see (`ViewPayProfiles`), with an employer filter; **Pay items** opens from a profile's row and page. The staff profile gets a **Payroll** card listing the person's profiles, with their blockers.

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
| `FormBoxes` | Boxes of tax forms, from the ones a country pack registers with `AddPayrollFormBox` |

Tax categories are an open list: `PayrollTaxCategories` holds `IncomeTax`, `SocialInsurance` and `Unemployment`, and a country pack adds its own with `AddPayrollTaxCategory(name, displayName)`.

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

**Accumulators** (`IPayAccumulatorService`) are never stored: they are the sums of `PayStatementLineIndex` rows, one per line of each statement that is not a draft, by pay code (`PayAccumulators.CodeKey`) and tax (`TaxKey`): the period, month, quarter, year and lifetime amounts, the year's hours and a tax's year-to-date taxable wages. A void is a statement with negative lines, so the two cancel out. The rows keep amounts as whole minor units and hours as hundredths, in integer columns.

**Preview the next pay** on a pay profile runs the engine for the current period of its schedule with the inputs and items there are now, and shows the lines, net pay, its split and the warnings, without keeping anything (`ViewPayProfiles` for the employer).

## Pay runs

A `PayRun` is one payroll of one employer (`IPayRunManager`): a number from the `Payroll:PayRuns` sequence, a pay schedule (or named people), a type (`Regular`, `Supplemental`, `OffCycle`, `Final`, `Correction`, `Contractor`, and `Reversal` and `Historical` for later slices), a period, a pay date, the supplemental method, the totals and issues of its last calculation, a version stamp, the approval request and a status on `PayRunStatuses.Transitions`: Draft, Calculating, Calculated, PendingApproval, Approved, Processing, Paid, PartiallyPaid, Failed, Canceled. A regular, final or correction run pays salaries, pay items and the pay input sources; the others pay only their own inputs.

`IPayRunService`:

- `CalculateAsync(run, user, staffMemberIds)` calculates a draft `PayStatement` per active profile of the employer on the schedule (or per named person), with the source inputs and the run's own `PayRunInput`s, under a distributed lock on the run; a subset recalculates only those people. Issues are the profiles' blockers and the calculation's warnings; the totals are gross, both sides' taxes, deductions, contributions, reimbursements, net and cash needed. A run waiting for approval or approved goes back to Calculated, and its pending request is cancelled: an approval is for one calculation (the request carries the run's version).
- `SubmitAsync(run, user)` asks Approvals' `PayRunApproval` policy (seeded by the migration with the `ApprovePayRuns` permission as approver) when the run is calculated and has no blocker. The request is limited to the employer (`AccessScope`, and the `PayRun` source access answers by the payroll scope), adds the employer's own `ApprovePayRuns` permission to the request's approvers, and names the preparer as a subject who may not decide unless **Settings > Payroll** allows it (which also sets the policy's requester and subject rules).
- `PayRunApprovalSubjectHandler` takes the decision: approved for the current version moves the run to Approved; rejected or cancelled returns it to Calculated.
- `CancelAsync` cancels an open run and removes its drafts.
- `ProcessAsync(run, user)` processes an approved run (or a partly paid or failed one again) under the run's lock: each draft gets a number from the `Payroll:PayStatements` sequence, the printed addresses (`IAddressFormatter`) and the status Final; pay items move (`PaidTowardGoal`, and `ArrearsBalance` for a code that carries arrears forward, the value before kept on the line as `ArrearsBefore`); the lines' source inputs are marked consumed under `payrun:{runId}`; each payment of net pay is paid by the `IPayStatementPaymentMethod` of its `NetPayMethod`, keeping its reference or its problem; a statement whose payments are all paid is Paid; the run is Paid, PartiallyPaid or Failed. `IPayStatementObserver`s hear each issued statement.
- `VoidStatementAsync(statement, user, reason)` stops the payments that have not gone out (`IPayStatementPaymentMethod.VoidAsync`), creates a numbered reversing statement (`VoidOf`) with the opposite lines, so the accumulators cancel, moves the pay items back, releases the source inputs, marks the original Voided (`VoidedBy`) and updates the run's totals.
- `ReissuePaymentAsync(statement, index, user, reason)` stops a payment and pays it again; `PayStatementPayment.Attempt` goes up, and is part of the idempotency key of the new disbursement.

Payment methods come from features:

- **Direct Deposit** (`CrestApps.OrchardCore.Payroll.DirectDeposit`: Payroll, Banking, Disbursements) registers `DirectDepositPaymentMethod`: an ACH `Disbursement` from the employer's `FundingBankAccountId` to the row's bank account, source kind `PayStatement`, access scope the employer, effective on the pay date, idempotency key `payroll:{statementId}:{index}` (plus `:{attempt}` after a reissue).
- **Paychecks** (`CrestApps.OrchardCore.Payroll.Checks`: Payroll, Checks) registers `PaycheckPaymentMethod` (a check on the funding account) and `PayStatementCheckVoucherSource`, which prints the statement's lines with hours, current and year-to-date amounts as the check's voucher. A deposit with no Direct Deposit is paid by check.

`PayStatementSourceAccess` (source kind `PayStatement`) limits those disbursements and checks to the employers whose runs a user may see (`ViewPayRuns`) or process (`ProcessPayRuns`). `PayStatementNotificationObserver` sends the `PayStatementReady` notification type (registered when Notifications is on) to the staff member's user, with a link to the statement in My Pay (`MyPayUrls`) and no amount, and removes it when the statement is voided. `PayStubDocument` writes a statement as a PDF with Pdf.Core's standard fonts; `PayStubService` gives it the reader's texts.

`PayRunInputImporter` saves rows (run number, employee number, pay code, hours or amount, rate, date, note) as run inputs and refuses a row with the reason; the **Save pay inputs** pipeline step (a destination: rows in, **Rejected** out with an `Error` field, like **Save content items**) uses it, so a file of bonuses read with **Read a media file** goes straight into a run. The run is named by a `PayRun` field or the `PayRun` run parameter; the user the pipeline runs for must prepare the employer's runs. Runs already calculated are calculated again for the people whose inputs changed.

**Payroll > Pay Runs** lists the runs of the employers the user may see (`ViewPayRuns`), with an employer filter. A run's page shows its totals, issues, inputs (add or remove, `PreparePayRuns`; nobody enters their own pay), the register of statements and **Calculate**, **Submit for approval** and **Cancel the run** in the action bar; people who may approve see **Approve** and **Reject** there (decided through Approvals), and people who may process an approved run see **Process and pay** (`ProcessPayRuns`). A statement's page shows its number, lines and payments with their references and problems, **Download the stub** (`ViewPayRuns`), and **Reissue** and **Void** (`VoidPayStatements`).

## My Pay

The **My Pay** feature (`CrestApps.OrchardCore.Payroll.SelfService`, depends on Payroll) gives a person their own pay under `~/Admin/my-pay`, linked from **Staff > My Profile** (`IStaffHomeLinkProvider`, shown with `ViewOwnPay`). `MyPayController` reads only the records of the staff member linked to the signed-in user (`IStaffDirectory.FindByUserIdAsync`): someone else's statement or profile, and a draft statement, are not found.

- The home lists the person's statements that are not drafts (an `<admin-list>` of `MyPayStatementRow`s with SummaryAdmin and SummaryAdminGrid rows), their pay profiles, and a link to Banking's **My bank accounts** when Banking is on and they may manage their own accounts.
- A statement page (the lines through the shared `PayStatementLines` partial, the payments with their references) and its stub (`PayStubService`).
- **Pay details** (`ManageOwnPayDetails`): the pay profile editor in the `MyPay` group (`PayrollConstants.MyPayEditorGroup`), in which `PayProfileDisplayDriver` shows and reads only the tax elections (a new one counts from today by default) and the net pay split; nothing else of the profile changes. A save tells the `IPayProfileObserver`s ("Changed in My Pay"), so Audit Trail records it.
- `PayDetailsChangedNotificationObserver` sends the `PayDetailsChanged` notification type to the person whenever their elections or net pay split change, whoever changed them: a security notice that names who changed it, never an account number.

## Integration

- **Observers.** `IPayRunObserver` hears each step of a run (Opened through `IPayRunService.OpenAsync`, Calculated, Submitted, Approved, Rejected, Paid, PartiallyPaid, Failed, Canceled) after it is saved; `IPayStatementObserver` hears issued and voided statements; `IPayProfileObserver` profile changes. Workflows, webhooks and the audit trail hang off them.
- **Workflows** (with Workflows on): the events Pay Run Opened, Calculated, Submitted for Approval, Approved, Rejected, Processed, Paid and Failed, Pay Statement Issued and Voided, and Pay Profile Changed, started after the change is committed; the tasks Open Pay Run (once per employer, schedule, type and period: `Exists` otherwise), Add Pay Input (as a file row, through `PayRunInputImporter`), Calculate Pay Run (`Blocked` when a person has a blocker), Submit Pay Run for Approval, Approve and Reject Pay Run (as the person the workflow runs for, under the policy) and Process Pay Run (`Paid`, `PartiallyPaid`, `Failed`). A returned deposit starts Disbursements' **Disbursement Returned** event (source kind `PayStatement`) and a changed bank account Banking's **Bank Account Changed** event. The **Automation: open and calculate pay runs on schedule** recipe adds, turned off, a workflow that opens, calculates and submits the period's run every weekday morning and tells the payroll administrators when it has blockers. Stubs are not stored by a task: they hold people's pay and are written on request (the media library serves its files publicly).
- **Data source** `Payroll` (with Data Pipelines Core): the data sets Pay inputs, Employees, Pay runs, Pay statements, Earnings, Deductions, Taxes, Employer contributions, Payments of net pay (no bank account) and Year-to-date balances, read by `PayrollReportReader` for the employers in the user's `ViewPayRuns` scope, filtered by pay date, and Journal entries for the user's `ExportPayrollData` scope (see [Payroll journal](#payroll-journal)).
- **Reports** (Workforce & Payroll, with Reports on): Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution and Year-to-date Earnings, for `ViewPayRuns`; the dashboard widgets **Payroll Cash Requirement** and **Payroll Summary**.
- **Webhooks** (with Webhooks on): `payroll.payrun.approved`, `payroll.payrun.processed` (with the run's totals), `payroll.statement.issued` and `payroll.statement.voided` (ids and dates, never a person's pay).
- **Calendar** (with Calendars on): the `Payroll.PayDates` source shows the pay date of each run of the employers a person may see, and a staff member's own coming pay dates on their personal calendar; all day and not busy.
- **Audit Trail**: pay run submitted, approved or rejected, processed, cancelled, and pay statement voided, with the run or statement number; never a person's pay.
- **Notification types**: Pay statement ready, Pay details changed, Deposit of pay returned (Direct Deposit) and Bank account changed (a staff member's account added or its number changed, with Banking on). None carries an amount or an account number.
- **Personal data**: a person's pay profiles (the last characters of the tax id, never an account number) and final statements are exported; anonymizing changes none of them, since the employer must keep payroll records as long as tax law requires.
- **Pay history**: the opt-in `PayrollPayHistory` deployment step exports final statements (without runs, bank accounts, disbursements and checks) and the recipe step imports them into a historical run per employer and pay date, paid and read-only (a statement imported as history can't be voided or paid again), skipping statements already there.

## Taxes

`IPayrollTaxProvider.CalculateAsync(PayrollTaxRequest)` is stateless. The request carries the employer and its registrations, the worker type, the home and work jurisdictions, the elections in effect on the pay date, the pay date and frequency (periods a year), the regular and supplemental wages of each tax category, the supplemental method, the accumulators and the currency; the result is tax lines (code, name, jurisdiction, category, payer, taxable wages, amount) and warnings. **Settings > Payroll** names the provider (`PayrollSettings.TaxProvider`; empty uses the only one there is) and the least net pay. With no provider, nothing is withheld and the calculation says so. A contractor has nothing withheld.

**Payroll Tax Tables** (`CrestApps.OrchardCore.Payroll.TaxTables`, on Taxation's **Tax Tables** feature) is the built-in provider, `TaxTables`. A `PayrollTaxRule` is a tax of one jurisdiction on the wages of one category, paid by the employee or the employer, with one of Taxation's calculation methods (`Percentage`, `FixedAmount`, `TaxTable`, `Progressive`, `Threshold`), a rate in percent, a fixed amount or a Taxation tax table, a yearly wage base, a yearly wage threshold (only the year's wages above it are taxed, `WageThreshold`), whether the wages are annualized, whether the person's election applies (exempt, allowances times `AllowanceAmount`, extra withholding), a flat supplemental rate, a rate on a year's supplemental wages above an amount whatever the method and the election (`SupplementalExcessRate`/`SupplementalExcessThreshold`), and dated versions (`EffectiveFrom`/`EffectiveTo`, never two versions of one `TaxCode` on the same day). A rule applies when the employer is registered in its jurisdiction and, for a jurisdiction below a country (with a dash), the person lives or works there (or has neither on file). A rule whose table is missing or not in effect on the pay date is skipped with a warning, and a table a rule uses can't be deleted (`ITaxTableUsage`).

A second pay in a period (a bonus paid on its own, a correction) is withheld, for an annualized rule or a withholding method, on the period's total less what the period's earlier pays were taxed on (`PayAccumulator.PeriodTaxableWages`), which is the IRS aggregate method for supplemental pay paid separately; extra withholding is added once a period. The request carries the year's wages and supplemental wages of each category before the pay (`YearWages`, `YearSupplementalWages`), from the pay codes' totals.

A country pack adds a **withholding method** for a form a single table can't express: `IPayrollWithholdingMethod` (`Name`, `DisplayName`, the `Tables` and `Parameters` it takes by key, `CalculateAsync(PayrollWithholdingContext)`), registered with `services.AddPayrollWithholdingMethod<T>()`. A rule names it as its `Method` and fills `Tables` (Taxation tax table ids, worked out as progressive brackets) and `Parameters` by key; the rule editor shows a card of them for each method, and a rule missing one of the method's tables is refused.

Rules are under **Payroll > Setup > Tax rules** (`ManagePayrollSettings` for every employer, since rules are the same for all). The `PayrollTaxRules` recipe step (`Rules`, matched by id, then by tax code and first day) and the **Payroll tax rules** deployment step carry them:

```json
{
  "name": "PayrollTaxRules",
  "Rules": [
    { "Name": "Social insurance", "TaxCode": "US-SS", "Jurisdiction": "US", "Category": "SocialInsurance", "Payer": "Employee",
      "Method": "Percentage", "Rate": 6.2, "WageBase": 176100, "EffectiveFrom": "2026-01-01" },
    { "Name": "Income tax", "TaxCode": "US-FIT", "Jurisdiction": "US", "Category": "IncomeTax", "Payer": "Employee",
      "Method": "Progressive", "TaxTableId": "<a tax table id>", "Annualize": true, "UsesElections": true, "AllowanceAmount": 4000,
      "SupplementalRate": 22, "EffectiveFrom": "2026-01-01" }
  ]
}
```

## Retro pay, tips, earlier weeks, gross-up and an extra pay date

- **Retro pay** (`RetroPayCalculationStep`, order 110, regular runs only): for each earlier final statement of the year, an hours line of a code with **In retro pay** paid below the rate now on file for its period (times the code's multiplier), and a full period's salary below the salary now on file, owe the difference, less what earlier retro lines (`PayStatementLine.RetroFor`) already paid for that statement. Only raises are paid. The context factory loads the person's final statements with the employer (`PayCalculationContext.History`) for regular runs and for amounts earned in earlier weeks.
- **Tip credit** (`TipCreditPayCalculationStep`, order 120): for a profile marked **Paid a tipped wage** (`EmployeePayProfile.Tipped`), each week's cash wage of the hours worked plus the tips that count (at most `PayrollSettings.MaximumTipCredit` an hour) must reach `PayrollSettings.MinimumHourlyWage` for those hours; what falls short is paid on the code of kind `MinimumWageMakeup` (seeded `TIPMU`). Tips of a week are the tip lines dated in it, undated ones spread by hours. Cash tips the person kept are reported on `CTIPS` (imputed income of kind `Tips`): taxed, not paid.
- **Overtime of earlier weeks** (`EarnedRegularRatePayCalculationStep`, order 210): an amount of a code that counts in the regular rate with `EarnedFrom`/`EarnedTo` before the period (a run input's **Earned from** and **Earned to**) raises the regular rate of those weeks: the amount divided by all hours worked in the statements of those weeks, times each overtime hour's multiplier less one, paid as the premium of the overtime code with the most hours (29 CFR 778.209-210).
- **Gross-up**: a run input marked **The amount is what the person takes home** (`PayRunInput.IsNet`) is grossed up: the pay is worked out without it, then with the earning raised until net pay is that much more (one a person a run; others are left out with a warning).
- **An extra pay date**: a schedule's `ExtraPayPeriod` is `Standard` (each pay is the salary divided by the usual number of pay dates, so a year with 27 every-two-weeks pay dates pays one more) or `DivideByPayDates` (the salary is divided by that year's pay dates, `PayCalculationContextFactory.PayDatesIn`). Taxes keep annualizing by the usual number.

## Garnishments

**Garnishments** (`CrestApps.OrchardCore.Payroll.Garnishments`, on Payroll) keeps `GarnishmentOrder` records (`IGarnishmentOrderManager`, in the Payroll collection, `GarnishmentOrderIndex` by employer, profile, staff member and status): the pay profile (its employer and staff member follow it on every save), the kind (`Support`, `FederalTaxLevy`, `StateTaxLevy`, `Bankruptcy`, `StudentLoan`, `Creditor`, `Other`), case number, agency, a deduction pay code, an amount and/or a percent of disposable earnings each pay, a limit percent, the pay left each week (`ProtectedWeeklyAmount`), the exempt amount of a levy, the total owed and what was withheld, a priority, the received, first and last pay dates, the status (`Active`, `Suspended`, `Completed`, `Released`), and the payee (name, mailing address, paid by `Check`, `BankAccount` with a Banking account owned by the payee, or `Outside`). `GarnishmentDefaults` gives each kind its United States federal limit and priority, which an order may change: support 50% (to 65%), a student loan 15%, a creditor 25% and 30 times the federal minimum wage a week (217.50) left; support first, then tax levies, bankruptcy, student loans and creditors. The feature seeds the `GARN` pay code (kind `Garnishment`, cut last by the net check).

The calculation step (`GarnishmentsPayCalculationStep`, order 750, after the deductions that are a percent of disposable earnings) takes each order in effect on the pay date, lowest priority number first, then by the day received: disposable earnings are gross pay less the employee's taxes; support orders share their own percent; a tax levy takes what is left above its exempt amount; a bankruptcy order takes its amount; every other order shares the creditor limit (the smaller of its percent and what is left above the pay a week, less what the orders before it took); an order stops at its total owed. A line (`PayStatementLine.GarnishmentOrderId`) is added per order, with a warning when a limit held one back.

`GarnishmentStatementObserver` (an `IPayStatementObserver`) moves `Withheld` when a statement is issued (an order that reached its total completes) and sends the withholding to the payee from the employer's funding account, by a check (Checks) or an ACH disbursement (Disbursements), as a payment of the statement (source kind `PayStatement`, scoped by the employer, idempotency key `payroll-garnishment:{statement}:{order}`), recording each in `Remittances` with its reference or problem. A void gives the withholding back, reopens a completed order and stops a payment that has not gone out.

Pages: **Payroll > Garnishments** (`ManageGarnishments`, per employer) lists the orders of the employers a user manages, with an editor of three cards (the order, how much, the payee) plus the payee's address and what was sent; nobody manages their own order unless the setting allows changing one's own pay; an order that withheld anything is released, never deleted. The `PayrollGarnishments` recipe step (`Orders`, matched by id) and the **Garnishment orders** deployment step (fails closed without a user, for `ExportPayrollData`; case numbers and payee accounts only with **Include sensitive data**; never what was withheld or sent) carry them, and the **Garnishments** data set (for `ExportPayrollData`) has one row per order.

## Contractors

A pay profile of worker type **Contractor** is paid by a **Contractors** pay run (or with others) from the amounts entered, with no pay rate needed and nothing withheld, except contractor withholding: the tax category `PayrollTaxCategories.ContractorWithholding` (registered by a country pack) applies only to contractors, and only when the profile's `BackupWithholding` is on or no tax id is on file (`PayrollTaxRequest.ContractorWithholding`); employees never have it. The United States recipe adds `US-BWH`, backup withholding at 24%. Contractors are left out of Forms W-2, 941 and 940; **Forms 1099-NEC and 1096** (`US-1099NEC`) list each contractor paid at least the year's threshold (2,000 from 2026, 600 before, `Form1099Nec.ThresholdFor`) or withheld on, with box 1 (payments) and box 4 (backup withholding) and the 1096 count and totals, a contractor with no tax id noted, a 1099-NEC PDF per contractor (also in My Pay) and the same filing, amending and notice as the W-2.

## Liabilities, filings and year end

Payroll calculates and prepares; the business deposits and files (decision 4).

- **Liabilities** (`ITaxLiabilityService`, derived, never stored): the tax lines of the final statements (both sides, reversals with their pay date) by jurisdiction and group (`TaxLiabilityGroups.Withholding`, `Unemployment`) into periods of the registration's `DepositFrequency` (`DepositSchedules.Monthly`, by the 15th of the next month; `SemiWeekly`, Wednesday to Friday by the next Wednesday and Saturday to Tuesday by the next Friday; `Quarterly`; `Annual`; unemployment always by quarter), with the `TaxDeposit` records the business enters (`ITaxDepositManager`), the balance and the due date. Due dates are not moved off weekends and holidays.
- **Filings** (`PayrollFiling`, `IPayrollFilingManager`, `IPayrollFilingService`): a country pack registers `IPayrollFilingForm`s (`services.AddPayrollFilingForm<T>()`: name, jurisdiction, quarterly or yearly, per-person statements, `PrepareAsync(PayrollFilingContext)` over the period's final statements, pay codes, profiles and deposits). A filing is prepared as a draft (prepared again on request), marked filed with the day and the agency's confirmation (never changed again; people with a statement in it get the `TaxFormReady` notification, linking to My Pay), and amended by a new draft that names it (`AmendsId`); filing the amendment marks the first `Amended`. Rule C: a filed form and a deposit counted in one are kept.
- **Year end** (`YearEndBlockersAsync`, `CloseYearAsync`, `ReopenYearAsync`): a year closes once every pay run of it is paid or cancelled; closing prepares the year's per-person forms and adds the year to `Employer.ClosedYears`, after which no run with a pay date in it is opened, calculated or processed and no statement of it is voided.
- Pages: **Payroll > Liabilities and Filings** (`ManagePayrollFilings` per employer): **Liabilities** (the periods and the deposits as `<admin-list>`s, a deposit form), **Filings** (the list, **Prepare**, a filing's page with its boxes, the people's statements as PDFs, **Mark as filed**, **Prepare again**, **Amend**), **Year end**. My Pay lists a person's filed forms with a download. Data sets **Tax liabilities** and, with United States Payroll, **W-2 wage records**; the `PayrollTaxDeposits` and `PayrollFilings` recipe steps and the **Tax deposits and filings** deployment step (fails closed without a user, `ExportPayrollData`).
- **United States forms**: **Form 941** (lines 1 to 15: employees paid in the pay period of the 12th of the quarter's last month, wages, income tax, Social Security and Medicare wages with the form's rates, the fractions-of-cents adjustment against what was withheld and matched, deposits, balance or overpayment), **Form 940** (payments, exempt payments, payments over the federal unemployment wage base, taxable wages and tax at the `US-FUTA` rule's rate, deposits) and **Forms W-2 and W-3** (boxes 1 to 7 and box 12 from pay codes whose form boxes name `W2:12D` and the like; the W-3 totals and count). The **SSA EFW2** file format (`efw2`, `Efw2DataFileFormat` over `UnitedStatesEfw2`) writes the W-2 wage records data set as RA, RE, RW, RT and RF records of 512 characters for one tax year; the submitter's SSA user id is the account id of an employer registration whose agency is `SSA`. The full social security number is read only for `ViewTaxIds` of the employer, and each one read is recorded. The **United States Payroll: W-2 wage records as an SSA EFW2 file** recipe adds the draft pipeline (no destination: add one that keeps the file private).

## United States

**United States Payroll** (`CrestApps.OrchardCore.Payroll.UnitedStates`, on Payroll Tax Tables) registers Form W-4 as the election of the `US` jurisdiction (`FormYear` 2020 or 2019, `FilingStatus` Single, MarriedFilingJointly or HeadOfHousehold, `MultipleJobs`, `Dependents`, `OtherIncome`, `Deductions`, `ExtraWithholding`, `Allowances` for a 2019 form, `Exempt`) and the `UnitedStatesFormW4` withholding method: IRS Publication 15-T, Worksheet 1A (percentage method for automated payroll systems). For a 2020 or later form, the period's wages times the periods a year, plus other income, less deductions and, when Step 2 is not checked, the line 1g adjustment (`AdjustmentMarriedFilingJointly`, `Adjustment`); the annual table of the filing status (`Single`, `MarriedFilingJointly`, `HeadOfHousehold`, or the same with `.Step2` when the box is checked); divided by the periods, less the dependents credit for a period. For a 2019 or earlier form, the year's wages less the allowances times the rule's `AllowanceAmount`, on the standard table of single or married filing jointly. A person with no election is withheld as single with no adjustments.

The **United States Payroll: federal tax rules for 2026** recipe adds, for pay dates in 2026, the six annual tables of Publication 15-T (2026) as Taxation tax tables and the rules `US-FIT` (W-4 method, 22% flat supplemental rate, 37% on a year's supplemental wages over 1,000,000, 4,300 an allowance, 12,900 and 8,600 for line 1g), `US-SS-EE` and `US-SS-ER` (6.2% on 184,500), `US-MED-EE` and `US-MED-ER` (1.45%), `US-MED-ADD` (0.9% withheld above 200,000 of a year's wages) and `US-FUTA` (0.6% on 7,000, the rate after the full state credit). They apply to employers registered in `US`. Each year ships as a new dated recipe; check the amounts against the IRS's current publications. State and local taxes are set up by hand or come from a tax engine's provider.

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

The accounts are `PayrollJournalSettings` under **Settings > Payroll** (card **Journal accounts**, for `ManagePayrollSettings`): one per kind, and rows that give a pay code (by its code) or a tax (by its tax code) other accounts, an empty side keeping the default. The settings travel with Orchard's `Settings` recipe step (schema `PayrollJournalSettings`). The **Payroll: export the payroll journal to a CSV file** recipe adds a draft data pipeline that groups the entries by pay date, employer, account and department and writes a CSV file; add the delivery (SFTP, web API, email) and a workflow timer.

## Teams and settings

With Teams on, people with `ManagePayrollSettings` get a **Payroll** card on the team editor: `PayrollTeamSettings` with the team's `LedgerCode` and a work site's `Jurisdictions`.

`PayrollSettings` (**Settings > Payroll**): `AllowPreparerToApprove` and `AllowChangingOwnPay` (both off: separation of duties), the tax provider and the least net pay, and the default country pack and currency of a new employer.

## Upgrading from the time clock's pay periods and pay codes

Before Pay Setup, the time clock kept `PayPeriodCalendar` and `PayCode` in the Workforce collection. The time clock's migration (`UpdateFrom2`) moves each one to the Payroll collection with the same id, so policies, timesheets and lines keep pointing at them:

- a pay code's old kind number becomes its name, and its window goes into `WorkforcePayCodeSettings`; unpaid codes become `Memo`, the others `Earning`;
- a code Pay Setup seeded a moment before with the same code is replaced by the old one;
- a record already in the Payroll collection with the same id is left as it is;
- the old documents and index tables are removed, so a second run moves nothing;
- roles with **Manage time clock policies** get **Manage pay setup**.
