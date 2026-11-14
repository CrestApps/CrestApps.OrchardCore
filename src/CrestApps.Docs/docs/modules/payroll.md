---
sidebar_label: Payroll
title: Payroll
description: "Payroll for Orchard Core. Pay Setup owns pay schedules (periods, the overtime week and pay dates) and pay codes (class, open kind, multiplier, premium, export code), shared by Workforce's time clock, with usage checks, recipe and deployment steps, and the migration that moved the time clock's pay periods and pay codes."
user_manual:
  - user-manual/finance/pay-setup
  - user-manual/finance/payroll-employers
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

`PayrollPermissions` holds the Payroll feature's permissions. Every one in `PayrollPermissions.PerEmployer` also exists per employer through `PayrollPermissionTemplates.For(permission, employerId)` (named `{Permission}_{employerId}`, implied by the global permission; a per-employer "view" is also implied by the same employer's "do" permissions). `PayrollPermissionProvider` lists each employer's copies, so the role editor shows a **Payroll: {employer}** group per employer.

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

## Teams and settings

With Teams on, people with `ManagePayrollSettings` get a **Payroll** card on the team editor: `PayrollTeamSettings` with the team's `LedgerCode` and a work site's `Jurisdictions`.

`PayrollSettings` (**Settings > Payroll**): `AllowPreparerToApprove` and `AllowChangingOwnPay` (both off: separation of duties), and the default country pack and currency of a new employer.

## Upgrading from the time clock's pay periods and pay codes

Before Pay Setup, the time clock kept `PayPeriodCalendar` and `PayCode` in the Workforce collection. The time clock's migration (`UpdateFrom2`) moves each one to the Payroll collection with the same id, so policies, timesheets and lines keep pointing at them:

- a pay code's old kind number becomes its name, and its window goes into `WorkforcePayCodeSettings`; unpaid codes become `Memo`, the others `Earning`;
- a code Pay Setup seeded a moment before with the same code is replaced by the old one;
- a record already in the Payroll collection with the same id is left as it is;
- the old documents and index tables are removed, so a second run moves nothing;
- roles with **Manage time clock policies** get **Manage pay setup**.
